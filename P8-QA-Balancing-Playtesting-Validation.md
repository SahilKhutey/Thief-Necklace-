# Thief & the Necklace

## P8 — QA, Balancing, Playtesting & Validation Specification

**Status:** Production QA / validation architecture  
**Parent:** P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7  
**Target:** Unity 6 LTS  
**MVP:** 4–6 players  
**Primary goal:** Prove that the integrated game is correct, fair, secure, understandable, stable, performant, and fun to play.

The source GDD calls for unit and multiplayer testing, platform checks, poor-network testing, beta feedback, regression, and testing of disconnect/vote edge cases. P8 turns those recommendations into a validation and release framework. This is a plan; it does not claim that tests have already run or passed.

## 1. Quality dimensions and gates

Quality includes correctness, fairness, security, usability, performance, stability, and fun. A technically correct social deduction game can still fail if its mysteries are obvious, impossible, or dull.

Release-candidate progression:

1. Code/unit correctness.
2. Game-rule and state-machine correctness.
3. Scenario and clue validation.
4. Multiplayer and disconnect validation.
5. Private-information security.
6. UI/UX, accessibility, animation, and audio checks.
7. Performance and platform compatibility.
8. Balance simulations and human playtests.
9. Regression and clean release-build checks.

Do not advance with open critical or core-gameplay blockers. The QA pyramid is unit tests → integration/system tests → human playtests: lower layers catch cheap defects; people catch problems code cannot identify.

## 2. Test suite categories

- Unit: role assignment, seeded RNG, scenario/timeline/clue generation, validators, vote and score resolution, XP/profile logic.
- Integration: phase machine, session, private delivery, chat, reveal, score commit, persistence.
- Scenario/content validation.
- Multiplayer and fault injection.
- Privacy/security and exploit checks.
- UI/UX, responsive, accessibility, animation, and audio feedback.
- Performance, compatibility, soak, and release build.
- Human playtests and regression.

## 3. Unit and deterministic generation checks

### Role assignment

- Valid role set for 4, 5, and 6 players.
- Exactly one Thief objective, independent from role identity.
- Every active player receives exactly one normal role and correct configured value.
- No incompatible or prohibited duplicate roles.
- Different seeds can vary outcomes; the same seed and content/generator version reproduce the same assignment.

### Seed distribution

Run a large sample (for example, 100,000 generated rounds) and compare each eligible player's Thief frequency with expected probability `1 / playerCount`. Six players therefore approach 16.67% over a sufficiently large sample. Finite samples naturally vary; establish statistical tolerance before treating deviations as a defect. Never force rotation in normal play to conceal a broken RNG.

### Determinism

Given the same scenario version, generator version, player roster/order, difficulty, and seed, roles, Thief, timeline, clues, distribution, and validation outcome must reproduce. Keep difficult seed fixtures as regression tests.

## 4. Scenario, timeline, and clue validation

Every generated candidate must pass P2 rules:

- Exactly one Thief.
- Valid location/object/role references and compatible role set.
- Ordered timeline and valid dependencies.
- Reachable theft location and object; valid access, opportunity, escape, then discovery.
- Every clue has a valid owner, source event, actor/location/object reference, type, and resolvable template.
- Innocent information is consistent with world truth; observations are possible; partial information is not mislabeled as an objective lie.
- Critical-event evidence coverage meets configured requirements.
- No accidental direct reveal; plausible innocent alternative exists; Thief has a playable defense; innocents have a reasoning path.
- No clue distribution exposes another player's private data.

Test known-invalid timelines, such as theft occurring before the Thief enters the location, and require rejection. A direct reveal and an impossible innocent clue are content defects, even if the round can technically run.

### Mass generation

Run at least 10,000 candidate rounds for balance/validator evaluation once a generator exists. Track validation failure rate, direct-reveal rate, impossible-timeline rate, insufficient-evidence rate, duplicate-clue rate, over-obvious rate, no-plausible-suspect rate, excessive-ambiguity rate, and fallback rate. Target **invalid generated rounds entering gameplay = zero**. Set acceptable candidate-rejection/fallback rates after measurement; do not confuse rejected candidates with invalid rounds reaching players.

## 5. Privacy and information-security validation

Test actual client payloads, serialized/network objects, and accessible runtime state—not only whether the UI hides an element.

Before reveal, each client must not receive another player's role, Thief flag, situation, clues, full timeline, full world truth, unrevealed ballots, or authority-only score calculation. Inspect network messages and client object/state graphs. After reveal, only data authorized by the reveal result becomes public.

Example: if P3 is the Thief, P3 sees their own objective; P1/P2/P4 see no P3 role/objective before reveal and see it after the authority's reveal event. Repeat the same test for every private clue owner.

Also attempt remote client exploits:

- Set Thief or another player's identity.
- Set score/XP or submit a fabricated result.
- Force reveal, skip phase, or alter a deadline.
- Vote as another player, self-vote, vote twice, or vote after close.
- Submit a fake vote count or another player's clue.
- Replay duplicate commands or score events.

No such client command may mutate authority-owned state. Document the host-trust limitation: a malicious host can inspect host-authoritative state; P8's remote-client tests do not claim to solve malicious-host cheating.

## 6. Voting, scoring, and progression tests

### Voting

- Valid target, one accepted vote per eligible voter, no self-vote.
- Invalid target, wrong phase, expired vote, disconnected voter, forged sender, duplicate request, and out-of-order events are rejected or handled by the P4 policy.
- Pre-resolution anonymity is preserved.
- Authority vote count equals accepted ballots, except explicit abstentions/disconnect policy.
- Test plurality distributions: 3–2–1, 3–3–0, 2–2–2, and all distinct votes.
- Runoff permits only tied top candidates; voter cannot self-vote; runoff timeout/no valid target follows P1.
- A second tie means Thief escapes; no third ballot.

### Score and match result

- Catch: each correct voter in final deciding ballot receives their role value; incorrect voters and Thief receive 0.
- Escape: Thief receives sum of innocent role values; innocents receive 0.
- No negative points, talking bonuses, or Thief base-role bonus.
- Runoff uses final deciding ballot for correct-voter reward.
- A round result is applied exactly once; duplicates are idempotent.
- Match scores persist across rounds, reset on new match, and determine five-round ranking.
- Match tie tiebreak: correct Thief detections, then successful escapes while assigned Thief, then shared placement.

### XP/profile

- Configured round/match XP is granted once and remains separate from match score.
- Aborted/disconnected match follows configured completion reward policy.
- Level thresholds, stats, local profile save/load, and safe handling of missing/corrupt/old-version data.
- Client cannot directly change authoritative online results or claim achievements/unlocks.

## 7. State machine, timers, and chat

Exercise the full path:

```text
Lobby → Intro → Private Reveal → Discussion → Voting → Resolution
      → Reveal → Scoring → Round End → Next Round → Match End
```

Test invalid transitions (Lobby→Reveal, Voting→Discussion, Scoring→Voting) and require rejection. Verify no deadlock across five rounds. Test timer boundaries at 01:00, 00:30, 00:10, 00:01, and 00:00; warning/expired events occur once. Clients with different local clocks must use the same authoritative network deadline.

Chat checks: available only in configured phases, empty messages rejected, max length and rate limits enforced, ordering preserved, UI scroll works, and input remains usable after recoverable rejection. Stress at 10/25/50 messages per second and measure FPS, memory, UI latency, loss, and ordering.

## 8. Multiplayer and disconnect matrix

Run the entire match flow with 4, 5, and 6 clients: create, join, ready, start, generate, privately distribute, discuss, vote, reveal, score, continue through five rounds, and finish.

Test disconnect/reconnect in lobby, intro, private reveal, discussion, vote, runoff, reveal, scoring, and match end. Include Thief disconnect, ordinary player disconnect, host exit, Relay/session loss, reconnect, and join-after-start.

Expected MVP policy from P4:

- Lobby departure updates roster/readiness.
- Late join after match start is rejected.
- Missing vote is abstention; accepted vote remains recorded.
- Thief disconnect before resolution aborts round with no score and replacement round after recovery/confirmation.
- If fewer than four connected players remain, abort the round with no score.
- Host loss invalidates the current round, awards no score, recreates/recover session, and starts a fresh round with a new authority/seed. No seamless migration is claimed.

Each case needs an explicit expected outcome; no client may silently create a different truth model.

## 9. Network condition and idempotency tests

Simulate latency at 50, 100, 200, 500, and 1000 ms and packet loss at 0%, 1%, 5%, and 10%. Check chat order, vote acknowledgements, deadline display, reveal synchronization, scoring, and recovery. Repeat command IDs; only one vote/score transaction may take effect. Delay or reorder `PhaseChanged` and `VoteAccepted` events; stale versions must not overwrite newer state.

## 10. UI, responsive, accessibility, and animation QA

Test Main Menu, Create/Join, Lobby, Scenario Intro, Private Role, Game Table, Voting, Vote Resolution, Reveal, Round Results, Match Results, Profile, and Settings for content, alignment, interaction, disabled/loading/error state, responsiveness, and accessible feedback.

Representative sizes: 1280×720, 1920×1080, 1024×768, 800×600, 390×844, 412×915, and 667×375; also resize the WebGL viewport. Test long names, long clues, large text, safe areas, orientation, touch targets, keyboard navigation, and color-independent status.

Test repeated/rapid taps on Ready, Create, Join, Vote Submit, and Rematch. No duplicate rooms, votes, score transactions, or rematches. Interrupt card flip/reveal with a network phase change; UI must recover without frozen controls or duplicate reveal. Animations never block authoritative state, alter rules, or run indefinitely. Verify Full/Reduced/Minimal motion modes and ensure no Thief-only art/audio leaks before reveal.

## 11. Art, audio, and localization readiness

Review at actual game scale. Verify card readability, correct emblem/value, common hidden card back, neutral Thief-role art, safe crops, assets load correctly, and effects do not obscure text or expose hidden state. Test audio mix/mute and confirm secret information has no premature audio cue.

Even if MVP ships English-only, test long names, titles, and clues at larger font sizes. Keep text externalized; no gameplay-critical text should be baked into art.

## 12. Performance, compatibility, and soak testing

Measure CPU, GPU, memory, GC allocations, loading time, frame rate, and network throughput on target hardware/builds. Baseline target: 60 FPS where hardware supports it; 30 FPS is an acceptable low-end fallback. Large drops in this UI-focused game need investigation.

Monitor memory during startup, lobby, scenario load, reveal, after five rounds, and after 20–50 consecutive rounds/matches. Look for texture duplication, un-released Addressables, prefab/chat leaks, state leakage, asset unload failures, and session cleanup issues. Stress six players, 50+ chat messages, repeated voting, round transitions, and scenario loads.

Test supported desktop browsers/WebGL and real Android/iOS devices. Include load, resize, fullscreen, tab switching/refresh policy, reconnect, orientation, app background/resume, Wi-Fi-to-mobile-data switching, thermal behavior, and memory limits. Editor-only success is not a substitute for device builds.

## 13. Balance and human playtesting

Do not set a hard detection-rate target before collecting baseline results. Compare Easy/Normal/Hard across detection rate, Thief escape rate, round length, vote concentration, player confidence, and scenario quality. For six players with self-voting disabled, random choice among five candidates is about 20%; detection near that baseline over repeated Normal tests suggests weak evidence, but individual rounds can vary.

Review vote distributions (1–1–1–1–1–1 through 5–1), time to first credible accusation, first contradiction, final vote, and meaningful evidence chains. Use these as diagnostics, not rigid per-round quotas. Flag rounds where everyone immediately knows the Thief or nobody can form a reasoned suspect.

After each playtest, ask players to rate 1–5:

- I understood my role and objective.
- I had useful information.
- I could form a reasoned suspect.
- I could defend myself as Thief.
- The vote felt fair.
- The reveal made sense after the clues were shown.
- I would play another round.

Observe whether players ask questions, cite evidence, compare claims, change theories, and bluff. Warning signs include random votes, silence, one player dominating discussion, obvious Thief early, ignored clues, arbitrary voting, unfair-feeling reveal, or Thief players reporting no agency. Do not add forced turn-taking based only on one session; diagnose and retest.

For Thief agency, ask “Did you have a believable story you could defend?” For innocent playability, ask “Did your information let you form a reasoned suspect?” A satisfying reveal produces “Now I understand,” not “That came out of nowhere.”

## 14. Balance iteration and telemetry

Change one major tuning variable at a time (clue density, timers, Thief reward, role value) and record version, change, reason, and observed result. Track scenario validity, detection/escape rates, average round length, vote concentration, contradiction/evidence-chain indicators, role performance, chat activity, disconnect/crash rate, FPS, memory growth, and private-data leak rate.

Telemetry should be aggregate and purpose-limited. Do not capture raw chat or private clues for balancing by default. Record scenario seed, content/generator version, player count, phase timing, votes/outcome as permitted by privacy policy, and build/platform metadata only as needed for diagnosis.

## 15. Defect severity and report template

Suggested severity:

- **P0 — Blocker:** data loss, widespread crash, exploitable secret leak, or release impossible.
- **P1 — Critical:** core match/score/privacy defect with no reasonable workaround.
- **P2 — Major:** important defect with a workaround.
- **P3 — Minor:** usability or visual issue with limited impact.
- **P4 — Cosmetic:** small polish issue.

Bug report fields:

```text
Bug ID / Severity / Build / Platform / Player count
Scenario ID + seed / Phase / Network conditions
Steps to reproduce / Expected / Actual / Screenshot-log-recording
```

Seed and content/generator versions are especially important for generated-round bugs.

## 16. Automation, CI, and regression

Suggested test groups:

```text
Tests/
├── EditMode/       Roles, Scenario, Timeline, Clues, Voting, Scoring, Progression
├── PlayMode/       GameFlow, UI, Reveal, Scene
├── Multiplayer/    Connection, PrivateState, Voting, Recovery
└── Performance/    Load, Memory, Soak
```

CI pipeline: clean checkout → compile/static checks → unit tests → scenario batch validation → integration tests → build → smoke test → artifact. Scenario/content changes should run the validator and report metrics; fail if invalid rounds can enter gameplay. Store deterministic known-bad seeds as regression fixtures. Content changes must not break existing scenario references, roles, art mappings, clue templates, or difficulty configuration.

Screenshot regression may compare representative Main Menu, Lobby, Role Card, Vote, Reveal, and Results screens at selected resolutions. Release builds must be tested from a clean environment with no hidden local files or editor-only dependencies.

## 17. Release checklist and gates

### Beta readiness

- No open P0 defects and no known critical privacy leak.
- Complete 4–6 player core loop and five-round match.
- Scenarios validate before play; score and tie rules are stable.
- Disconnect/host-loss behavior is defined and exercised.
- Major UI flows work on target builds.
- Playtesters understand objective, Thief has agency, and innocents have useful evidence.

### Release candidate

- Clean build and smoke test pass.
- Unit/integration/network/security/scenario suite passes.
- Invalid generated rounds entering gameplay = zero.
- No P0 or P1 core gameplay/privacy issues open.
- 4/5/6-player matrix passes.
- Voting, scoring, persistence, and duplicate handling pass.
- Mobile/WebGL platform checks and performance pass.
- Human playtest review is acceptable and documented.
- Debug commands, hidden Thief display, test seed overrides, and developer shortcuts are removed or safely gated.

## 18. End-to-end P8 definition of done

The full validation run completes this path with six players (and separately with four and five):

```text
Create room → join → ready → seeded round → private delivery
→ discussion → vote → tie/no-tie resolution → reveal → score
→ next round × 5 → match winner → profile XP → rematch
```

Across normal play, bad networks, disconnects, repeated rounds, supported devices, and real human behavior, the build must maintain no critical bug, no secret leak, no score corruption, no state desync, and no invalid scenario entering gameplay.

## 19. P8 final quality lock

The game is not finished when each feature works alone. It is ready when the features work together through normal play, abnormal play, poor networks, repeated rounds, different devices, and real human behavior.

P8 closes the design/architecture sequence P0–P8. The next practical phase is implementation: Unity project/package setup, data assets and ScriptableObjects, C# gameplay and network systems, scenario content, UI prefabs, art integration, tests, and a four-player vertical slice before expanding to the full 4–6 player build.
