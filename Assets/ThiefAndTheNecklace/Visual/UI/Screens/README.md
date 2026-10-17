# PV2 screen templates

The production layer includes Main Menu, Profile, Create Room, Join Room, Lobby, Scenario Intro, Private Role, Game, Reveal, Round Results, Match Results, Settings, How To Play, Exit Confirmation, Error, Reconnect, and Disconnected UXML pages. Each uses the shared page/background/content/overlay hierarchy and design-token styles.

`ScreenManager` owns active-page navigation and brief fades; `VisualOverlayManager` keeps chat, voting, vote-locked, settings, error, connection, and exit-confirmation overlays separate. `PageNavigationView` controls multi-page tutorials. Screen data binding accepts public view models, while private-role data is explicitly limited to its private screen and reveal data requires `AuthorizedRevealViewModel`.

Use the PV2 setup commands documented in the parent [UI README](../README.md) to attach the pages to a UIDocument or generate the visual traversal scene.
