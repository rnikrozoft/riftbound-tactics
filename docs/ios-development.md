# iPhone development build

Use the iOS export preset with the connected device's Apple development team and
`com.rnikrozoft.riftboundtactics` bundle identifier. Export the Xcode project, then
build and install with automatic development signing. Keep generated exports and
Xcode build products outside the repository.

For a local Nakama server, enter the Mac's Wi-Fi IP in Guest Login, without scheme
or port. Nakama listens on port 7350; localhost on the phone refers to the phone.
The iOS preset declares `NSLocalNetworkUsageDescription` and
`NSAllowsLocalNetworking`, while retaining ATS protection for other connections.
The user may need to enable the app under Settings > Privacy & Security > Local
Network. This setting is separate from developer certificate trust.

Nakama 3.17 TinyJson uses reflection for API/socket models and collection capacity
constructors. iOS NativeAOT must retain the Nakama assembly and the constructors
listed in `scripts/NakamaAot.xml`; otherwise authentication may successfully reach
the backend but fail in `Session.Update` with a NullReferenceException.

For an on-device smoke test, launch the debug app with environment variables
`RIFT_IOS_NETWORK_TEST=1` and `RIFT_IOS_NETWORK_HOST=<Mac LAN IP>`.
The test authenticates a dedicated test identity, connects its realtime socket,
fetches its account and loads player bootstrap via RPC. The result, without auth
tokens, is written to `Documents/ios-network-test.txt` in the app data container.
Launch normally afterwards to return to the login screen.

Mobile builds use a fixed 960x540 logical canvas with the aspect expanded to the
landscape display. There is no global magnification or two-finger canvas panning.
Menus use larger touch controls, safe-area margins and normal list scrolling.
The battle keeps deployment slots, shop and hand on screen, uses compact player
profiles and a PLAYERS button to expand standings. Desktop layout is unchanged.

Run `tests/mobile_layout.tscn` with `RIFT_MOBILE_LAYOUT_TEST=1` and a landscape
window matching the device aspect to check the mobile presentation on desktop.
