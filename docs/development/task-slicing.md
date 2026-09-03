# Context-efficient task slicing

This repository adapts the `to-tickets` idea into a Squad-Up-specific workflow.
The goal is to reduce context consumption without sacrificing architecture,
security, failure-path coverage, or repository verification.

A skill does not reduce context by itself. The savings come from planning once,
executing one small ticket per fresh session, loading only relevant evidence,
and storing durable state in the repository.

## When to split

| Request shape | Action |
| --- | --- |
| One bounded context and one verifiable outcome | Keep one ticket |
| Several independent outcomes in one context | Split by observable behavior |
| Several bounded contexts | Split by owner and contract dependency |
| Cross-context contract change | Isolate contract compatibility before consumers |
| Atomic security, transaction, or external-effect boundary | Keep the boundary and its failure tests together |

Use the repository skill `$squad-up-to-tickets` for broad requests, roadmap
items, specifications, or an explicit request to create tickets. A split map
contains two to five tickets; a task that does not need splitting may produce a
single ticket.

## Workflow

1. Create the ticket map once. Order it so architecture, ownership, and security
   uncertainties are resolved before dependent implementation.
2. Start a fresh Codex session for one ticket. Do not preload later tickets.
3. Verify `docs/session-handoff.md` against Git, then read the exact `plan.md`
   section and only the relevant ADRs, threat-model entries, and affected code.
4. State the outcome and, where required, the business invariant, trust
   boundary, failure paths, and assumptions before editing.
5. Implement the smallest vertical slice and use focused tests during the edit
   loop, including negative and failure cases.
6. Review the final diff and run the complete repository gates once before the
   milestone commit and push.
7. Push a separate handoff commit containing the outcome, verification, known
   limits, and the exact next ticket prompt. The next ticket starts in another
   fresh session.

The full repository gates remain mandatory. This workflow removes repeated
broad exploration and noisy output; it does not weaken validation.

## Ticket template

```text
Outcome:
Bounded context:
Business invariant / trust boundary / failure paths / assumptions:
In scope and likely files:
Out of scope:
Acceptance evidence and focused tests:
Dependencies:
Done condition:
Recommended current model and effort:
Fresh-session prompt:
```

Authentication, authorization, concurrency, messaging, migrations, and
external effects require their safety evidence in the same ticket as the
behavior. Examples include negative authorization tests, an outbox write in the
same database transaction, duplicate/reorder/crash-window tests, migration SQL
and compatibility review, or idempotency and reconciliation for an ambiguous
HTTP result.

## Context budget

- Prefer `rg`, narrow file ranges, focused diffs, and test filters while
  investigating.
- Exclude `.claude/`, `bin/`, `obj/`, generated outputs, and unrelated user
  changes from searches and reviews.
- Avoid loading whole plans, handoffs, ADR collections, or long build logs when
  a heading, range, or summarized result answers the question.
- Load only the skill needed for the current job. More always-on instructions
  consume context and can conflict with each other.
- Use a balanced current Codex model with medium reasoning for routine bounded
  slices. Raise reasoning for novel architecture or authentication,
  authorization, concurrency, messaging, migration, and external-effect work.
  Check the current catalog rather than persisting model names that may expire.

## Current example: Fase 2 item 8

The plan entry `Secret redaction e audit logs` crosses a shared logging concern
and two business owners. Execute it as three milestones:

1. Define and prove centralized secret-redaction behavior with canary values and
   failure-path tests, without adding business audit events.
2. Add structured audit events for Profile mutations, preserving Profile's
   ownership, authorization, antiforgery, and data-classification boundaries.
3. Add structured audit events for privileged Identity actions, including
   authorization negatives and proof that Restricted values cannot enter logs.

Each milestone gets its own fresh session, focused tests, complete gates,
functional commit and push, followed by a separate handoff commit and push.
