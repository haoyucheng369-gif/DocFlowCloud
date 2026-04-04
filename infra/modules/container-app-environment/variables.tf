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

variable "tags" {
  description = "统一打到资源上的标签。"
  type        = map(string)
  default     = {}
}
