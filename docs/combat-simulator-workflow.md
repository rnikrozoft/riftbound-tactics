# Combat Simulator for effect editors

Open the game and choose **COMBAT SIMULATOR / OFFLINE** on the login screen. No account, server IP, or backend connection is required. The lobby also retains its simulator entry.

Choose attacker A, target B, source attack, displayed animation, particle, anchor, position, and size. Attacks repeat automatically and each change saves on this device. Settings are separate for each A + source attack + B combination and apply across that character's star tiers. Use arrow keys to edit selectors and numbers; Tab moves focus.

Choose **EXPORT JSON**, select a destination, and send `combat_visuals.json` to the game maintainer. Export includes all saved combinations and packaged defaults. Local edits and export do not publish to the backend or other players automatically.

For a game release, validate the received JSON, review the changed entries, then merge the approved entries into `data/combat_visuals.json` and build the client. Keep the `schema_version` and `entries` structure. Existing local overrides take priority over packaged settings on an editor's device.

**BACK** returns to the login screen when opened offline; **LOBBY** returns to the lobby when opened there.
