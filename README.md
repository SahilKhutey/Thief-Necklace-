# Thief & the Necklace

A Unity social-deduction card game prototype about reconstructing a royal mystery through private clues, free-form discussion, bluffing, voting, and reveal.

> **Project status: development prototype — not a release candidate.** The local pass-and-play flow and several architecture foundations are in the repository. Online multiplayer, authentication, matchmaking, cloud profiles/rewards, production analytics, release operations, and live content publishing are not connected. Unity compilation and playability have not been verified in this authoring environment.

## The game

Each round gives players different roles and private information about an incident. One player is secretly the Thief. The group compares accounts, asks questions, identifies inconsistencies, votes, and sees the truth and score.

**Round loop:** Scenario → secret role and situation → discussion → vote/runoff → reveal → score.

The design goal is to reward building a coherent story from overlapping evidence, not selecting the most suspicious-looking role.

## Current implementation

- Unity 6.3 project metadata (`6000.3.0f1`); Package Manager lists Unity Test Framework.
- Offline four-player pass-and-play prototype with authored UI/runtime flow.
- Seeded scenario generator and scenario validator for 4–6 players, with authored mystery templates.
- PB2 networking/authority contracts, PB8 backend/security service seams, and PB10 live-content operations model. These are foundations, not connected production services.
- Local progression/profile foundations.
- Release evidence checklist and privacy-safe structured logging contract.

See [Docs/PROJECT_STATUS.md](Docs/PROJECT_STATUS.md) for the verified scope and blockers. Do not treat PB1–PB10 labels as claims that online production milestones passed.

## Open in Unity

1. Install Unity **6000.3.0f1** or a compatible Unity 6.3 LTS patch.
2. Open this repository folder in Unity Hub and allow packages to resolve.
3. Review the Console for import/compiler errors. The editor initialization flow may create the Bootstrap scene and default scenario assets on first open.
4. Open the generated Bootstrap scene and try **Play Local · 4 Players** if initialization completes.
5. Use **Window → General → Test Runner** to run Edit Mode tests before accepting any candidate build.

The steps above are setup guidance, not evidence that this checkout has compiled or run successfully. The Unity Editor was unavailable when these files were prepared. Read [Docs/BUILDING.md](Docs/BUILDING.md) before producing a build.

## Documentation

- [Documentation index](Docs/README.md)
- [Project status and release blockers](Docs/PROJECT_STATUS.md)
- [Architecture and trust boundaries](Docs/ARCHITECTURE.md)
- [Scenario/content workflow](Docs/CONTENT_WORKFLOW.md)
- [Build and validation](Docs/BUILDING.md)
- [Contribution workflow](Docs/CONTRIBUTING.md)
- [Release and tag policy](Docs/RELEASES_AND_TAGS.md)
- [Security reporting](SECURITY.md)
- [PB9 release evidence pack](Release/PB9/README.md)
- [PB10 live operations foundation](PB10-Implementation-Notes.md)
- [Final checklist and logging](Production/Finalization/README.md)

Design source documents remain at the repository root (`P1`–`P9`) alongside the original game design document PDF.

## Repository layout

```text
Assets/ThiefAndTheNecklace/Scripts/Core       Game rules and scenario generation
Assets/ThiefAndTheNecklace/Scripts/UI         Offline UI and presentation hooks
Assets/ThiefAndTheNecklace/Scripts/Networking Engine-independent network contracts
Assets/ThiefAndTheNecklace/Scripts/Backend    Backend service contracts and guardrails
Assets/ThiefAndTheNecklace/Scripts/Live       Content operations and match pins
Assets/ThiefAndTheNecklace/Scripts/Logging    Privacy-safe structured logging contract
Assets/ThiefAndTheNecklace/Tests               Edit Mode test sources (not yet run here)
Docs/                                          Setup, architecture, content, release docs
Release/                                       RC/beta/operations evidence templates
Production/                                    Final checklist and logging templates
Packages/                                      Unity package manifest
ProjectSettings/                               Unity project settings
```

## License

The project uses a custom **proprietary, commercial permission required** license. See [LICENSE.md](LICENSE.md). It is not an open-source license. Third-party packages and materials remain subject to their own licenses. Because this repository is public, GitHub platform terms separately allow viewing and forking; make the repository private if you need to restrict source access beyond that platform behavior.
