## Objective

<!-- What verifiable outcome does this PR deliver? Link the issue with Closes #123. -->

Closes #

## Context and decision

<!-- Why is this change needed? Which ADRs, invariants, or threat-model entries apply? -->

## Scope

### Included

-

### Explicitly out of scope

-

## Failure and risk review

<!-- Describe relevant negative paths: authorization, concurrency, duplicate/reordered messages, partial failure, timeout ambiguity, migration compatibility, or external effects. Use N/A only with a reason. -->

- Failure paths considered:
- Residual risk:
- Rollback or recovery:

## Verification evidence

<!-- Paste commands and concise results. Never paste secrets, tokens, production payloads, or unsanitized logs. -->

```text
command
result
```

## Acceptance criteria

- [ ] The issue's executable acceptance criteria pass.
- [ ] Focused positive, negative, and failure-path tests were added or updated.
- [ ] Repository formatting, build, and test gates pass.
- [ ] Checks not run are listed with a reason.

## Review checklist

- [ ] The diff is a small, coherent outcome without unrelated cleanup.
- [ ] Dependency direction and bounded-context data ownership are preserved.
- [ ] Authentication and authorization are tested independently where affected.
- [ ] New data follows the classification, minimization, redaction, retention,
      and deletion rules.
- [ ] Message changes address versioning, idempotency, ordering, retry, outbox,
      and DLQ behavior where applicable.
- [ ] Database changes are expand-safe; generated SQL and compatibility were
      reviewed where applicable.
- [ ] Logs, traces, metrics, health behavior, and runbooks were updated where
      operational behavior changed.
- [ ] No secret or Restricted data appears in the diff or attached evidence.
- [ ] Documentation, ADR, API, or contract fixtures were updated where behavior
      or an accepted decision changed.
