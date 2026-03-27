# 系统流程图

这份文档描述当前 `DocFlowCloud` 在两种环境下的真实消息链：

- **本地 Development**：继续使用 RabbitMQ，方便调试和断点
- **云上 Testbed**：使用 Azure Service Bus，走真实云消息基础设施

## 当前云上主链

```mermaid
flowchart TD
    A[前端上传文件] --> B[POST /api/jobs/document-to-pdf]
    B --> C[JobService.CreateDocumentToPdfAsync]
    C --> D[保存原文件到 Azure Blob]
    C --> E[(Jobs)]
    C --> F[(OutboxMessages)]
    F --> G[OutboxPublisherWorker]
    G --> H[Azure Service Bus Topic: job-events]
    H --> I[Subscription: worker]
    H --> J[Subscription: notification]
    H --> K[Subscription: api-realtime]
    I --> L[DocFlowCloud.Worker]
    J --> M[DocFlowCloud.NotificationService]
    K --> N[ServiceBusJobStatusUpdatesConsumer]
    L --> O[(InboxMessages)]
    L --> P[JobSideEffectExecutor]
    P --> Q[保存结果 PDF 到 Azure Blob]
    L --> E
    N --> R[SignalR Hub]
    R --> S[前端实时刷新状态]
```

## 从上传到处理完成

1. 前端上传一个图片、txt、md 或 html 文件。
2. API 调用 `JobService.CreateDocumentToPdfAsync(...)`。
3. `JobService` 先把原文件写入存储层，拿到 `InputStorageKey`。
4. `JobService` 在同一个数据库事务里写入：
   - 一条 `Job`
   - 一条 `OutboxMessage`
5. `OutboxPublisherWorker` 扫描未发布 Outbox，把 `job.created` 发到 Azure Service Bus Topic `job-events`。
6. `worker` subscription 被 `DocFlowCloud.Worker` 消费。
7. `notification` subscription 被 `DocFlowCloud.NotificationService` 消费。
8. Worker 从 Azure Blob 读取原文件，执行文档转 PDF。
9. Worker 把生成好的 PDF 保存回 Azure Blob，并更新数据库中的 `Job` / `InboxMessage`。
10. Worker 发布 `job.status.changed` 到同一个 Topic。
11. `api-realtime` subscription 被 API 里的 `ServiceBusJobStatusUpdatesConsumer` 消费。
12. API 通过 SignalR 把 `jobUpdated` 推给前端，前端实时更新状态。

## Worker 消费流程

```mermaid
flowchart TD
    A[收到 job.created] --> B[TryClaimAsync for Worker]
    B -->|Claim 失败| C[跳过重复消费]
    B -->|Claim 成功| D{Job 是否已完成?}
    D -->|是| E[只更新 Inbox=Processed]
    D -->|否| F[从 Azure Blob 读取原文件]
    F --> G[执行文档转 PDF]
    G --> H[保存结果 PDF 到 Azure Blob]
    H --> I[提交事务更新 Job 和 Inbox]
    I --> J[发布 job.status.changed]
```

## Notification Service 流程

```mermaid
flowchart TD
    A[收到 job.created] --> B[TryClaimAsync for Notification]
    B -->|Claim 失败| C[跳过重复消费]
    B -->|Claim 成功| D[执行通知逻辑]
    D --> E[Inbox 标记为 Processed]
```

## 失败与恢复

```mermaid
flowchart TD
    A[Worker 处理异常] --> B{可重试吗?}
    B -->|是| C[Abandon 消息]
    C --> D[Service Bus 重新投递]
    B -->|否| E[Dead-letter 到 DLQ]
    F[StaleInboxRecoveryWorker] --> G[扫描长期卡在 Processing 的 Inbox]
    G --> H[恢复 Job 状态]
    H --> I[写入新的 OutboxMessage]
    I --> J[OutboxPublisherWorker 重新发消息]
```

## 环境差异

### 本地 Development

- SQL Server：本地 Docker
- 消息系统：RabbitMQ
- 文件存储：Local
- 目的：快速调试、断点、低成本开发

### 云上 Testbed

- Azure SQL Database
- Azure Service Bus
- Azure Blob Storage
- Azure Container Apps
- 目的：验证完整云上交付链和真实运行环境
