# Log and Evidence Templates

All times are UTC. Do not paste credentials, private clues, raw chat, or secrets into these records. Use build/revision and opaque correlation IDs. Store evidence links in the access-controlled evidence system.

## Test run

```text
Run ID:
Build / source revision:
Platform / environment:
Tester:
Start UTC / end UTC:
Test suite and version:
Result: PASS / PASS-WR / FAIL / BLOCKED / N/A / DEFERRED
Evidence location:
Related bug IDs:
Notes (redacted):
```

## Test result

```text
Test ID / category:
Description:
Preconditions:
Expected:
Actual:
Status / severity / priority:
Build / platform:
Evidence:
Owner:
```

## Bug

```text
Bug ID / title:
Severity / priority:
Build / platform / environment:
Reproduction steps:
Expected / actual:
Evidence:
Owner / status:
Fix revision:
QA verification run:
```

Bug statuses: NEW → TRIAGED → ASSIGNED → IN PROGRESS → FIXED → READY FOR QA → VERIFIED → CLOSED. Also allow REOPENED, WONT FIX (with approval), and DUPLICATE (with linked issue).

## Change approval

```text
Change ID / date / author / area:
Before / after:
Reason / risk:
Build and evidence:
Tests and result:
Designer / engineer / QA approvals:
Security / operations approval (when required):
Status:
```

## Scenario and playtest

```text
Scenario ID / version / package hash:
Author / created / modified:
Generator version / seed reference:
Players / difficulty / rounds:
Validation run and result:
Logic / evidence / Thief agency / innocent agency findings:
Clarity / fairness / suspense / replay ratings:
Issue IDs / action / owner:
Lifecycle: DRAFT / TESTING / LIVE / WATCH / RETIRED / DISABLED
```

Do not include actual thief identity, clue text, private situation, or world truth in general scenario logs.

## Performance / security / accessibility / multiplayer run

```text
Run ID / build / revision / platform / device class:
Scenario or scene / phase:
Conditions (network profile, text size, motion mode, etc.):
Metric or attack case:
Expected threshold / actual:
Result / severity:
Evidence location (redacted):
Owner / follow-up issue:
```

Security logs must avoid credential values and unnecessary target identity. Describe the test case with a case ID.

## Deployment / build

```text
Deployment/build ID:
Version / commit / tag:
Environment / platform:
Content / scenario / protocol / profile / config versions:
Artifact checksum / symbols location:
Started / completed UTC:
Regression / security / performance / content results:
Health / rollback reference:
Approvals / final result:
```

## Incident / postmortem

```text
Incident ID / severity:
Affected build / platform / region / service:
Impact and start/end UTC:
Detection / alert:
Timeline:
Containment / rollback:
Data and match integrity verified by:
Root cause:
Recovery time:
Player communication:
Preventive actions / owners / due dates:
Evidence / review date:
```

## Daily / weekly operations

```text
Date or week / build:
Active players / completed matches (with denominator):
Completion / crash / disconnect / queue / profile / reward health:
Scenario health (scenario/version/sample size):
Security / moderation incidents (aggregate only):
Changes / releases / rollback:
Risks / blockers / owners:
Next review:
```

Do not publish invented metrics. Include cohort size, time window, and source for every operational measure.
