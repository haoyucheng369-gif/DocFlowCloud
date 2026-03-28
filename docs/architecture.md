# Architecture

`DocFlowCloud` is an asynchronous document-to-PDF system designed to demonstrate a realistic application architecture across local development, cloud testbed, and production.

## Layers

### `DocFlowCloud.Web`

Responsibilities:

- upload files
- create jobs
- display job list and detail
- subscribe to SignalR updates
- download the generated PDF

### `DocFlowCloud.Api`

Responsibilities:

- receive uploaded files
- create conversion jobs
- expose job query and result download endpoints
- host the SignalR hub
- consume realtime status updates in cloud environments

Key files:

- `src/DocFlowCloud.Api/Program.cs`
- `src/DocFlowCloud.Api/Controllers/JobsController.cs`
- `src/DocFlowCloud.Api/Realtime/ServiceBusJobStatusUpdatesConsumer.cs`

### `DocFlowCloud.Application`

Responsibilities:

- define use cases and orchestration rules
- define integration messages and contracts
- depend on abstractions instead of providers

Key files:

- `src/DocFlowCloud.Application/Jobs/JobService.cs`
- `src/DocFlowCloud.Application/Messaging/JobCreatedIntegrationMessage.cs`

### `DocFlowCloud.Domain`

Responsibilities:

- define `Job`
- define `InboxMessage`
- define `OutboxMessage`
- enforce state transitions

### `DocFlowCloud.Infrastructure`

Responsibilities:

- EF Core persistence
- local / Azure Blob storage implementations
- RabbitMQ / Azure Service Bus implementations
- dependency injection and provider switching

Design note:

- local `Development` keeps RabbitMQ and local storage
- cloud `Testbed` / `Production` switch to Service Bus and Azure Blob

Key files:

- `src/DocFlowCloud.Infrastructure/DependencyInjection.cs`
- `src/DocFlowCloud.Infrastructure/Messaging/ServiceBusJobMessagePublisher.cs`
- `src/DocFlowCloud.Infrastructure/Storage/AzureBlobFileStorage.cs`

### `DocFlowCloud.Worker`

Responsibilities:

- publish outbox messages
- consume job messages
- execute document conversion
- update job state and result storage

Key files:

- `src/DocFlowCloud.Worker/OutboxPublisherWorker.cs`
- `src/DocFlowCloud.Worker/ServiceBusWorker.cs`
- `src/DocFlowCloud.Worker/JobSideEffectExecutor.cs`
- `src/DocFlowCloud.Worker/StaleInboxRecoveryWorker.cs`

### `DocFlowCloud.NotificationService`

Responsibilities:

- subscribe to job events
- run secondary consumer logic
- maintain its own inbox processing state

Key files:

- `src/DocFlowCloud.NotificationService/ServiceBusNotificationWorker.cs`

## Data and Storage

### Database

Primary tables:

- `Jobs`
- `InboxMessages`
- `OutboxMessages`

### File storage

The database stores logical storage keys rather than file contents.

- development: local file storage
- cloud: Azure Blob Storage

Important keys:

- `InputStorageKey`
- `OutputStorageKey`

## Messaging Model

### Local development

- RabbitMQ

### Cloud testbed and production

- Azure Service Bus topic: `job-events`
- subscriptions:
  - `worker`
  - `notification`
  - `api-realtime`

## Reliability Patterns

- Outbox
  - API writes `Job` and `OutboxMessage` in one transaction
- Inbox
  - consumer-side idempotency and claim tracking
- Retry / DLQ
  - transient failures are retried; terminal failures land in dead-letter handling
- Stale recovery
  - long-running stuck processing states can be replayed safely

## Cloud Runtime Model

### Testbed

- Azure Container Apps:
  - `web`
  - `api`
  - `worker`
  - `notification-service`
- Azure SQL Database
- Azure Blob Storage
- Azure Service Bus
- Azure Key Vault
- managed identities

### Production

- same runtime shape as testbed
- validated image tags are promoted from testbed
- runtime secrets come from Key Vault through managed identity
