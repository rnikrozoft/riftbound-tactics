# Marvel Duel shop and player health research

These sources describe the 2020-2021 game. They establish the historical baseline; no claim is made that every later patch is unchanged.

## Shop rules adapted for Riftbound

Start at shop level 2, with a maximum level of 6. Offer capacities at levels 2/3/4/5/6 are 3/4/4/5/5. Base upgrade prices for 2-to-3, 3-to-4, 4-to-5, 5-to-6 are 2/8/10/14 coins. Each subsequent round discounts the current pending upgrade by 2, to a minimum of zero. Buying an upgrade resets the next upgrade to its full base price, with no extra same-round discount. For example: 2-to-3 costs 2 in round one, or zero if delayed until round two. After a free level-three upgrade in round two, level four costs 8 immediately or 6 in round three.

Riftbound leaves all existing offers and empty slots unchanged on upgrade. New capacity and card tiers apply on paid reroll or normal next-round refresh/refill. Reroll costs 2. Lock/unlock is free; locked shops preserve offers across rounds and fill vacant slots at the next preparation. Reroll requires unlocking first. Cards can only appear if their purchase price is at most the shop level. Four copies of each card kind are available in each player's independent pool; selling does not replenish it.

Round-start income is 4/6/9/12/15/18/20, capped at 20 from round seven. Unspent coins always carry over: previous balance plus new round income. Four unspent coins plus six in round two equals ten. There is no win bonus or loss penalty to coins.

Sources:
- Shop guide, including base costs and offer capacities: https://www.iyingdi.com/tz/post/5105383
- Gem and economy guide: https://www.iyingdi.com/tz/post/5107585
- Thai upgrade timing examples (free first upgrade in round two; six-cost upgrade in round three): https://www.4gamers.co.th/news/detail/2762/marvel-duel-how-to-upgrade-your-shop
- Official patch notes show a first-refresh-free skill as a special effect, not the universal default: https://www.marvelduel.com/news/bulletin/20201022/33231_911367.html

Marvel Duel's historical sale rule returns one gem. The user explicitly chose a Riftbound custom rule: selling refunds floor(total purchase investment / 2), and the maximum shop is five offers. No additional win-income rule is inferred from these sources.

## Player health and damage

The September 2021 beginner guide, published by a NetEase games moderator on Xiaomi Game Center, states that losing hero damage equals the winning shop level plus the sum of star levels of surviving characters; zero health eliminates a hero. Source: https://game.xiaomi.com/viewpoint/1123748901_1631092346325_13 (game version v1.0.20210820).

Riftbound adapts this for two players: both start at 30 HP, loss damage is clamped at zero HP, a draw causes no damage, and zero HP immediately ends the match. Player health persists between rounds. The server counts actual winning battle survivors, never all deployed units or client-supplied results. The terminal game_over phase rejects all subsequent shop, ready and next-round actions.

## Character upgrades: custom rule requested by the user

Every duplicate purchase of the same card kind upgrades the existing hand card or deployed character by one star: 1, 2, 3, 4. Four purchased copies is the maximum. This differs from Marvel Duel's historical 1-to-2-to-4 merge sequence. Card price, shop level and veteran status are separate from star level.

Merging retains the original token and veteran status. A deployed upgrade returns to hand and frees its field slot. HP = 100 times stars, Attack = 30 times stars, Speed = 10 plus 2 times (stars minus 1). Actual attack damage scales by stars; speed reduces the pause between turns. Combined purchase investment is retained for the half-price sale refund. Deploying, returning and changing rounds preserve stars. Existing TravelBookLite star icons and Super Pixel Effects Gigapack Level Up/heal animations provide visuals without custom generated artwork.

Upgrading a deployed character automatically removes it from its field slot and returns the upgraded card to hand, even after Ready. Token, stars, investment and veteran status persist. The existing asset upgrade animation plays on the returned hand card. Field upgrades require a free hand slot; if full, the purchase is rejected without charging coins or consuming copies/offers. Upgrading an existing hand card still works with a full hand because it occupies the same slot. Manual return restrictions remain unchanged.
