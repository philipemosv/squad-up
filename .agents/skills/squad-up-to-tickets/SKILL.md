---
name: squad-up-to-tickets
description: "Split broad Squad-Up requests, roadmap items, specifications, or plans into small vertical tickets that each fit one bounded context, produce one independently verifiable outcome, and can be completed in a fresh session. Use before implementation when work spans multiple outcomes or contexts, or when the user asks to reduce context usage, create tickets, sequence work, or adapt a to-tickets workflow."
---

# Squad Up To Tickets

Create the smallest safe ticket map; do not implement it unless the user also
asks for implementation.

## Build the map

1. Verify `docs/session-handoff.md` against Git.
2. Read only the exact `plan.md` section, ADRs, threat-model entries, and source
   files needed to identify boundaries and dependencies.
3. If the request already has one bounded context and one verifiable outcome,
   return one ticket. Otherwise create two to five ordered vertical tickets.
4. Resolve architectural or security uncertainty before dependent behavior.
5. Recommend the currently available provider/model and reasoning effort for
   each ticket as required by `AGENTS.md`.

Prefer targeted searches and file ranges. Exclude `.claude/`, `bin/`, `obj/`,
generated artifacts, broad document dumps, and full diffs unless they are
necessary. Summarize command and gate output instead of retaining it verbatim.

## Ticket contract

Each ticket must contain:

- outcome and bounded context;
- business invariant, trust boundary, failure paths, and assumptions when the
  work touches authentication, concurrency, messaging, migrations, or external
  effects;
- in-scope behavior and likely files, plus explicit exclusions;
- executable acceptance evidence, including focused negative/failure tests;
- dependencies and the exact done condition;
- a concise fresh-session prompt.

Keep security and transaction boundaries intact. Never split an atomic database
change from its outbox write, authorization from its negative tests, or an
external effect from its idempotency/reconciliation evidence. Do not create
issues, mutate trackers, commit, or push unless explicitly requested.

Execute one ticket per fresh session. During implementation, run focused tests
while iterating and the complete repository gates once before that ticket's
milestone commit and push. Then update and push `docs/session-handoff.md`
separately, following [the repository workflow](../../../docs/development/task-slicing.md).
