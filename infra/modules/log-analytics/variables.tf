variable "name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "sku" {
  description = "Log Analytics Workspace 使用的计费 SKU。"
  type        = string
  default     = "PerGB2018"
}

variable "retention_in_days" {
  description = "日志默认保留天数。"
  type        = number
  default     = 30
}

variable "daily_quota_gb" {
  description = "Log Analytics 每日日志配额，GB。-1 表示不限制。"
  type        = number
  default     = -1
}

variable "local_authentication_enabled" {
  description = "是否启用 Log Analytics 本地认证。"
  type        = bool
  default     = true
}

variable "tags" {
  description = "统一打到资源上的标签。"
  type        = map(string)
  default     = {}
}
