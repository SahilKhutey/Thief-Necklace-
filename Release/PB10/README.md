# PB10 Live Operations Records

PB10 is a post-launch operating model, not authorization to turn on live services. PB9 is currently NO-GO, so these records describe the safe content workflow to complete after a real staging/backend environment exists.

- [Live Operations Policy](Live-Operations-Policy.md) — risk classification, content lifecycle, pinning, rollout, experiments, and ranked gate.
- [Content Change Record](Content-Change-Record.md) — one auditable request per proposed content/config update.

The code foundation is in `Assets/ThiefAndTheNecklace/Scripts/Live`. It validates and pins content locally, but does not publish packages, canary a cohort, or update a live server. Never use it as the sole validation or authority boundary for a production service.
