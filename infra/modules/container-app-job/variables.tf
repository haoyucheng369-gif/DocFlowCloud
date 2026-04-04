variable "name" {
  description = "Container App Job 名称。"
  type        = string
}

variable "resource_group_name" {
  description = "Container App Job 所属的资源组名称。"
  type        = string
}

variable "location" {
  description = "Container App Job 部署的 Azure 区域。"
  type        = string
}

variable "container_app_environment_id" {
  description = "Container Apps Environment 资源 ID。"
  type        = string
}

variable "trigger_type" {
  description = "Job 触发类型。当前先使用 Manual。"
  type        = string
  default     = "Manual"
}

variable "container_name" {
  description = "Job 内部容器名称。"
  type        = string
}

variable "image" {
  description = "Job 使用的镜像地址。"
  type        = string
}

variable "cpu" {
  description = "Job 容器 CPU 配额。"
  type        = number
  default     = 0.5
}

variable "memory" {
  description = "Job 容器内存配额。"
  type        = string
  default     = "1Gi"
}

variable "env_vars" {
  description = "Job 使用的非敏感环境变量。"
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "Job 使用 secretref 的环境变量，key 是 env 名称，value 是 secret 名称。"
  type        = map(string)
  default     = {}
}

variable "key_vault_secret_refs" {
  description = "Job 内部的 Key Vault secret 引用，key 是 secret 名称，value 是 Key Vault Secret ID。"
  type        = map(string)
  default     = {}
}

variable "enable_system_assigned_identity" {
  description = "是否启用 SystemAssigned Managed Identity。"
  type        = bool
  default     = false
}

variable "registry_server" {
  description = "私有镜像仓库地址。"
  type        = string
  default     = "ghcr.io"
}

variable "registry_username" {
  description = "私有镜像仓库用户名。未传值时表示不配置镜像仓库认证。"
  type        = string
  default     = null
}

variable "registry_password" {
  description = "私有镜像仓库密码或 token。"
  type        = string
  default     = null
  sensitive   = true
}

variable "parallelism" {
  description = "并行执行数。"
  type        = number
  default     = 1
}

variable "replica_timeout_in_seconds" {
  description = "单次执行超时时间。"
  type        = number
  default     = 1800
}

variable "replica_retry_limit" {
  description = "单次执行失败后的重试次数。"
  type        = number
  default     = 0
}

variable "tags" {
  description = "统一资源标签。"
  type        = map(string)
  default     = {}
}
