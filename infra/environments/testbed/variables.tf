variable "location" {
  description = "testbed 环境使用的 Azure 区域。"
  type        = string
  default     = "francecentral"
}

variable "project_name" {
  description = "命名规则里使用的项目短前缀。"
  type        = string
  default     = "docflow"
}

variable "sql_administrator_login" {
  description = "testbed Azure SQL Server 的管理员用户名。"
  type        = string
  default     = "docflowadmin"
}

variable "sql_administrator_login_password" {
  description = "testbed Azure SQL Server 的管理员密码。"
  type        = string
  sensitive   = true
}

variable "sql_connection_string" {
  description = "testbed 运行时使用的 SQL 连接字符串。"
  type        = string
  sensitive   = true
}

variable "blob_connection_string" {
  description = "testbed 运行时使用的 Blob connection string。"
  type        = string
  sensitive   = true
}

variable "service_bus_connection_string" {
  description = "testbed 运行时使用的 Service Bus connection string。"
  type        = string
  sensitive   = true
}

variable "sql_sku_name" {
  description = "testbed Azure SQL Database 使用的 SKU。"
  type        = string
  default     = "Basic"
}

variable "storage_account_tier" {
  description = "testbed Storage Account 性能层。"
  type        = string
  default     = "Standard"
}

variable "storage_account_replication_type" {
  description = "testbed Storage Account 副本类型。"
  type        = string
  default     = "LRS"
}

variable "service_bus_sku" {
  description = "testbed Service Bus Namespace 使用的 SKU。"
  type        = string
  default     = "Standard"
}

variable "service_bus_max_delivery_count" {
  description = "testbed Service Bus subscription 的最大投递次数。"
  type        = number
  default     = 10
}

variable "key_vault_sku_name" {
  description = "testbed Key Vault 使用的 SKU。"
  type        = string
  default     = "standard"
}

variable "ghcr_registry_server" {
  description = "testbed 私有镜像仓库地址。"
  type        = string
  default     = "ghcr.io"
}

variable "ghcr_registry_username" {
  description = "testbed GHCR 拉取镜像使用的用户名。"
  type        = string
}

variable "ghcr_registry_password" {
  description = "testbed GHCR 拉取镜像使用的密码或 token。"
  type        = string
  sensitive   = true
}

variable "api_revision_mode" {
  description = "testbed API 的 revision 模式。"
  type        = string
  default     = "Single"
}

variable "api_allow_insecure_connections" {
  description = "testbed API 是否允许不安全的 HTTP 连接。"
  type        = bool
  default     = false
}

variable "api_ingress_transport" {
  description = "testbed API ingress transport。"
  type        = string
  default     = "auto"
}

variable "worker_min_replicas" {
  description = "testbed Worker 最小副本数。"
  type        = number
  default     = 1
}

variable "worker_max_replicas" {
  description = "testbed Worker 最大副本数。"
  type        = number
  default     = 1
}

variable "notification_min_replicas" {
  description = "testbed NotificationService 最小副本数。"
  type        = number
  default     = 1
}

variable "notification_max_replicas" {
  description = "testbed NotificationService 最大副本数。"
  type        = number
  default     = 1
}

variable "migrator_parallelism" {
  description = "testbed Migrator Job 并行执行数。"
  type        = number
  default     = 1
}

variable "migrator_replica_timeout_in_seconds" {
  description = "testbed Migrator Job 单次执行超时时间。"
  type        = number
  default     = 1800
}

variable "migrator_replica_retry_limit" {
  description = "testbed Migrator Job 单次执行失败后的重试次数。"
  type        = number
  default     = 0
}

variable "api_image" {
  description = "testbed API 使用的镜像地址。"
  type        = string
}

variable "web_image" {
  description = "testbed Web 使用的镜像地址。"
  type        = string
}

variable "web_runtime_api_base_url" {
  description = "testbed Web 运行时访问 API 的基础地址。"
  type        = string
  default     = "https://api.testbed.example.com"
}

variable "worker_image" {
  description = "testbed Worker 使用的镜像地址。"
  type        = string
}

variable "notification_image" {
  description = "testbed NotificationService 使用的镜像地址。"
  type        = string
}

variable "migrator_image" {
  description = "testbed Migrator Job 使用的镜像地址。"
  type        = string
}
