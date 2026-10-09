# Production Logs and Evidence

`Assets/ThiefAndTheNecklace/Scripts/Logging` defines a structured, privacy-safe logging contract. It is not wired to a persistent file, console, or cloud sink yet. The caller must select an approved sink, access controls, retention window, export/deletion workflow, and operational monitoring before production use.

## Directory convention

```text
Logs/
  Builds/ QA/ Multiplayer/ Security/ Scenario/ Performance/
  Accessibility/ Progression/ Release/ Production/ Incidents/
```

Store these outside the runtime content bundle in the team's approved evidence system. Restrict security, audit, incident, and player support records to least-privilege access. Keep build/source identifiers with every test record.

## Never log by default

Thief identity before reveal, private role/clue/situation, full world truth, raw chat, passwords, access/refresh tokens, request/response bodies, or unnecessary player/account identifiers. Do not put secrets in exception messages or stack traces. Use fixed event codes and safe result codes; the logger API intentionally has no free-form message field.

Production excludes TRACE/DEBUG through `ProductionLogger`. Debug detail requires an explicit non-production environment. Sink failures must be observable through `SinkFailureCount` and independent health monitoring; logging failure must not interrupt a match.

The logger's fixed schema is not a persistence or compliance system. The backend/provider must supply authenticated transport or protected storage, redaction, access audit, retention/deletion, and incident handling.
