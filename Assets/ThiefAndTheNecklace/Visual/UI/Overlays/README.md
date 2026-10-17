# PV2 overlays

Chat, voting, vote-locked, vote-resolution, settings, error, connection, and exit-confirmation overlays live on a layer independent of the current screen. The voting controller omits the local player as a candidate and keeps submission disabled until an eligible target is selected. The core runtime must confirm acceptance through `ScreenManager.ConfirmVoteLocked()` before the vote-locked overlay appears. The core adapter presents first-tie/revote and second-tie/escape resolution messages from the authoritative vote result.

Chat text is length-checked at the presentation boundary and emitted as an intent; the application still applies its chat policy. Bind public chat messages with `BindChatMessages`.
