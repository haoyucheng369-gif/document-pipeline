# System Flow

This document describes the current end-to-end async flow in `CloudDocumentPipeline`.

## Environment Split

- local `Development`
  - RabbitMQ
  - local storage
- cloud `Testbed`
  - Azure Service Bus
  - Azure Blob
  - Azure Container Apps
- cloud `Production`
  - same runtime shape as testbed
  - promotes validated image tags

## Cloud Main Flow

```mermaid
flowchart TD
    A[Frontend uploads file] --> B[POST /api/jobs/document-to-pdf]
    B --> C[JobService.CreateDocumentToPdfAsync]
    C --> D[Save source file to Azure Blob]
    C --> E[(Jobs)]
    C --> F[(OutboxMessages)]
    F --> G[OutboxPublisherWorker]
    G --> H[Azure Service Bus Topic: job-events]
    H --> I[Subscription: worker]
    H --> J[Subscription: notification]
    H --> K[Subscription: api-realtime]
    I --> L[CloudDocumentPipeline.Worker]
    J --> M[CloudDocumentPipeline.NotificationService]
    K --> N[ServiceBusJobStatusUpdatesConsumer]
    L --> O[(InboxMessages)]
    L --> P[JobSideEffectExecutor]
    P --> Q[Save result PDF to Azure Blob]
    L --> E
    N --> R[SignalR Hub]
    R --> S[Frontend realtime refresh]
```

## End-to-End Processing

1. The frontend uploads an image, text, markdown, or HTML file.
2. The API calls `JobService.CreateDocumentToPdfAsync(...)`.
3. The source file is written to the configured storage provider.
4. The API writes both:
   - a `Job`
   - an `OutboxMessage`
5. `OutboxPublisherWorker` publishes `job.created` to the `job-events` topic.
6. The `worker` subscription is consumed by `CloudDocumentPipeline.Worker`.
7. The `notification` subscription is consumed by `CloudDocumentPipeline.NotificationService`.
8. The worker loads the source file and performs the conversion.
9. The worker saves the generated PDF and updates `Job` + `InboxMessage`.
10. The worker publishes `job.status.changed`.
11. The `api-realtime` subscription is consumed by the API realtime consumer.
12. The API sends `jobUpdated` through SignalR so the browser refreshes status.

## Worker Consumption Flow

```mermaid
flowchart TD
    A[Receive job.created] --> B[TryClaimAsync for Worker]
    B -->|Claim failed| C[Skip duplicate processing]
    B -->|Claim succeeded| D{Job already done?}
    D -->|Yes| E[Mark inbox processed]
    D -->|No| F[Read source file]
    F --> G[Convert document to PDF]
    G --> H[Save result PDF]
    H --> I[Commit Job and Inbox]
    I --> J[Publish job.status.changed]
```

## Failure and Recovery

```mermaid
flowchart TD
    A[Worker processing fails] --> B{Retryable?}
    B -->|Yes| C[Abandon / retry path]
    B -->|No| D[Dead-letter path]
    E[StaleInboxRecoveryWorker] --> F[Find long-running stuck inbox items]
    F --> G[Recover job state]
    G --> H[Write replay outbox message]
    H --> I[OutboxPublisherWorker republishes]
```

## Promotion Flow

```mermaid
flowchart LR
    A[Push to test] --> B[CI + build + push images]
    B --> C[Testbed deploy]
    C --> D[Validate cloud runtime]
    D --> E[Manual promote by image tag]
    E --> F[Run prod migrator if migrations changed]
    F --> G[Update prod apps]
    G --> H[Rollback by re-promoting an older validated tag]
```
