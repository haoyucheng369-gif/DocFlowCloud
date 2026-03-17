# DocFlowCloud

DocFlowCloud 现在是一个面向学习和演示的**异步文档转 PDF 系统**。

它把一个比较完整的企业常见后端骨架落到了具体业务上：
- ASP.NET Core Web API
- Clean Architecture
- RabbitMQ Topic 事件分发
- Outbox / Inbox
- TryClaim 去重与抢占处理权
- Worker 后台异步处理
- Retry / DLQ / Backoff
- CorrelationId 链路日志
- 本地共享存储 / Azure Blob 可切换文件存储
- React + TypeScript + Tailwind 前端
- TanStack Query + react-hook-form + zod

## 当前业务能力

- 上传简单文档并创建异步转换任务
- 支持输入类型：
  - 图片：`jpg / jpeg / png / bmp / gif / webp`
  - 文本：`txt`
  - Markdown：`md`
  - HTML：`html / htm`
- 后台异步转成 PDF
- 查看任务列表
- 查看任务详情
- 下载转换结果
- 失败后重试
- 处理中卡死自动恢复

## 文档

- [架构说明](docs/architecture.md)
- [系统流程图](docs/system-flow.md)
- [状态图](docs/state-diagrams.md)

## 项目结构

- `src/DocFlowCloud.Api`
  HTTP API，负责上传文件、创建任务、查询任务、下载结果
- `src/DocFlowCloud.Application`
  用例编排、DTO、消息契约、文件存储抽象
- `src/DocFlowCloud.Domain`
  `Job` / `Inbox` / `Outbox` 领域模型和状态机
- `src/DocFlowCloud.Infrastructure`
  EF Core、RabbitMQ、文件存储实现、依赖注入
- `src/DocFlowCloud.Worker`
  Outbox 发布、Job 消费、文档转 PDF、卡死恢复
- `src/DocFlowCloud.NotificationService`
  第二个消费者，模拟通知服务
- `src/DocFlowCloud.Web`
  React 前端，负责上传、列表、详情、下载、重试

## 本地开发启动

### 方式一：推荐开发方式

只用 Docker 跑基础设施，本机直接调试应用。

1. 启动 SQL Server 和 RabbitMQ

```powershell
docker compose up -d sqlserver rabbitmq
```

2. 启动 API

```powershell
dotnet run --project src/DocFlowCloud.Api
```

3. 启动 Worker

```powershell
dotnet run --project src/DocFlowCloud.Worker
```

4. 启动 Notification Service

```powershell
dotnet run --project src/DocFlowCloud.NotificationService
```

5. 启动前端

```powershell
cd src/DocFlowCloud.Web
npm install
npm run dev
```

前端默认地址：
- `http://localhost:5173` 或 Vite 控制台显示的地址

后端默认地址：
- API：`http://localhost:5000` 或实际启动端口
- RabbitMQ 管理台：`http://localhost:15672`

### 方式二：一键 Docker 启动

```powershell
docker compose up --build
```

启动后默认访问：
- 前端：`http://localhost:3000`
- API：`http://localhost:8080`
- RabbitMQ 管理台：`http://localhost:15672`

## 文件存储

当前默认使用：
- `Storage:Provider = Local`

本地和 Docker 下都会使用共享目录：
- `shared-storage/uploads/...`
- `shared-storage/results/...`

数据库里不再保存文件 Base64，而是只保存：
- `InputStorageKey`
- `OutputStorageKey`

后续上云时可以切换：
- `Storage:Provider = AzureBlob`

## 示例请求

### 上传并创建文档转 PDF 任务

```http
POST /api/jobs/document-to-pdf
Content-Type: multipart/form-data
```

表单字段：
- `file`：上传文件
- `name`：可选任务名

### 查询任务列表

```http
GET /api/jobs
```

### 查询任务详情

```http
GET /api/jobs/{id}
```

### 下载 PDF

```http
GET /api/jobs/{id}/result-file
```

### 重试失败任务

```http
POST /api/jobs/{id}/retry
```

## 前端演示页面

当前前端包含 3 个核心页面：

1. 新建任务页
   - 选择文件
   - 填任务名
   - 提交转换任务

2. 任务列表页
   - 查看任务名称
   - 查看状态
   - 查看创建时间
   - 判断是否可重试

3. 任务详情页
   - 查看当前状态
   - 自动轮询任务结果
   - 下载转换后的 PDF
   - 对失败任务执行重试

## 演示建议

推荐你按下面顺序演示：

1. 在前端上传一个 `txt / md / html / image` 文件
2. 提交后跳转到任务详情页
3. 展示任务状态从 `Pending -> Processing -> Succeeded`
4. 下载转换后的 PDF
5. 查看任务列表中的状态变化
6. 如果需要，演示失败任务重试

## 适合展示的技术点

- 为什么文档转换适合异步处理
- 为什么需要 Outbox / Inbox
- 为什么需要 TryClaim
- 为什么要做业务幂等
- 为什么要做 Retry / DLQ / Backoff
- 为什么要做 CorrelationId
- 为什么文件存储要抽象成 `StorageKey + Provider`

## 当前定位

这个项目更适合理解成：

- 一个高质量的异步文档处理学习项目
- 一个带消息可靠性和恢复能力的企业应用样板

它不是一个完整微服务平台，但已经覆盖了很多企业项目里非常重要的核心能力。
