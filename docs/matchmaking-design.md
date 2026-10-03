# Six-player matchmaking

## Implemented behavior

Lobby Play uses **Find Match** with the selected validated 40-slot deck. Legacy two-player room RPCs remain available for existing debug/integration scenes; the normal lobby no longer exposes room codes.

The client registers a realtime queue session and deck through `queue_begin`, then calls Nakama's socket `AddMatchmakerAsync`. The server `MatchmakerAdd` before hook replaces client properties, query and player counts. Nakama's `MatchmakerMatched` callback creates an authoritative `riftbound_league` with six reserved seats; empty seats become bots. An unreserved account cannot join.

Matchmaking uses a separate server-owned MMR, starting at 1,000. Compatible ratings must fit reciprocal numeric ranges (initial +/-150, widened to +/-300 after ten seconds). Client-supplied ratings and leaderboard positions are ignored. The visible leaderboard uses current MMR, descending, with rated-game counts. Wins remain a separate historical statistic and never determine ranking or matchmaking. Bots display an explicit bot label and do not have Nakama accounts or leaderboard records.

## MMR

Each human's final placement is compared with every other human in the match. A better placement scores 1, a tie 0.5, and a worse placement 0. Expected score is `1 / (1 + 10^((opponent MMR - own MMR) / 400))`, using ratings captured before the match. The change is the rounded mean of actual minus expected scores, multiplied by 32. Six equally rated humans finishing in distinct places receive +16, +10, +3, -3, -10 and -16. Ratings cannot fall below zero.

Bots are excluded from these comparisons. A match with only one human is unrated and does not increment rated games. A human's result is settled as soon as their placement is known, before leaving the match after elimination; the champion settles at game end. Transactions lock the account row and deduplicate results by match and user, so retries cannot award a second change. Failed writes retry while unresolved results retain the match state. `mmr_self` returns the authenticated account's rating and rated-game count; clients cannot write ratings. Existing wins are not converted into MMR. The authoritative Nakama `riftbound_mmr` leaderboard uses SET writes so losses lower the displayed score. Durable dirty flags retry failed updates and backfill existing rated accounts. Only accounts with at least one rated game enter the board.

This placement-based Elo adaptation belongs to Riftbound Tactics; it is not a verified Marvel Duel ranking formula.

The matchmaker processes once per second, preferring six humans. After eight unsuccessful matching intervals it may match a compatible smaller human group and fill it with bots. At twenty seconds a solo server fallback fills five seats. The client polls its server reservation, renews its ticket once at ten seconds to widen the range, removes the ticket upon assignment/cancellation, and cancels on returning to Lobby. A shared server lock makes matched-versus-fallback reservation atomic. Abandoned registrations expire after thirty seconds on the next queue registration. This queue is process-local and intended for the existing single Nakama node; distributed queues require shared coordination before deploying multiple nodes.

## Combat and elimination

Six players fight three independent 1v1 duels simultaneously. Preparation ends when **all surviving players** are ready or the sixty-second deadline expires. Ready players retain the existing allowed field editing rules until combat starts. Each client sees their own current pair; the local profile stays on the left even when their side changes. A public six-seat roster shows HP and placements; opposing hand/shop and preparing formations stay private.

Pairing enumerates all possible perfect matchings (at most fifteen for six players), strongly penalizes consecutive opponents, then minimizes repeated meetings. Random tie breaking prevents fixed seat bias. With only two players left, they meet every round.

With an odd number of survivors, one player fights a deep copy of an eliminated player's last formation. Ghost assignments rotate fairly among surviving players. The formation is frozen; combat mutations never modify the stored copy. Losing to it still causes HP damage. Ghosts cannot revive or receive leaderboard rewards.

All start at 30 HP. Existing damage remains the winning shop level plus surviving character stars; draws cause zero damage. Battle publishes a shared server deadline at most 30 seconds after phase entry. Long replay plans retain their full precomputed events, winner and damage but cap their presentation end time. Early finishers keep shop and field editing locked while waiting; the frontend shows remaining battle time. Once all presentation end times have passed (or the cap is reached), the server applies every pair's full result, eliminates zero-HP players, then starts preparation together after the existing two-second result transition. Every survivor receives the same new 60-second preparation deadline. The client skips remaining events at the replay deadline and applies their final visual health/death state. Coin balances and private deck inventory persist between pairs/rounds. Each player receives round income once. Simultaneous eliminations share a placement; one remaining survivor wins immediately. Eliminated clients receive their final HP/placement snapshot, then the server immediately removes their match presence. They cannot rejoin that match, and no longer receive its updates. Their Nakama authentication/socket remains usable for Lobby APIs. Eliminated seats and frozen formations are retained for standings and ghost opponents.

Only the final human champion receives the existing +1 leaderboard win, deduplicated through the durable match ledger. Bot champions receive no record; a single-round win does not grant leaderboard points or a coin bonus.

## Bots

Four immutable valid deck templates are prepared once at server startup. Each match allocates independent HP, coins, inventory and formations. Bots buy, deploy, merge characters, upgrade shops and reroll using the same validated economy actions as humans, with at most 24 shop decision steps per preparation. They receive no extra resources and use no login/socket or Godot process. A disconnected human is temporarily controlled by the same policy; returning to Lobby does not erase the seat. Bot display rating is the human group's mean MMR; the initial policy is shared rather than skill-calibrated.

No allocation/CPU benchmark has been performed. Object pooling is unnecessary until profiling shows a need.

## Research basis

Marvel Duel's normal mode uses six individuals facing 1v1 opponents in successive rounds and elimination through avatar HP. Public sources reviewed did not specify a reliable exact opponent-repeat cooldown or complete ghost-selection algorithm. The repeat avoidance and ghost rotation above are our adaptation, not a claim to reproduce undocumented Marvel Duel internals.

- [Marvel Duel gameplay review](https://potions.sg/gaming/reviews/command-your-favourite-heroes-and-villains-in-marvel-duels-mobile-game/)
- [App Store editorial](https://apps.apple.com/cn/iphone/story/id1578645420?l=en-GB)
- [Player example of an eliminated formation fight](https://www.taptap.cn/moment/454708477323905077)
- [Nakama Matchmaker](https://heroiclabs.com/docs/nakama/concepts/multiplayer/matchmaker/)
- [Nakama numeric query syntax](https://heroiclabs.com/docs/nakama/concepts/multiplayer/query-syntax/)

## Verification

`internal/game/league_test.go` covers six seats, opponent rotation, coin carryover, unique card tokens, frozen ghost copies and fair assignments, global readiness/deadline, elimination and the final survivor. `matchmaking_test.go` covers server-controlled ranks/counts, widening, rejected sessions, cancellation and concurrent matched/fallback assignment. Docker builds run Go tests with the race detector.

`tests/matchmaking_integration.tscn` connects six real Nakama clients, verifies one room, rank/privacy, automatic rounds and new opponents, purchase/deploy and HP damage, solo bot fill, restricted joining and cancellation. `tests/matchmaking_ui.tscn` checks Lobby, the six-seat view and the local profile position with rendered screenshots.

`mmr_test.go` covers equal and unequal ratings, tied placements, early elimination and bot exclusion. `tests/mmr_integration.tscn` plays a complete six-human ranked match, checks elimination settlement and disconnection, durable ratings after reconnect, duplicate protection, and using the updated MMR in the next solo queue.
