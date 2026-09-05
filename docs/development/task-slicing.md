# Context-efficient task slicing

This repository adapts the `to-tickets` idea into a Squad-Up-specific workflow.
The goal is to reduce context consumption without sacrificing architecture,
security, failure-path coverage, or repository verification.

A skill does not reduce context by itself. The savings come from planning once,
executing one small ticket per fresh session, loading only relevant evidence,
and storing durable state in the repository. The durable entry point is the
[implementation catalog](../implementation-catalog.md), not a full reread of
`plan.md`.

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
3. Verify `docs/session-handoff.md` against Git, then locate the exact ID in
   the implementation catalog. Read only its listed ADRs, threat-model entries,
   and affected code. Open the cited `plan.md` anchor only when the catalog says
   it is needed for a design decision; never preload the whole plan.
4. State the outcome and, where required, the business invariant, trust
   boundary, failure paths, and assumptions before editing.
5. Implement the smallest vertical slice and use focused tests during the edit
   loop, including negative and failure cases.
6. Review the final diff and apply the verification policy in `AGENTS.md`:
   documentation-only checks for prose changes; complete repository gates once
   for code, dependencies, executable configuration or mixed changes.
7. Push a separate handoff commit containing the outcome, verification, known
   limits, and the exact next ticket prompt. The next ticket starts in another
   fresh session.

The user approved a documentation-only exception on 2026-09-05. Its checks are
diff whitespace, changed links/anchors, accuracy and instruction consistency;
also verify handoff limits/history when affected. Full gates remain mandatory
outside that exception; security and required CI checks are unchanged.

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

- For an atomic ticket, begin with the handoff, its catalog block, and the
  affected code. Read an ADR, threat-model entry, plan anchor, or skill only
  when that ticket's boundary requires it; use a heading or exact range, never
  a broad dump.
- Prefer `rg`, narrow file ranges, focused diffs, and test filters while
  investigating.
- Exclude agent worktrees, `bin/`, `obj/`, generated outputs, and unrelated user
  changes from searches and reviews.
- Avoid loading whole plans, handoffs, ADR collections, or long build logs when
  a heading, range, or summarized result answers the question.
- Load only the skill needed for the current job. More always-on instructions
  consume context and can conflict with each other.
- Use a balanced current Codex model with medium reasoning for routine bounded
  slices. Raise reasoning for unresolved architectural or correctness questions;
  required security evidence does not depend on model effort. Reuse dated
  recommendations while applicable; refresh when stale or uncertain.

## Cost controls and evidence (DEV-WF-01)

The 2026-09-05 audit found a 308-line/24,249-byte handoff accumulating old
milestones in Current state, including contradictory completion/test claims.
The ten inspected handoff changes added more lines than they removed.
This proves growing startup material, not its share of subscription usage.
The user reports using Terra/medium for almost every roadmap execution, so
switching to that configuration is not a new intervention. Historical model
traces, Fast mode, tool/reasoning usage and quota deltas have not been measured.
The active handoff now has a 60-line/4-KB ceiling; historical evidence is linked
but excluded from routine startup. Keep task-specific detail in its catalog
block. Do not copy finished ticket narratives back into Current state.

- Reuse the existing ticket and acceptance criteria. Split by independently
  useful outcome, never merely to reduce file count. A smaller ticket still
  pays startup, gates, review and two-commit handoff costs.
- Read each relevant source once, then follow the diff. Search narrowly before
  opening more files. Do not rerun planning or research resolved questions.
- Use one agent by default. Delegate only when explicitly requested and when
  independent work justifies duplicated context. Parallel shell reads do not
  need additional agents.
- After a failed check, inspect its relevant failure once and change the
  hypothesis before retrying. Run the applicable checks once at the final state;
  repeat only when subsequent changes or unresolved failures require it.
  Poll long jobs at useful intervals and summarize results; elapsed test time
  alone does not establish model token consumption.
- Mechanical documentation can use low effort; prefer standard speed when
  conserving usage. Keep final reports to outcome, evidence, limits and next step.

Selection reference checked 2026-09-05: GPT-5.6 Terra/medium for ordinary
implementation, Luna/low for well-specified mechanical work; Gemini 3.8 Flash
at its default effort is an alternative if available in the user's client.
These are recommendations, not measured comparisons or automatic model changes.
Refresh when stale or uncertain, not on every ticket. Official references:
[OpenAI models](https://learn.chatgpt.com/docs/models),
[usage guidance](https://learn.chatgpt.com/docs/pricing), and
[Gemini catalog](https://ai.google.dev/gemini-api/docs/models).

For the next three comparable tickets, record one optional row per ticket:

| Ticket / model / effort / speed | 5h used before→after | Weekly used before→after | Retries / result |
| --- | --- | --- | --- |
| No measurements yet | Unknown | Unknown | Do not infer savings |

Use account-visible counters when available, without credentials or raw session
transcripts. Mark quota resets, other simultaneous usage and unavailable data;
do not attribute those deltas solely to the ticket. Compare percentage-point
deltas separately for each window, alongside defects and rework. Byte/line
reduction measures startup material, not billed tokens or quota savings.
Account settings and unused connectors need separate inspection if the measured
cost remains high; do not change global configuration as part of a code ticket.
