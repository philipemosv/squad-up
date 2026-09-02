# ADR-004: AWS deployment profiles and initial region

- Status: Accepted
- Date: 2026-09-01
- Decision owners: Squad-Up maintainers

## Context

Squad-Up needs a cloud sandbox for learning and integration tests without
paying the fixed cost of a production-like platform continuously. A real
production environment has different availability, data protection, recovery,
and operational requirements. Treating production as a larger sandbox would
hide those differences.

Region choice also affects latency, service availability, price, and data
location. Those properties change and must be measured close to deployment;
they should not be assumed from an architecture document.

## Decision

Maintain two explicit Terraform deployment profiles: `sandbox` and
`production`. They use separate state, accounts or strongly isolated account
boundaries, configuration, secrets, and deployment roles. The production
profile is a reference architecture, not authorization to provision resources.

### Sandbox profile

Start the synthetic, non-sensitive sandbox in `us-east-1`. This is a
provisional engineering choice for broad service availability and ecosystem
support, not a production-region decision or a claim of legal suitability.

Before the first Terraform apply:

- confirm every selected service and architecture is available in the region;
- estimate the complete profile with the current AWS Pricing Calculator;
- set budget alerts, cost-anomaly monitoring, mandatory cost tags, and an owner;
- document a time-to-live and a tested Terraform destroy procedure.

Before sending real Brazilian user data or traffic, measure end-to-end latency
from the intended audience against at least `sa-east-1`, review data residency
and privacy requirements, and record a separate production-region decision.

The sandbox topology is intentionally cost-conscious:

- API Gateway HTTP API is the public entry point.
- A VPC Link and AWS Cloud Map provide private integration to ECS services. A
  technical spike must prove discovery, deployment, and failure behavior before
  this becomes implementation baseline.
- Small ARM64 ECS Fargate tasks run with one replica where availability permits.
- To avoid a continuously billed NAT gateway, application tasks may use public
  subnets and public IP addresses for outbound access. Security groups allow no
  direct public inbound traffic; ingress remains through the API Gateway path.
- Data stores remain non-public. PostgreSQL starts as a small, single-AZ RDS
  instance with separate database/schema/user ownership per bounded context.
- SQS Standard queues and SNS topics implement the messaging decision in
  ADR-001.
- The orchestrator and Discord worker may share a task definition only in this
  profile. Fargate Spot is limited to interruption-safe, idempotent workers.
- Cache is added only when a measured use case justifies it.
- The environment is ephemeral and recreated from Terraform rather than
  repaired manually.

Public IPv4 addresses, logs, data transfer, secrets, storage, and idle managed
services are billable concerns. Avoiding NAT or a load balancer does not make
the sandbox free.

### Production profile

Production uses the separately approved region and a hardened topology:

- application tasks run in private subnets across availability zones;
- controlled outbound access supports Discord and other required external APIs
  through measured NAT and/or VPC endpoint choices;
- synchronous services have at least two replicas and autoscaling based on
  observed load and saturation signals;
- workers have separate task definitions, scaling policies, queues, and
  interruption policies;
- RDS uses Multi-AZ deployment, backups, point-in-time recovery, encryption,
  deletion protection, and a regularly tested restore procedure;
- any cache has an availability and recovery design matching its role;
- alarms, dashboards, audit logs, secret rotation, and incident runbooks are
  release requirements.

### Deployment identity and cost controls

GitHub Actions authenticates to AWS through OpenID Connect and short-lived role
credentials. Do not store long-lived AWS access keys in GitHub secrets.

- Separate read-only planning and approved deployment roles.
- Give each ECS task its own least-privilege task role.
- Use a distinct, time-bounded role for database migrations.
- Restrict the GitHub OIDC trust policy to the exact organization, repository,
  branch or environment, and expected audience.
- Require tags for environment, service, owner, and cost allocation.
- Alert at progressive budget thresholds and investigate anomaly alerts; a
  budget notification is not a hard spending cap.

No AWS resources are created merely by accepting this ADR. Terraform code,
reviewed cost estimates, credentials, and an explicit apply remain separate
steps.

## Alternatives considered

### Make the sandbox production-like from day one

This improves parity but creates a high idle-cost floor for Multi-AZ databases,
NAT gateways, load balancers, and duplicate compute before the application has
measured demand.

### Develop only against local emulators

This is cheapest, but cannot validate IAM, VPC integration, regional service
behavior, quotas, or managed-service failure paths. Local development remains
the inner loop; the disposable sandbox proves cloud integration.

### Select `sa-east-1` for every environment immediately

It may provide better latency for a Brazilian audience, but latency, current
pricing, and service availability must be compared using the actual workload.
The sandbox starts elsewhere without predetermining production placement.

### Run Kubernetes

Kubernetes would introduce a control plane and operational surface not needed
by the pilot. ECS Fargate demonstrates container scheduling and service
boundaries with fewer components.

### Use NAT gateways and a load balancer in the sandbox

This more closely resembles the production network but adds recurring cost.
The selected compromise is acceptable only for synthetic sandbox workloads and
must be validated by the API Gateway and Cloud Map spike.

## Consequences

### Positive

- Cloud integration can be learned and tested with a bounded idle-cost target.
- Production requirements are explicit instead of being inferred by scaling a
  development environment.
- Short-lived deployment credentials reduce secret-management exposure.
- Region, topology, and cost assumptions have concrete validation gates.

### Negative

- Sandbox and production have meaningful topology differences.
- Public-IP sandbox tasks require careful security-group and routing review.
- Cloud Map private integration adds discovery behavior that must be proven.
- A production-region review and a production-readiness pass remain mandatory.

## Enforcement

- Terraform plans and state are isolated per profile.
- Policy checks reject public database access and unrestricted inbound rules.
- CI scans Terraform and verifies required tags and OIDC-based roles.
- A sandbox smoke test covers API Gateway, VPC Link, Cloud Map, ECS, messaging,
  database access, and outbound Discord connectivity.
- Monthly cost review compares actual spend with the estimate and removes idle
  resources.
- Production cannot reuse the sandbox profile or region implicitly.

## Revisit when

- The Cloud Map integration spike fails its deployment or reliability criteria.
- Measured traffic justifies an ALB, a NAT gateway, VPC endpoints, or a cache.
- Current pricing or service availability changes the sandbox trade-off.
- Real-user data, a public pilot, or production deployment enters scope.
- Availability objectives require a different regional or multi-region design.

## Sources

- [API Gateway private integrations](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-private.html)
- [AWS tutorial for an ECS private integration](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-private-integration.html)
- [GitHub Actions OIDC in AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws)
- [AWS cost usage controls](https://docs.aws.amazon.com/wellarchitected/latest/framework/cost_govern_usage_controls.html)
- [AWS Cost Anomaly Detection](https://docs.aws.amazon.com/cost-management/latest/userguide/manage-ad.html)
