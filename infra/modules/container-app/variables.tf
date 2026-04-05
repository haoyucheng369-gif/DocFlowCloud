variable "name" {
  description = "Container App 名称。"
  type        = string
}

variable "resource_group_name" {
  description = "Container App 所属的资源组名称。"
  type        = string
}

variable "container_app_environment_id" {
  description = "Container Apps Environment 资源 ID。"
  type        = string
}

variable "revision_mode" {
  description = "Revision 模式。默认单 revision。"
  type        = string
  default     = "Single"
}

variable "external_ingress_enabled" {
  description = "是否启用外部 ingress。"
  type        = bool
  default     = false
}

variable "allow_insecure_connections" {
  description = "是否允许不安全的 HTTP 连接。"
  type        = bool
  default     = false
}

variable "target_port" {
  description = "应用对外监听端口。未启用 ingress 时可为 null。"
  type        = number
  default     = null
}

variable "transport" {
  description = "Ingress transport。"
  type        = string
  default     = "auto"
}

variable "container_name" {
  description = "容器名称。"
  type        = string
}

variable "image" {
  description = "容器镜像地址。"
  type        = string
}

variable "cpu" {
  description = "容器 CPU 配额。"
  type        = number
  default     = 0.5
}

variable "memory" {
  description = "容器内存配额，Azure Container Apps 使用字符串格式，例如 1Gi。"
  type        = string
  default     = "1Gi"
}

variable "min_replicas" {
  description = "最小副本数。"
  type        = number
  default     = 1
}

variable "max_replicas" {
  description = "最大副本数。"
  type        = number
  default     = 1
}

variable "env_vars" {
  description = "非敏感环境变量。"
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "使用 secretref 的环境变量，key 是 env 名称，value 是 secret 名称。"
  type        = map(string)
  default     = {}
}

variable "env_entries" {
  description = "按顺序声明的环境变量列表，用于和现网对齐 env 顺序。"
  type = list(object({
    name        = string
    value       = optional(string)
    secret_name = optional(string)
  }))
  default = []
}

variable "key_vault_secret_refs" {
  description = "Container App 内部的 Key Vault secret 引用，key 是 secret 名称，value 是 Key Vault Secret ID。"
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

variable "liveness_probe" {
  description = "容器 liveness probe 配置。未传值时不配置。"
  type = object({
    transport               = string
    port                    = number
    path                    = string
    interval_seconds        = number
    timeout                 = number
    failure_count_threshold = number
    initial_delay           = number
  })
  default = null
}

variable "readiness_probe" {
  description = "容器 readiness probe 配置。未传值时不配置。"
  type = object({
    transport               = string
    port                    = number
    path                    = string
    interval_seconds        = number
    timeout                 = number
    failure_count_threshold = number
    success_count_threshold = number
    initial_delay           = number
  })
  default = null
}

variable "startup_probe" {
  description = "容器 startup probe 配置。未传值时不配置。"
  type = object({
    transport               = string
    port                    = number
    path                    = optional(string)
    interval_seconds        = number
    timeout                 = number
    failure_count_threshold = number
    success_count_threshold = number
    initial_delay           = number
  })
  default = null
}

variable "tags" {
  description = "统一资源标签。"
  type        = map(string)
  default     = {}
}
