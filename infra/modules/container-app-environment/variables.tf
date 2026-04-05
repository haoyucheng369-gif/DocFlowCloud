variable "name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "log_analytics_workspace_id" {
  description = "关联的 Log Analytics Workspace 资源 ID。"
  type        = string
}

variable "workload_profile_name" {
  description = "Container Apps Environment workload profile 名称。未传值时不显式配置。"
  type        = string
  default     = null
}

variable "workload_profile_type" {
  description = "Container Apps Environment workload profile 类型。"
  type        = string
  default     = null
}

variable "tags" {
  description = "统一打到资源上的标签。"
  type        = map(string)
  default     = {}
}
