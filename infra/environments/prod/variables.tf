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

variable "key_vault_sku_name" {
  description = "prod Key Vault 使用的 SKU。"
  type        = string
  default     = "standard"
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
