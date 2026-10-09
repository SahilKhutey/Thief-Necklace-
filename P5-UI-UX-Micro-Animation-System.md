# Thief & the Necklace

## P5 — UI/UX, Interaction & Micro-Animation Specification

**Status:** Production design specification  
**Parent:** P0 → P1 → P2 → P3 → P4  
**Target:** Unity 6 LTS  
**Platforms:** Mobile and desktop/WebGL  
**Primary goal:** Make conversation, hidden information, voting, revelation, and scoring immediately understandable while preserving the ornate illustrated card-game identity.

P0 identifies Main Menu, Lobby, Game, Voting, and Reveal/Results as core screens. It also calls for role cards, player identities, a discussion timer, chat, selectable vote targets, vote feedback, reveal animation, and score presentation. P5 defines their complete UX and animation behavior.

## 1. UX objective and pillars

The interface connects game state, player input, visual feedback, animation, and audio. At every moment, players should understand: **What is happening? What can I do? What just happened?**

1. **Readable before decorative.** Prioritize function and hierarchy; use ornament to support them.
2. **Information is sacred.** Public scenario/player/chat data, private role/situation/clues, and authority-only truth must remain visibly and technically distinct.
3. **Conversation is the main activity.** The investigation table prioritizes players, chat, and timer.
4. **Important actions get feedback.** Input receives immediate press feedback, then clear confirmation when the authority accepts it.
5. **The reveal is the emotional peak.** Save the strongest animation and sound for the culprit reveal.

Visual hierarchy:

1. Critical game state (phase, timer, prompt).
2. Player actions (vote, ready, send, continue).
3. Supporting information (player details, chat, clues).
4. Decoration (ornamental frames, textures, flourishes).

## 2. Visual direction and information hierarchy

Retain the supplied mockups' ornate royal/fantasy illustrated presentation: carved-gold or bronze frames, parchment/card surfaces, dark translucent panels, royal insignia, illustrated portraits, and restrained decorative detail. Dense or frequently read text uses a high-legibility body font. Decorative display type is reserved for titles and dramatic reveals.

Do not communicate important state by color alone. Pair color with labels, shapes, or icons. Public scenario text, player names, and chat must never be styled in a way that implies private knowledge. The local player's role and clue area should be clearly marked **PRIVATE** and remain reviewable during discussion.

### Global game-state status

Each major screen should include a consistent phase label, scenario title where space permits, and network status when relevant. Loading, reconnecting, vote submitted, and action rejected states must be legible and actionable.

## 3. Master screen flow and screen inventory

```text
BOOT → MAIN MENU → CREATE / JOIN → LOBBY → SCENARIO INTRO
  → PRIVATE ROLE → INVESTIGATION TABLE → VOTING → RESOLUTION
  → THIEF REVEAL → ROUND SCORE → NEXT ROUND / MATCH RESULTS
```

Screens and overlays:

- Boot / service initialization
- Main Menu
- Create Room
- Join Room
- Lobby
- Scenario Intro
- Private Role and Situation
- Game Table / Discussion
- Voting Overlay
- Vote Resolution
- Reveal Overlay
- Round Results
- Match Results
- Settings
- Reusable notices, confirmation dialogs, reconnect/loading/error states

## 4. Main Menu

The title is the strongest element. Primary actions are **Play**, **Create Room**, and **Join Room**; Settings is secondary. Buttons support idle, pointer hover (desktop), pressed, focused (keyboard/controller), loading, and disabled states. A disabled action has reduced emphasis and no press motion; explain why when useful.

On press, provide a brief compression and release, then show a loading or accepted state. Prevent duplicate create/join requests while one is in progress.

## 5. Create and Join Room

### Create Room

Expose host-selectable rules supported by the match system: capacity (4–6), rounds (default five), and difficulty (default Normal). Do not imply that the host can start with fewer than four. Show validation before creation and a clear service error if the session cannot be created.

### Join Room

Use a large, high-contrast room-code field and a touch-friendly Join button. Normalize input (case and allowed characters), retain the code after a recoverable error, and show plain-language states: searching, joining, room not found, room full, match already started, and connection lost.

## 6. Lobby

The lobby shows room code, player count (for example, 4/6), player slots, readiness, host marker, connection state, and room chat. Never show roles or clues in the lobby. The Ready action changes to a clear ready state and can be toggled off until match start if the host's rule allows it.

Each player slot includes portrait/avatar, display name, connection state, ready state, and optional host marker. On join, slide/fade the new slot into place (about 250–350 ms). On ready, show a checkmark and one soft glow/pulse. On leave, fade/dim the slot and show “Player left”; avoid dramatic motion.

The Start button is host-only and disabled until the connected roster has at least four players and all required players are ready. Disabled state should explain the missing condition. During start, lock interactions and display a brief “Preparing round” state.

## 7. Scenario Intro

Present the public incident, setting, target, and relevant context with a concise narrative card. Sequence: background fade, frame draw or fade, title, incident text, Continue. Target total is 1.5–2.5 seconds of optional entrance motion; the player should be able to continue immediately or reduce motion in settings. No hidden culprit information appears here.

## 8. Private Role and clue UX

This is a privacy-critical screen. Show role identity, role point value if appropriate for current rules, objective, private situation, and clue(s). For the Thief, clearly communicate **You are the Thief** and the objective to survive the vote. Avoid dramatic color or audio that nearby people could use to infer the role in local/shared-screen play; provide a neutral privacy mode and optional tap-to-reveal.

Use separate card sections for **Your Role**, **Your Situation**, and **What You Know**. Keep card text short and readable. Players can review their role and clues from a persistent private-info button during discussion; do not require memorization. Before leaving the private screen, require an explicit **I Understand / Continue** action, with no timer pressure unless match rules explicitly set one.

Private content must be supplied only from the recipient's P4 private payload. UI presentation must never query or expose authority-only truth, other players' roles, or their clues.

## 9. Investigation Table — primary screen

The Game Table is the central play space. Desktop can use three areas: player list/portraits, scenario/context area, and chat. Mobile should use a compact header and timer, horizontally scrollable player carousel, central context area, chat, and action bar. P0/P4 define no real-time player movement; player cards are identity/status UI, not synchronized avatars.

Each public player card may show portrait, display name, connection state, host badge, and whether the player has voted (only when voting is open). During discussion, do not show role, guilt, private clues, or system-generated suspicion. A selected vote target may be highlighted only within the local voting interaction; public vote totals/individual choices remain hidden until resolution according to game rules.

### Player attention states

Support normal, local selection, disconnected, host, and voted/submitted states. A speaking indicator is reserved for a future voice-chat feature; text chat should not imply voice activity. Do not add a suspicion meter.

## 10. Chat UX

Chat is a primary activity. Messages show sender name and readable text, with modest visual distinction and consistent alignment. Incoming messages fade from 0 to 1 opacity and translate about 8 px into place over 150–220 ms. Avoid bouncing bubbles and large movement that distracts from reading.

Sending clears the input only after local validation/authority or transport acceptance; rejected messages retain editable text and show a concise error. Support scrolling, long messages, larger text, and keyboard behavior. The last message must not obscure the input or timer. Do not make notices cover chat for long.

## 11. Timer and phase feedback

Display the authoritative phase deadline from P4's synchronized network clock. Clients animate locally; the UI must not determine when a phase ends. Use normal, warning, critical, and expired presentations:

- Normal: stable, low-motion display.
- 30 seconds: one subtle pulse.
- 10 seconds: stronger but brief pulse.
- Zero: one short impact and phase transition state.

Do not pulse continuously. Respect reduced-motion settings and avoid relying on color alone.

### Early vote control

P1/P4 currently specify timer-led discussion and do not define a vote-to-end threshold. Therefore, P5 does not make **Call Vote** an active MVP control. If a future rules version enables early voting, add the control only with a defined authority-side threshold and clear confirmation; otherwise the interface must not promise an action the game state cannot accept.

## 12. Voting overlay

Show the prompt “Who is the Thief?”, eligible player targets, local selection, deadline, and Submit. Self-vote is visibly unavailable with a “YOU” label, not silently omitted. Tied candidates only are available in a runoff. The authority remains responsible for final eligibility validation.

Target states include idle, hover/focus, selected, self/disabled, submitted/locked, and unavailable. Selected state uses lift/border/checkmark plus label, not color alone. Submit is disabled until one valid target is selected. When submitted, lock the ballot, show the selected target and “Vote locked,” and display votes received (e.g. 4/6) without revealing individual choices.

Input feedback can be a 100–150 ms compression and release. Once the authority confirms acceptance, show a checkmark and lock. If rejected, keep the overlay usable and explain why; do not imply a vote was accepted before acknowledgement.

## 13. Vote resolution and tie flow

When voting closes, show that the ballot is locked, then animate public totals only as permitted by P1/P4. Animate count-up briefly and highlight the unique plurality winner. If tied at the top, show “Tie — final revote,” restrict selectable cards to tied candidates, and display the runoff deadline. Do not expose who cast each ballot before reveal. A second tie resolves as the Thief escaping; the UI should present this as an authoritative outcome, not calculate it locally.

## 14. Reveal and role explanation

Reveal is the signature dramatic moment. Suggested sequence (2–3 seconds total, skippable/reduced in accessibility settings):

1. Dim the background and reduce ambient audio.
2. Move the accused player's card to center.
3. Rotate/flip the card; swap front/back content near 90 degrees.
4. Reveal the accused role and whether they are the Thief.
5. Reveal the Necklace emblem and caught/escaped result.
6. Show all roles, then a concise timeline/clue explanation if included in this round's reveal payload.

If the accused is innocent, show “The Thief escaped,” then reveal the actual Thief. If caught, show “The Necklace has been recovered.” Do not expose the true Thief or full timeline before the authority sends `RevealResult`.

After the reveal, a “What everyone knew” recap can show each player's revealed situation/clues to explain the deduction. Keep this recap readable and allow players to advance without waiting on long text animations.

## 15. Score and match results

Round results list each player's public role, round delta, and cumulative total. Animate score deltas into totals (600–1000 ms starting target), but commit/display data from the authority. Do not make score animation completion a prerequisite for game-state progression.

Match results show final ranking and winner/shared win according to P1 rules. Provide Next Round/Rematch and Exit actions only when permitted by the session state. Avoid rank animation that hides score values or tie outcomes.

## 16. Responsive behavior and input

Support desktop, tablet, and mobile landscape first. Mobile should preserve legible chat and selectable targets; use a player carousel when necessary. If portrait orientation is supported, define a separate layout rather than shrinking the landscape design. Respect safe areas, rounded corners, browser viewport changes, and WebGL browser controls.

Use responsive anchors, layout groups, constraints, and safe-area padding rather than absolute pixel positions. Provide generous mobile touch targets. On desktop, support pointer hover and keyboard focus; on mobile, use touch-down feedback instead of hover. Route mouse, touch, keyboard, and controller actions through Unity's Input System abstraction.

Suggested test sizes from the brief: 1280×720, 1920×1080, 1024×768, 800×600, 390×844, 412×915, and 667×375, plus browser viewport resizing.

## 17. Canvas, card, and component systems

Suggested canvas layers:

```text
Canvas
├── BackgroundLayer
├── SceneLayer
├── HeaderLayer
├── PlayerLayer
├── MainContentLayer
├── ChatLayer
├── InteractionLayer
├── OverlayLayer
└── ModalLayer
```

Separate static decoration from frequently changing content to limit unnecessary Canvas rebuilds. Pool chat message, notification, vote target, and score row views where profiling shows a benefit.

Role and clue cards share a shell with title, artwork/icon, body, and optional value. Card states: face down, face up, selected, disabled, highlighted, revealing, revealed. A card flip swaps face/back content around the midpoint. Desktop hover may lift about 4 px and scale slightly; mobile uses press feedback. Avoid indefinite pulses.

Suggested reusable prefabs: primary/secondary button, player slot, role card, clue card, chat message, vote target, timer, score row, reveal card, modal, notification.

## 18. Animation and audio language

Starting timing ranges:

| Feedback | Duration |
|---|---:|
| Micro interaction | 80–180 ms |
| UI element entrance | 150–300 ms |
| Screen transition | 250–450 ms |
| Reveal sequence | 2,000–3,000 ms |
| Score presentation | 600–1,000 ms |

Use a consistent vocabulary: hover = small lift; press = small compression; selection = lift/highlight; success = pop/check; warning = brief pulse; error = short shake; new content = fade/slide; reveal = card flip; score = count/float. Keep animation subtle except the reveal.

Important events emit audio cues through an audio event layer (button press, card selected, vote locked, timer warning, reveal started, role revealed, score added, round completed). UI components should not play arbitrary audio directly. Provide volume controls and ensure critical information is not audio-only.

## 19. UI architecture and data flow

```text
GAME / NETWORK STATE
        ↓
PRESENTATION MODEL
        ↓
SCREEN CONTROLLER + VIEW
        ↓
ANIMATION / AUDIO FEEDBACK
```

Suggested controllers: `UIManager`, `MainMenuUI`, `LobbyUI`, `ScenarioIntroUI`, `PrivateRoleUI`, `GameTableUI`, `ChatUI`, `VoteUI`, `RevealUI`, `RoundResultUI`, `MatchResultUI`.

Controllers consume presentation data and phase events; they do not own game rules. For example, `PhaseChanged(Voting)` opens `VoteUI`; `VoteAccepted` locks the local UI; `RevealResult` starts the reveal presentation. State is committed before animation begins.

Use a centralized animation interface such as `IUIAnimator` for show/hide, flip, pop, shake, and transitions. The chosen implementation may use Animator, tweening, or coroutines; game logic must not wait on arbitrary animation delays to resolve a vote or advance state.

## 20. Accessibility and privacy

Provide settings for text scale, high contrast, reduced motion (Full/Reduced/Minimal), and audio levels. Use labels/icons as well as color for Ready, Selected, Warning, and Disabled. Keep chat scrollable and readable at larger text sizes. Give accessible names to interactive controls where supported by the platform.

Private information remains reviewable. In shared-screen contexts, use a neutral role reveal, tap-to-view option, and no role-specific public animation/audio before the owner confirms. The online MVP still treats roles/clues as private payloads per P4.

## 21. Notifications and errors

Use a reusable notification component for “Player left,” “Voting started,” “Vote locked,” “30 seconds remain,” and “Thief revealed.” A starting motion is a 150 ms entrance, short hold, then 200 ms fade; do not obscure chat or target selection.

Show plain-language errors rather than network codes: “The room is full,” “This match already started,” “Your vote was not accepted,” “Connection lost — reconnecting.” Preserve user input after recoverable failures.

## 22. UI performance and content rules

Profile before optimizing. Separate static and dynamic canvases, avoid unnecessary layout/string rebuilds, and pool repeated objects where justified. Test chat loads at 1, 10, 50, and 100 messages; verify scrolling, memory, and layout performance. Target 60 FPS where supported; 30 FPS is an acceptable low-end fallback. Animations must not allocate heavily or block input/state.

Use a shared UI theme asset for font tiers, spacing, corner treatments, and default motion durations. Use a consistent custom icon set in production rather than emoji. Dense clue/chat text always uses body typography, never decorative title type.

## 23. Suggested Unity folder structure

```text
Scripts/UI/
├── Core/           UIManager.cs, UITheme.cs, PresentationState.cs
├── Screens/        MainMenuUI.cs, LobbyUI.cs, PrivateRoleUI.cs,
│                   GameTableUI.cs, VoteUI.cs, RevealUI.cs, ResultUI.cs
├── Components/     PlayerSlotUI.cs, RoleCardUI.cs, ClueCardUI.cs,
│                   ChatMessageUI.cs, VoteTargetUI.cs, ScoreRowUI.cs, TimerUI.cs
├── Animation/      UIAnimator.cs, CardFlipAnimator.cs,
│                   ScoreAnimator.cs, TransitionAnimator.cs
├── Input/          UIInputRouter.cs
└── Accessibility/  AccessibilitySettings.cs
```

## 24. Validation matrix

### Functional

- Main menu navigation, create/join loading and error states.
- Lobby roster, room code, ready/start gating, join/leave updates, chat scroll.
- Private screen shows the local role/clues and remains available in discussion.
- Game table timer matches authoritative phase deadline; player/chat state updates.
- Voting prevents self-vote, locks on acceptance, rejects duplicates, and respects timeout/runoff eligibility.
- Reveal shows accused, true Thief, roles, outcome, and score values from authority data.
- Orientation/viewport changes preserve readable cards and private info.

### Animation and accessibility

- Button press, card flip, target selection, timer warning, notice entrance, score count-up, reveal sequence.
- Reduced-motion mode preserves all information and state feedback.
- Color-independent status, text scaling, high contrast, long names, and long clue wrapping.

### Layout and load

- Test specified desktop/mobile sizes and WebGL viewport resizing.
- Verify long names and clues do not clip or overlap controls.
- Test chat at 1/10/50/100 messages and profile pooling/layout behavior.

Animations never change authoritative state, permit duplicate actions, or block gameplay input while the action is pending.

## 25. P5 definition of done

P5 is ready when a player can navigate the full match loop, identify their private information, use discussion/chat comfortably, understand phase/timer state, submit and confirm a secret vote, follow tie resolution, understand the reveal and scoring, and continue/rematch across supported layouts. No screen exposes unauthorized secret information. All accepted/rejected inputs receive clear feedback, and reduced-motion/accessibility options preserve equivalent gameplay clarity.

## 26. P5 design lock and P6 handoff

The interface model is:

```text
GAME STATE → UI STATE → VISUAL + INPUT + FEEDBACK → ANIMATION → AUDIO
```

The defining player rhythm is **look → read → think → talk → question → select → confirm → wait → reveal → react**. The UI should support that rhythm without interrupting it.

P6 — Art, Card Illustration & Visual Production System should define the art bible, character and role-card designs, card backs, Necklace artwork, palace/environment art, UI ornamentation, icons, portraits, backgrounds, VFX, asset naming/resolution, 2D/2.5D approach, animation-ready layers, platform variants, and Unity asset/prefab integration pipeline.
