# Hand capacity and generated cards

The ten-card shop limit counts only cards currently in hand that originated from
purchases. Generated cards do not consume these slots. Example: 9 purchased + 16
generated cards may buy one more card; 10 purchased + any generated cards may not
buy an eleventh purchased card. Existing purchase merges are resolved before the
capacity check. A purchase merged with a generated card remains a purchased card.

`generated` is an authoritative boolean on backend Card/Unit and client
OnlineCard/OnlineUnit/CardData. Missing/false means purchased. Deployment and return
retain origin. Generated units may return to an overflowing hand, subject to the
existing readiness/veteran restrictions; purchased units still need a shop slot.

Future authoritative skill resolution calls Room.GrantGeneratedCard(userID, kind).
This grants a fresh token without spending coins, taking shop pool copies, merging,
or capping hand size. There is no client action/RPC to request generated cards.
Offline skill resolution uses CardShop.AddGeneratedCard(kind); online hands are
received from the server. No existing character skill has been changed or added.

Above ten cards the hand retains its ten-card size and uses negative spacing to
fit all cards. Hover reveals the covered part, click opens details, exposed strips
support drag. Spacing and empty slots recover when the hand shrinks. Opponent backs
are allocated to the actual hand count rather than truncated to ten. Overflow
shows total hand count and the purchased-card quota separately.

Validation: backend hand_test.go covers grants, shop limits, rejected-action
atomicity, merge origin and deploy/return. tests/hand_overflow.tscn checks 10, 11,
25 and 40 cards, real pointer targeting and detail clicks, drag eligibility, normal
spacing recovery, incoming provenance and opponent overflow, on desktop/mobile.

The broader native integration suite passes purchase, drag, deployment, return,
selling and purchased-hand capacity checks, but currently stops at its dead-unit
inspection assertion. Its health-signal check now applies damage through armor so
it actually reduces HP for armored characters. No combat implementation changed.
Backend go test ./... and go vet ./... pass; targeted desktop/mobile overflow runs
pass with no runtime errors.
