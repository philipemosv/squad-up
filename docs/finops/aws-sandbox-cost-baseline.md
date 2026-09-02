# AWS sandbox cost baseline

- Status: Version-controlled estimate; not deployment approval
- Price date: 2026-09-02
- Region: `us-east-1` (US East, N. Virginia)
- Currency: USD, before tax
- Profile: synthetic-data sandbox from ADR-004
- Owner: Squad-Up maintainers

## Purpose and limits

This baseline makes the sandbox cost hypothesis reviewable before Terraform or
AWS credentials exist. It compares an ephemeral learning lab with the same
small topology running continuously.

It is an estimate, not an invoice, quote, spending cap, production estimate, or
claim of Free Tier eligibility. AWS prices, account credits, taxes, data
transfer paths, log volume, and usage can change the result. Re-enter these
inputs in the AWS Pricing Calculator and attach its shared estimate before the
first `terraform apply`.

Accepting this document does not authorize AWS account access, resource
creation, Terraform apply, secret retrieval, or real-user data processing.

## Architecture assumptions

The baseline models:

- one public API Gateway HTTP API with 100,000 requests/month;
- one VPC Link and HTTP-only Cloud Map discovery, without NAT Gateway, ALB, NLB,
  Route 53 DNS discovery, or custom domain;
- three Linux/ARM64 Fargate tasks:
  - API: `0.25` vCPU and `0.5` GB;
  - Lobby: `0.25` vCPU and `0.5` GB;
  - co-hosted Match Orchestrator + Discord Integration: `0.25` vCPU and
    `0.5` GB total;
- one public IPv4 address per running task, with no public security-group
  ingress; application entry remains through API Gateway;
- one RDS for PostgreSQL `db.t4g.micro`, Single-AZ, with 20 GB gp3 storage and
  no additional provisioned IOPS or throughput;
- two registered Cloud Map resources and 100,000 discovery calls/month;
- 100,000 SQS Standard API actions and 20,000 SNS publishes/month;
- nine separately scoped Secrets Manager secrets and 10,000 reads/month;
- 2 GB of private ECR image storage and less than 0.5 GB of Terraform state;
- 2 GB of logs ingested, 1 GB retained, 0.5 GB of OpenTelemetry metrics, one
  dashboard, ten standard alarms, and at most 100,000 recorded traces/month;
- at most 5 GB/month of internet data transfer out;
- 730 hours for a continuously running month.

Fargate includes the first 20 GB of ephemeral storage per task. Cache is not in
the base topology and is priced separately.

## Price evidence

The exact RDS prices were read from the AWS public Price List bulk catalog for
`us-east-1`, retrieved on 2026-09-02. The object reported `Last-Modified: Mon,
31 Aug 2026 09:27:34 GMT`.

| Item | Unit rate used | Evidence or formula |
| --- | ---: | --- |
| Fargate Linux/ARM vCPU | $0.0000089944/vCPU-second | AWS Fargate N. Virginia ARM example |
| Fargate Linux/ARM memory | $0.0000009889/GB-second | AWS Fargate N. Virginia ARM example |
| Public IPv4 | $0.005/address-hour | AWS VPC pricing |
| RDS PostgreSQL `db.t4g.micro`, Single-AZ | $0.016/instance-hour | AWS Price List SKU `9HPEGXQTDDGH53C9` |
| RDS PostgreSQL gp3 | $0.115/GB-month | AWS Price List SKU `KYVYY29G957PKY3B` |
| RDS PostgreSQL additional backup | $0.095/GB-month | AWS Price List SKU `6W8ECRFVDATCER7J` |
| API Gateway HTTP API | $1.00/million requests | First pricing tier in N. Virginia |
| Cloud Map registered resource | $0.10/resource-month | AWS Cloud Map pricing example |
| Cloud Map API discovery | $1.00/million calls | AWS Cloud Map pricing example |
| SQS Standard | $0.40/million API actions | Paid unit rate; monthly allowance ignored |
| SNS publish | $0.50/million requests | Paid unit rate; monthly allowance ignored |
| Secrets Manager | $0.40/secret-month + $0.05/10,000 calls | AWS Secrets Manager pricing |
| Private ECR storage | $0.10/GB-month | AWS ECR pricing |
| CloudWatch logs ingestion | $0.50/GB | N. Virginia example; allowance ignored |
| CloudWatch OTel metrics ingestion | $0.50/GB | AWS CloudWatch pricing |
| CloudWatch dashboard | $3.00/dashboard-month | Up to 50 metrics |
| CloudWatch standard alarm | $0.10/alarm-metric-month | N. Virginia pricing example |
| S3 Standard state storage | $0.023/GB-month | First N. Virginia storage tier |
| Internet data transfer out | First 100 GB/month $0; then $0.09/GB | Aggregate AWS allowance and first paid tier |

The HTTP API pricing page does not publish a separate hourly VPC Link line
item. This estimate therefore prices HTTP API calls and Cloud Map but no
standalone VPC Link fee. That is an inference from the current public price
pages and must be checked in the Calculator during the Cloud Map integration
spike.

## Continuously running estimate

The Fargate rate for one selected task is:

```text
(0.25 vCPU × $0.0000089944 × 3,600)
+ (0.5 GB × $0.0000009889 × 3,600)
= $0.00987498 per task-hour
```

| Component | Monthly calculation | Estimated cost |
| --- | --- | ---: |
| Three Fargate tasks | `3 × 730 × $0.00987498` | $21.63 |
| Three public IPv4 addresses | `3 × 730 × $0.005` | $10.95 |
| RDS compute | `730 × $0.016` | $11.68 |
| RDS gp3 storage | `20 × $0.115` | $2.30 |
| API Gateway HTTP API | `0.1 million × $1.00` | $0.10 |
| Cloud Map | `2 × $0.10 + 0.1 million × $1.00` | $0.30 |
| SQS + SNS | `0.1 million × $0.40 + 0.02 million × $0.50` | $0.05 |
| Secrets Manager | `9 × $0.40 + 10,000 calls` | $3.65 |
| ECR | `2 GB × $0.10` | $0.20 |
| S3 state and requests | Conservative small-state allowance | $0.02 |
| Observability | Logs $1.03 + OTel $0.25 + dashboard $3 + alarms $1 | $5.28 |
| X-Ray up to 100,000 traces | Current monthly trace allowance | $0.00 |
| Up to 5 GB transfer out | Within current 100 GB aggregate allowance | $0.00 |
| Budget monitoring + anomaly detection | Current monitoring price | $0.00 |
| **Raw estimate** | | **$56.16/month** |
| **20% uncertainty reserve** | Rounding, requests, transfer paths, log variance | **$11.23/month** |
| **Planning total** | | **$67.39/month** |

ElastiCache Serverless for Valkey is optional and starts around $6/month at its
100 MB minimum before meaningful ECPU usage. Adding that minimum and the same
20% reserve produces approximately **$74.59/month**.

## Ephemeral lab estimate

For 40 aggregate active hours/month, with the Fargate, RDS, API Gateway, Cloud
Map resources, queues, topics, alarms, and logs destroyed after each lab, the
same rates produce:

| Cost group | Assumption | Estimated cost |
| --- | --- | ---: |
| Fargate + IPv4 + RDS compute/storage | 40 active hours, no retained DB snapshot | $2.55 |
| API Gateway + Cloud Map + messaging | Small lab request volume | $0.04 |
| Persistent Secrets Manager secrets | Nine secrets + 10,000 reads | $3.65 |
| Persistent ECR + S3 state | 2 GB images + small state | $0.22 |
| Short-lived observability | Bounded logs, metrics, and alarms | $0.63 |
| **Raw estimate** | | **$7.09/month** |
| **With 20% uncertainty reserve** | | **$8.51/month** |

The operational target for this mode is **at most $10/month**. Retaining one
full 20 GB RDS snapshot after database deletion adds approximately $1.90 for a
full month. Repeatedly stopping RDS is not the strategy: AWS automatically
restarts a stopped instance after seven consecutive days, so an idle lab is
destroyed and rebuilt from synthetic seed data.

## Budget and guardrails

Create these controls before the first sandbox resources:

- one monthly cost budget of **$80** for the `sandbox` profile;
- notifications at 50% ($40), 80% ($64), and 100% ($80), including forecasted
  thresholds where supported;
- Cost Anomaly Detection monitor and subscription;
- cost allocation tags `Project`, `Environment`, `Owner`, `CostCenter`,
  `ManagedBy`, and `DataClassification`;
- log retention and trace/metric sampling limits in Terraform;
- desired counts and autoscaling maxima that cannot silently exceed this
  topology;
- no NAT Gateway, load balancer, ElastiCache, WAF, customer-managed KMS key,
  cross-region copy, or paid support plan without an updated estimate;
- a TTL/owner and verified destroy command for the disposable environment.

AWS Budgets notifications do not stop spending. The $80 value is a review
threshold, not a hard cap. An alarm must lead to inspection and safe destroy or
scale-down, never an automated destructive production action.

## Excluded from this baseline

- production or `sa-east-1`;
- taxes, currency conversion, and paid AWS Support;
- custom domain and Route 53 hosted zone;
- NAT Gateway, ALB/NLB, PrivateLink endpoints, WAF, and cross-AZ transfer;
- ElastiCache except for the optional minimum shown above;
- customer-managed KMS keys and their API calls;
- RDS Multi-AZ, read replicas, extra IOPS/throughput, and retained snapshots;
- Container Insights enhanced observability, Application Signals, synthetic
  canaries, large trace queries, and high-cardinality custom telemetry;
- vulnerability scanning beyond ECR basic scanning;
- traffic or storage above the stated assumptions.

These are not assumed to be free. Adding any one of them requires a new line in
the estimate before implementation.

## Calculator reproduction checklist

Before the first apply, create and share an AWS Pricing Calculator estimate
using exactly these inputs:

1. Select `us-east-1` and USD.
2. Add three ECS Fargate Linux/ARM tasks at `0.25` vCPU, `0.5` GB, 730 hours.
3. Add three in-use public IPv4 addresses for 730 hours.
4. Add RDS PostgreSQL `db.t4g.micro`, Single-AZ, on demand, 20 GB gp3.
5. Add 100,000 API Gateway HTTP API requests.
6. Add the Cloud Map, SQS, SNS, Secrets Manager, ECR, S3, CloudWatch, X-Ray, and
   transfer assumptions from the architecture table.
7. Do not apply introductory credits, time-limited Free Plan credits,
   reservations, Savings Plans, or production commitments.
8. Compare the Calculator subtotal with $56.16. Explain any difference greater
   than 10% by changed price, omitted dimension, or changed assumption.
9. Store the shared estimate URL, creation date, and subtotal in this section.

Calculator shared estimate: **pending account/Calculator access; required before
first apply**.

## Update triggers

Recalculate when:

- the first Terraform plan defines exact counts or sizes;
- a price or region changes;
- measured monthly usage differs by more than 20% from an assumption;
- the raw forecast exceeds $64, which is 80% of the sandbox budget;
- cache, NAT, load balancer, WAF, VPC endpoint, custom KMS key, enhanced
  observability, retained snapshot, or cross-region traffic enters scope;
- the sandbox processes real-user data or a production region is considered.

After the first full month, replace assumptions with Cost Explorer/Cost and
Usage Report evidence while keeping this baseline for comparison.

## Sources

- [AWS Fargate pricing](https://aws.amazon.com/fargate/pricing/)
- [AWS public RDS Price List for `us-east-1`](https://pricing.us-east-1.amazonaws.com/offers/v1.0/aws/AmazonRDS/current/us-east-1/index.json)
- [Stopping an RDS instance temporarily](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_StopInstance.html)
- [Amazon VPC and public IPv4 pricing](https://aws.amazon.com/vpc/pricing/)
- [Amazon API Gateway pricing](https://aws.amazon.com/api-gateway/pricing/)
- [AWS Cloud Map pricing](https://aws.amazon.com/cloud-map/pricing/)
- [Amazon SQS pricing](https://aws.amazon.com/sqs/pricing/)
- [Amazon SNS pricing](https://aws.amazon.com/sns/pricing/)
- [AWS Secrets Manager pricing](https://aws.amazon.com/secrets-manager/pricing/)
- [Amazon ECR pricing](https://aws.amazon.com/ecr/pricing/)
- [Amazon CloudWatch and X-Ray pricing](https://aws.amazon.com/cloudwatch/pricing/)
- [Amazon ElastiCache pricing](https://aws.amazon.com/elasticache/pricing/)
- [AWS Budgets pricing](https://aws.amazon.com/aws-cost-management/aws-budgets/pricing/)
- [EC2 data transfer pricing](https://aws.amazon.com/ec2/pricing/on-demand/)
