# Terraform 目录结构

这个目录存放 `DocFlowCloud` 的 Azure 基础设施代码。

## 结构说明

- `modules/`
  - 可复用的 Azure 资源模块
- `environments/testbed/`
  - testbed 环境入口
- `environments/prod/`
  - prod 环境入口

每个环境目录本身都是一个独立的 Terraform root module，所以：

- provider 声明放在 `environments/*`
- 环境变量和命名规则也放在 `environments/*`
- `main.tf` 再去组合 `modules/*`

## 当前模块

当前已经有的模块：

- `resource-group`
- `log-analytics`
- `container-app-environment`
- `sql-database`
- `storage-account`
- `service-bus`
- `key-vault`
- `container-app`
- `container-app-job`

## 当前覆盖范围

### 基础设施底座

- Resource Group
- Log Analytics Workspace
- Container Apps Environment
- Azure SQL Server + Database
- Storage Account + Blob Container
- Service Bus Namespace + Topic + Subscriptions
- Key Vault

### 运行层

- `api`
- `web`
- `worker`
- `notification`
- `migrator` job

### 运行时关键配置

- Managed Identity
- Key Vault secret references
- SQL / Blob / Service Bus secret 注入
- GHCR 私有镜像拉取认证
- API probes
- worker / notification 副本参数
- ingress 细配置
- revision mode
- migrator job timeout / retry / parallelism

## 调用方式

每个环境的执行链可以近似理解成：

```text
terraform.tfvars
-> variables.tf
-> locals.tf
-> main.tf
-> modules/*
-> Azure resources
-> outputs.tf
```

也就是：

- `variables.tf`
  - 定义输入
- `terraform.tfvars`
  - 提供环境实际参数
- `locals.tf`
  - 拼装命名和中间值
- `main.tf`
  - 调用模块
- `outputs.tf`
  - 暴露关键结果

## Secret 和镜像参数约定

- `terraform.tfvars`
  - 只保留非敏感环境参数
- `secrets.auto.tfvars`
  - 放本地敏感值和镜像地址
  - 已加入 `.gitignore`
- `secrets.auto.tfvars.example`
  - 只作为示例模板，不提交真实值

常见本地敏感参数包括：

- `sql_administrator_login_password`
- `sql_connection_string`
- `blob_connection_string`
- `service_bus_connection_string`
- `ghcr_registry_username`
- `ghcr_registry_password`
- `api_image`
- `web_image`
- `worker_image`
- `notification_image`
- `migrator_image`

如果后续接入 CI/CD，更推荐使用：

- `TF_VAR_*` 环境变量
- GitHub environment secrets

## 当前状态

这套 Terraform 主线已经完成到：

- 基础设施结构完整
- 运行层结构完整
- 运行时关键配置基本对齐现网

当前更像“可落地前的收尾阶段”，还差的主要是：

- 真实值注入
- 第一次真实 `terraform plan/apply`
- 可选的 remote state backend
- 可选的 infra workflow
