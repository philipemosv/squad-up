# Catálogo de execução do roadmap

Este catálogo é o ponto de entrada para implementar o roadmap. Ele evita a
releitura de [plan.md](../plan.md), que permanece a fonte de decisões
arquiteturais e justificativas duráveis.

## Uso por sessão

1. Leia o handoff e confirme-o contra o Git.
2. Localize o ID com `rg -n '^### F[0-9]+-' docs/implementation-catalog.md`.
3. Leia apenas o bloco, seus documentos em **Ler** e os arquivos afetados.
   Abra a âncora indicada de `plan.md` só quando for necessária para uma decisão;
   nunca carregue o plano inteiro.
4. Caso o bloco tenha múltiplos resultados, use `$squad-up-to-tickets`, registre
   dois a cinco IDs derivados aqui e execute um em uma sessão nova.
5. No marco, atualize estado, evidência e próximo ID; atualize o handoff em
   commit separado.

**Estados:** `Concluído` tem evidência no handoff/histórico; `Em andamento` tem
próximo ticket; `Pendente` não iniciou; `Condicional` espera o gatilho. Git e
testes prevalecem se o catálogo estiver defasado.

**Leitura transversal:** ADR-002 para dependências; threat model e classificação
de dados para identidade, dados, mensagens, logs ou efeitos externos.

## Fase 0 — decisões e skeleton

Fronteira: governança/estrutura. Dependências: nenhuma. Aceite: build e
architecture tests verdes, ADR de licença aceita e nenhum segredo no repo.
Ler: `plan.md` §16/Fase 0 apenas se necessário.

### F0-01 a F0-07 — Concluído

ADRs 000–004; threat model/classificação; solução e locks; projetos e architecture
tests; instruções/templates; CI básica; baseline AWS sem provisionar.

## Fase 1 — plataforma local e service defaults

Fronteira: hosts/infra de desenvolvimento. Dependências: F0. Aceite: trace no
Jaeger, containers non-root e dependências isoladas na CI. Ler: ADR-001,
ADR-002, ADR-004 e `plan.md` §16/Fase 1 se necessário.

### F1-01 a F1-07 — Concluído

Compose; LocalStack/Toxiproxy; ServiceDefaults; health; Dockerfiles chiseled;
Testcontainers/smoke; validação de configuração e User Secrets.

## Fase 2 — Identity, Discord OAuth e Profile

Fronteira: API/Identity/Profile. Ler: ADR-003, ADR-005, TM-01–TM-05,
classificação e `plan.md` §16/Fase 2 quando necessário. Aceite: negativos de
autorização/CSRF e isolamento entre usuários junto ao comportamento.

### F2-01 — Identity/migration Npgsql — Concluído
### F2-02 — Discord Authorization Code — Concluído
### F2-03 — upsert/link/unlink de external login — Concluído
### F2-04 — Cookie BFF/JWT interno — Concluído
### F2-05 — refresh-token rotation/reuse detection — Condicional

Gatilho: cliente bearer ou refresh token exposto; revisar retenção/revogação,
ADR-003 e TM-02 antes de iniciar.

### F2-06 — claims, roles, policies e harness — Concluído
### F2-07 — Profile, jogos/ranks e catálogo inicial — Concluído
### F2-08 — redaction e audit logs — Concluído

Subtickets concluídos: `F2-08-01` redaction; `F2-08-02` auditoria Profile;
`F2-08-03` auditoria Identity.

### F2-09 — double de Discord OAuth para CI — Concluído

## Fase 3 — Lobby core e concorrência

Fronteira: Lobby não lê tabelas de Identity/Profile. Ler: ADR-002, ADR-003 para
API→Lobby, TM-04/TM-15/TM-16, classificação e `plan.md` §3.1–3.3/§16 Fase 3.
Aceite: prova de capacidade sob corrida e respostas 401/403/409/503 corretas.

### F3-01 — aggregate, value objects e transitions — Concluído
### F3-02 — EF mappings, constraints e concorrência — Concluído
### F3-03 — CQRS sem broker — Em andamento

`F3-03-01` criação/busca e `F3-03-02` join/leave com retry limitado de `xmin`
estão concluídos. **Próximo: `F3-03-03`**, cancelamento CQRS usando o harness
owner-or-moderator; excluir endpoints, idempotência HTTP, broker e outbox.

### F3-04 — endpoints create/search/join/leave/cancel — Pendente
### F3-05 — ledger HTTP Idempotency-Key — Pendente
### F3-06 — keyset pagination e projections — Pendente
### F3-07 — corrida de 50 joins/overbooking — Pendente
### F3-08 — typed client API→Lobby/JWT/timeout/circuit breaker — Pendente
### F3-09 — degradação graciosa interna — Pendente

## Fase 4 — cache distribuído

Fronteira: projeções de leitura; cache não autoriza nem reserva vagas.
Dependências: F3-04/F3-06. Ler: TM-11/TM-12, classificação e `plan.md` §8/§16 Fase 4.
Aceite: Redis indisponível não afeta a correção de join.

### F4-01 — HybridCache/Redis L2 — Pendente
### F4-02 — keys, TTL/jitter e invalidação — Pendente
### F4-03 — lease para hot key — Pendente
### F4-04 — stale-while-revalidate de busca — Pendente
### F4-05 — bypass/fallback com limite — Pendente
### F4-06 — benchmark e métricas — Pendente

## Fase 5 — MassTransit, Outbox e DLQ

Fronteira: contratos e transação por contexto. Dependências: F3. Ler: ADR-000,
ADR-001, ADR-002, TM-06/TM-07/TM-10/TM-14, classificação e `plan.md` §5/§16
Fase 5. Aceite: provas de duplicata, reorder, crash-window, retry esgotado e
broker offline; nunca prometer exactly-once.

### F5-01 — Contracts V1/fixtures — Pendente
### F5-02 — topology RabbitMQ/endpoints — Pendente
### F5-03 — EF Bus Outbox Lobby — Pendente
### F5-04 — Consumer Outbox/inbox — Pendente
### F5-05 — retry/redelivery/jitter/filtros — Pendente
### F5-06 — error/skipped/Fault/métricas/runbook — Pendente
### F5-07 — kill tests commit/publish/ack — Pendente
### F5-08 — paridade LocalStack SQS/SNS — Pendente
### F5-09 — semântica at-least-once documentada — Pendente

## Fase 6 — Saga e Discord Integration

Fronteira: Orchestrator e Discord Integration possuem estado próprio.
Dependências: F5. Ler: ADR-001–003, TM-06/TM-08/TM-09/TM-10/TM-14/TM-15,
classificação e `plan.md` §5–7/§16 Fase 6. Aceite: uma operação lógica por
match e reconciliação para falhas ambíguas; nenhum segredo em contrato/log.

### F6-01 — MatchStateMachine/EF saga/outbox — Pendente
### F6-02 — commands/results/timeouts/compensação versionados — Pendente
### F6-03 — Discord client/Polly safe-unsafe — Pendente
### F6-04 — rate-limit/deferral durável — Pendente
### F6-05 — operação única por match — Pendente
### F6-06 — canal/permissões/invite/reconciliação — Pendente
### F6-07 — notification/cleanup/compensation — Pendente
### F6-08 — reconciler de órfãos — Pendente
### F6-09 — fake Discord de falhas — Pendente
### F6-10 — projection de provisioning — Pendente

## Fase 7 — migrations de produção

Fronteira: schema por contexto e identidade de migration dedicada. Ler:
classificação e `plan.md` §9/§16 Fase 7. Aceite: N/N+1,
expand/backfill/contract em releases distintos; contract/destrutivo requer
aprovação humana.

### F7-01 — bundle/SQL idempotente — Pendente
### F7-02 — usuário/role DDL — Pendente
### F7-03 — expand-contract dual read/write — Pendente
### F7-04 — backfill/checkpoints/métricas — Pendente
### F7-05 — índice/timeouts/volume — Pendente
### F7-06 — compatibility N/N+1 — Pendente
### F7-07 — restore/rollback — Pendente

## Fase 8 — observabilidade e performance

Fronteira: observabilidade aceita só campos permitidos. Dependências: F4–F6.
Ler: TM-10/TM-12/TM-14/TM-15, classificação e `plan.md` §10/§16 Fase 8.
Aceite: correlação ponta a ponta, alertas acionáveis e revisão de
cardinalidade/redaction.

### F8-01 — trace context — Pendente
### F8-02 — meters — Pendente
### F8-03 — dashboards — Pendente
### F8-04 — alertas/runbooks — Pendente
### F8-05 — laboratório starvation — Pendente
### F8-06 — load tests/SLO — Pendente
### F8-07 — sampling/redaction/cardinality — Pendente

## Fase 9 — Terraform e AWS sandbox

Fronteira: CI/AWS/OIDC. Nenhum apply, deploy ou destroy sem aprovação humana.
Dependências: F5–F8. Ler: ADR-004, TM-10/TM-12/TM-13, classificação e
`plan.md` §12–13/§16 Fase 9. Aceite: ambiente reproduzível, mesmo digest
promovido e evidência de rollback/custo.

### F9-01 — backend S3 — Pendente
### F9-02 — GitHub Environments/regras — Pendente
### F9-03 — OIDC/roles — Pendente
### F9-04 — ci.yml — Pendente
### F9-05 — infra-plan.yml — Pendente
### F9-06 — build/ECR/SBOM/scan/attestation/manifest — Pendente
### F9-07 — VPC/API Gateway/Cloud Map/SGs — Pendente
### F9-08 — ECS/autoscaling/circuit breaker — Pendente
### F9-09 — RDS/Secrets/ElastiCache — Pendente
### F9-10 — SNS/SQS/DLQ/alarms — Pendente
### F9-11 — ADOT/X-Ray/CloudWatch — Pendente
### F9-12 — deploy reusable/gates — Pendente
### F9-13 — dev/promoção — Pendente
### F9-14 — rollback/redeploy — Pendente
### F9-15 — budget/anomalias/tags — Pendente
### F9-16 — fault test/destroy sandbox — Pendente

## Fase 10 — hardening e portfólio

Fronteira: evidência operacional; replay de DLQ ou efeitos com usuários reais
requer aprovação humana. Dependências: F5–F9. Ler: threat model completo,
classificação e `plan.md` §15/§19–20/§16 Fase 10.

### F10-01 — threat-model/security tests — Pendente
### F10-02 — game day — Pendente
### F10-03 — replay auditado de DLQ — Pendente
### F10-04 — RTO/RPO/restore — Pendente
### F10-05 — revisão de decisões — Pendente
### F10-06 — README/C4/SLOs/evidências — Pendente
### F10-07 — FinOps/capacidade — Pendente
### F10-08 — interview packet — Pendente
