# MMR leaderboard

The visible leaderboard uses Nakama `riftbound_mmr`: authoritative, descending, SET operator, rank cache enabled, no scheduled reset. Scores are current MMR, not cumulative wins or peak MMR. SET allows scores to decrease after losses.

All accounts start at 1,000 MMR. Placement is compared against other humans using pre-match MMR; bots are excluded. Accounts enter the board after at least one rated game. Existing rated accounts are automatically synchronized, while historical wins are not converted into MMR. See [matchmaking design](matchmaking-design.md) for the formula.

The server commits each result once through `rift_mmr_results`, updates `rift_mmr`, then publishes its latest durable rating. A dirty flag retains failed updates for retry every fifteen seconds. Synchronization is serialized on the current single Nakama node and only clears the flag if both rating and game count still match, preserving concurrent newer changes.

The main menu calls `ListLeaderboardRecordsAsync` with fifty rows per page and the owner's record. It displays rank, name, MMR, rated games and UID, with refresh and cursor pagination. Battle profiles show their MMR and leaderboard position. Existing TravelBookLite assets supply frames and buttons.

The historical `riftbound_match_wins` board remains available for old integrations and win statistics, but normal UI and matchmaking use MMR exclusively.

Verification: Go race tests and frontend build; `tests/mmr_integration.tscn` checks complete six-human results, current leaderboard scores for every participant including losers, descending ordering, persistence, elimination, and next-queue ratings.
