variable "project_name" {
  description = "命名规则里使用的项目短前缀。"
  type        = string
  default     = "docflow"
}

variable "location" {
  description = "环境资源使用的 Azure 区域。"
  type        = string
  default     = "francecentral"
}
