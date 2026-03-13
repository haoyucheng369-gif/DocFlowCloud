# 系统流程图

这份文档说明当前 DocFlowCloud 中，一个请求是如何流转到消息、消费者和数据库状态更新的。

## 主流程

```mermaid
flowchart TD
    A[客户端 POST /api/jobs] --> B[DocFlowCloud.Api]
    B --> C[JobService.CreateAsync]
    C --> D[(Jobs)]
    C --> E[(OutboxMessages)]
    E --> F[OutboxPublisherWorker]
    F --> G[Topic Exchange: docflow.events]
    G --> H[Queue: docflow.jobs]
    G --> I[Queue: docflow.notifications]
    H --> J[DocFlowCloud.Worker]
    I --> K[DocFlowCloud.NotificationService]
    J --> L[(InboxMessages)]
    K --> L
    J --> M[JobSideEffectExecutor]
    K --> N[NotificationEmailSender]
    J --> D
```

## 从请求到处理完成

1. 客户端调用 `POST /api/jobs`。
2. `JobService` 在同一个数据库事务里写入：
   - 一条 `Job`
   - 一条 `OutboxMessage`
3. `OutboxPublisherWorker` 扫描未处理的 outbox 记录。
4. 发布器把 `job.created` 事件发送到 `docflow.events` 这个 topic exchange。
5. RabbitMQ 把同一个事件路由到两个队列：
   - `docflow.jobs`
   - `docflow.notifications`
6. `DocFlowCloud.Worker` 消费任务处理队列。
7. `DocFlowCloud.NotificationService` 消费通知队列，模拟发送邮件。

## 任务处理 Worker 流程

```mermaid
flowchart TD
    A[收到 job.created] --> B[TryClaimAsync for JobConsumer]
    B -->|Claim 失败| C[Ack 并跳过]
    B -->|Claim 成功| D{Job 已经成功了吗?}
    D -->|是| E[把 Inbox 标为 Processed]
    E --> C
    D -->|否| F[执行任务副作用]
    F --> G[事务提交: Job + Inbox]
    G --> H[Job 标记为 Succeeded]
    G --> I[Inbox 标记为 Processed]
    H --> C
    I --> C
```

## 通知服务流程

```mermaid
flowchart TD
    A[收到 job.created] --> B[TryClaimAsync for NotificationConsumer]
    B -->|Claim 失败| C[Ack 并跳过]
    B -->|Claim 成功| D[模拟发送邮件]
    D --> E[Inbox 标记为 Processed]
    E --> C
```

## 失败与重试流程

```mermaid
flowchart TD
    A[Worker 出现异常] --> B[先把 Job / Inbox 标记为 Failed]
    B --> C{是否可重试?}
    C -->|是| D[发送到 retry queue]
    D --> E[等待 TTL 到期]
    E --> F[通过 dead-letter 回到 topic exchange]
    F --> G[主队列重新收到消息]
    C -->|否| H[直接发送到 DLQ]
```

## CorrelationId 链路

1. API 读取或生成 `X-Correlation-Id`。
2. 该值会写入响应头和 Serilog 日志上下文。
3. `JobService` 把相同的 `CorrelationId` 写入消息体。
4. Worker 消费消息时，再把这个值恢复到自己的日志上下文。
5. 后续排查时，可以通过同一个 `CorrelationId` 串起 API 和两个消费者的日志。
