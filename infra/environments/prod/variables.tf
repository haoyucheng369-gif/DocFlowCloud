variable "location" {
  description = "prod 环境使用的 Azure 区域。"
  type        = string
  default     = "francecentral"
}

variable "project_name" {
  description = "命名规则里使用的项目短前缀。"
  type        = string
  default     = "docflow"
}

variable "sql_administrator_login" {
  description = "prod Azure SQL Server 的管理员用户名。"
  type        = string
  default     = "docflowadmin"
}

variable "sql_administrator_login_password" {
  description = "prod Azure SQL Server 的管理员密码。"
  type        = string
  sensitive   = true
}

variable "sql_connection_string" {
  description = "prod 运行时使用的 SQL 连接字符串。"
  type        = string
  sensitive   = true
}

variable "blob_connection_string" {
  description = "prod 运行时使用的 Blob connection string。"
  type        = string
  sensitive   = true
}

variable "service_bus_connection_string" {
  description = "prod 运行时使用的 Service Bus connection string。"
  type        = string
  sensitive   = true
}

variable "sql_sku_name" {
  description = "prod Azure SQL Database 使用的 SKU。"
  type        = string
  default     = "Basic"
}

variable "sql_storage_account_type" {
  description = "prod Azure SQL Database 备份存储冗余类型。"
  type        = string
  default     = "Geo"
}

variable "storage_account_tier" {
  description = "prod Storage Account 性能层。"
  type        = string
  default     = "Standard"
}

variable "storage_account_replication_type" {
  description = "prod Storage Account 副本类型。"
  type        = string
  default     = "LRS"
}

variable "storage_allow_nested_items_to_be_public" {
  description = "prod 是否允许存储账户中的嵌套项公开访问。"
  type        = bool
  default     = true
}

variable "service_bus_sku" {
  description = "prod Service Bus Namespace 使用的 SKU。"
  type        = string
  default     = "Standard"
}

variable "service_bus_max_delivery_count" {
  description = "prod Service Bus subscription 的最大投递次数。"
  type        = number
  default     = 10
}

variable "service_bus_topic_default_message_ttl" {
  description = "prod Service Bus topic 默认 TTL。"
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "service_bus_topic_enable_batched_operations" {
  description = "prod Service Bus topic 是否启用 batched operations。"
  type        = bool
  default     = false
}

variable "service_bus_subscription_default_message_ttl" {
  description = "prod Service Bus subscription 默认 TTL。"
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "service_bus_subscription_auto_delete_on_idle" {
  description = "prod Service Bus subscription 自动删除闲置时间。"
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "service_bus_subscription_enable_batched_operations" {
  description = "prod Service Bus subscription 是否启用 batched operations。"
  type        = bool
  default     = false
}

variable "service_bus_subscription_dead_lettering_on_filter_evaluation_error" {
  description = "prod Service Bus subscription 过滤规则评估错误时是否死信。"
  type        = bool
  default     = true
}

variable "key_vault_sku_name" {
  description = "prod Key Vault 使用的 SKU。"
  type        = string
  default     = "standard"
}

variable "key_vault_soft_delete_retention_days" {
  description = "prod Key Vault 软删除保留天数。"
  type        = number
  default     = 7
}

variable "log_analytics_daily_quota_gb" {
  description = "prod Log Analytics 每日日志配额，GB。"
  type        = number
  default     = -1
}

variable "log_analytics_local_authentication_enabled" {
  description = "prod Log Analytics 是否启用本地认证。"
  type        = bool
  default     = true
}

variable "container_app_environment_workload_profile_name" {
  description = "prod Container Apps Environment workload profile 名称。"
  type        = string
  default     = null
}

variable "container_app_environment_workload_profile_type" {
  description = "prod Container Apps Environment workload profile 类型。"
  type        = string
  default     = null
}

variable "ghcr_registry_server" {
  description = "prod 私有镜像仓库地址。"
  type        = string
  default     = "ghcr.io"
}

variable "ghcr_registry_username" {
  description = "prod GHCR 拉取镜像使用的用户名。"
  type        = string
}

variable "ghcr_registry_password" {
  description = "prod GHCR 拉取镜像使用的密码或 token。"
  type        = string
  sensitive   = true
}

variable "api_revision_mode" {
  description = "prod API 的 revision 模式。"
  type        = string
  default     = "Single"
}

variable "api_allow_insecure_connections" {
  description = "prod API 是否允许不安全的 HTTP 连接。"
  type        = bool
  default     = false
}

variable "api_ingress_transport" {
  description = "prod API ingress transport。"
  type        = string
  default     = "auto"
}

variable "api_min_replicas" {
  description = "prod API 最小副本数。"
  type        = number
  default     = 1
}

variable "api_max_replicas" {
  description = "prod API 最大副本数。"
  type        = number
  default     = 1
}

variable "web_min_replicas" {
  description = "prod Web 最小副本数。"
  type        = number
  default     = 1
}

variable "web_max_replicas" {
  description = "prod Web 最大副本数。"
  type        = number
  default     = 1
}

variable "worker_min_replicas" {
  description = "prod Worker 最小副本数。"
  type        = number
  default     = 1
}

variable "worker_max_replicas" {
  description = "prod Worker 最大副本数。"
  type        = number
  default     = 1
}

variable "notification_min_replicas" {
  description = "prod NotificationService 最小副本数。"
  type        = number
  default     = 1
}

variable "notification_max_replicas" {
  description = "prod NotificationService 最大副本数。"
  type        = number
  default     = 1
}

variable "migrator_parallelism" {
  description = "prod Migrator Job 并行执行数。"
  type        = number
  default     = 1
}

variable "migrator_replica_timeout_in_seconds" {
  description = "prod Migrator Job 单次执行超时时间。"
  type        = number
  default     = 1800
}

variable "migrator_replica_retry_limit" {
  description = "prod Migrator Job 单次执行失败后的重试次数。"
  type        = number
  default     = 0
}

variable "api_image" {
  description = "prod API 使用的镜像地址。"
  type        = string
}

variable "web_image" {
  description = "prod Web 使用的镜像地址。"
  type        = string
}

variable "web_runtime_api_base_url" {
  description = "prod Web 运行时访问 API 的基础地址。"
  type        = string
  default     = "https://api.prod.example.com"
}

variable "worker_image" {
  description = "prod Worker 使用的镜像地址。"
  type        = string
}

variable "notification_image" {
  description = "prod NotificationService 使用的镜像地址。"
  type        = string
}

variable "migrator_image" {
  description = "prod Migrator Job 使用的镜像地址。"
  type        = string
}
