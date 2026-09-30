# Architecture Decision Records

One file per irreversible or costly decision: `NNN-title.md` with **Context**, **Options**,
**Decision** and **Consequences**. Written by the human, in a few paragraphs — not generated as a
report.

Use an ADR when a choice is hard to reverse or future-you needs the *why* (token strategy, schema,
real-time transport, deployment). For everyday changes, update the relevant [spec](../specs/) and
the code instead.

To backfill: the opaque 30-day sliding token sessions (vs JWT) and the SQLite domain model / EF
outbox shape are good first candidates.
