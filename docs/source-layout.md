# Client source layout and optimization

The project uses Godot 4.7.2 .NET. Files are grouped by responsibility; class names, C# namespaces, scene node names and paths within each scene are retained. Godot script UID files moved with their scripts. Scene resources, autoloads, tooling references and the iOS NativeAOT descriptor path were updated together.

| Directory | Responsibility |
| --- | --- |
| scripts/account | Identity, inventory and player profile |
| scripts/arena | Background terrain, wildlife, shadows and menu preview |
| scripts/cards | Card shop, drag/drop, card definitions and presentation |
| scripts/combat | Battle flow, unit state, local simulation and authoritative replay |
| scripts/combat/visuals | Animations, impacts, projectiles and screen effects |
| scripts/combat/simulator | Simulator and saved visual settings |
| scripts/core | Preferences and source-generated JSON metadata |
| scripts/decks | Deck builder, analysis and history charts |
| scripts/localization | Translation and cache |
| scripts/network | Nakama transport, online protocol, scene reconciliation and recovery |
| scripts/platform | Desktop/mobile layout and safe areas |
| scripts/replay | Replay persistence, browser, timeline and player |
| scripts/ui | Menus, skins, modal and shared UI components |

## Reduced repeated work

- Static forest bushes draw in shared canvas nodes grouped by their original Y coordinate. Row positions retain Y sorting against animals, heroes and other arena items. Art, destination rectangles, nearest filtering, marker order and walkable-cell calculations remain unchanged.
- Wildlife reuses a four-entry neighbor buffer instead of allocating LINQ iterators and an array for every roaming decision. Candidate order, random calls, distances, timing and movement rules are retained.
- Cloud projection parameters update only when the viewport size or field transform changes. The existing elapsed-time uniform still updates each frame; zoom, camera and window resizing remain supported.
- Card placement validates hand tokens/catalog names with direct loops, avoiding captured predicate allocations on the drag-validation path. Slot selection and placement restrictions remain unchanged.

Measured on this Windows Debug build: the authored forest has 255 bushes rendered by 30 depth-sorted nodes. The existing 10,000-placement-validation diagnostic allocated 240,040 managed bytes before removing captured predicates and 40 bytes afterwards (the Stopwatch instance). This measures that validation path only.

No general FPS, mobile throughput or loading-time improvement is claimed. Scene tests check visible rendering, depth placement, wildlife constraints and viewport transformations. Existing interaction tests were updated to select heroes for hero slots, count card views separately from empty-slot panels, and compare against current combat plans rather than legacy fixed attack counts.

## Backend

The backend keeps Nakama adapters under internal/server and gameplay under internal/game. See its docs/nakama-api-audit.md for the feature inventory, storage transactions, replay archives and the single-node deployment boundary. No previous SQL data migration is included.


## Validation results (2026-10-23)

Debug and ExportRelease builds passed with no warnings. Native drag/drop and battle integration passed (667 checks); the wildlife suite passed 6,000 roaming iterations and confirmed the 255-to-30 forest node reduction; cloud rendering/zoom assertions passed; combat simulator and initiative suites passed (175 and 61 checks). Scene/autoload script paths all resolve, and all 83 moved script UIDs match their original bytes. At the end of the initial optimization pass, only ArenaCloudShadows, ArenaWildlife and CardShop had production implementation changes; the other moves retained their original implementations. The subsequent [Undead Hollow area feature](undead-hollow.md) also adds scene selection to the lobby, settings, battle display and replay presentation.
