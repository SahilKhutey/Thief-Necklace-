# PB9 Player Support Playbook

## Triage

1. Acknowledge the report and confirm the player's platform, app version/build, approximate UTC time, and whether a match ID exists.
2. Categorize: account/access, matchmaking, disconnect, vote/result, reward/profile, content/fairness, accessibility, abuse/safety, or crash/performance.
3. Ask only for the minimum diagnostic detail needed. Never ask for passwords, auth tokens, private clues, or raw chat exports.
4. Check candidate, match/round, scenario/version, phase, and redacted service result codes. Limit access to identity-linked reports.
5. Escalate suspected secret exposure, unauthorized access, forged results/rewards, or player safety reports immediately through the approved internal route.
6. Close the loop with a plain explanation grounded in the published rules; link an issue ID where possible.

## Match result investigation

Use match ID, round ID, scenario ID/version, seed, config/build version, final ballot, authoritative accused identity, and score calculation. Keep private roles/clues restricted to authorized reviewers and reveal them only when needed to resolve the case. Do not expose another player's personal data in the response.

## Service incident response

Classify severity, identify affected build/platform/region, preserve server-side match and reward integrity, contain with a verified feature/config disable or rollback, confirm recovery with a fresh session, notify affected players through the approved channel, and create a postmortem with actions and owners.

## Current readiness

This is a template only. There is no staffed support rota, live service, or approved player contact channel in the current project state. Do not publish this playbook as a working support promise until an owner and route are assigned.
