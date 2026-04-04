variable "name" {
  description = "Storage Account 名称。必须全局唯一且满足 Azure 命名规则。"
  type        = string
}

variable "blob_container_name" {
  description = "应用上传和结果文件使用的 Blob 容器名称。"
  type        = string
}

variable "resource_group_name" {
  description = "Storage Account 所属的资源组名称。"
  type        = string
}

variable "location" {
  description = "Storage Account 部署的 Azure 区域。"
  type        = string
}

variable "account_tier" {
  description = "Storage Account 性能层。"
  type        = string
  default     = "Standard"
}

variable "account_replication_type" {
  description = "Storage Account 副本类型。"
  type        = string
  default     = "LRS"
}

variable "tags" {
  description = "统一资源标签。"
  type        = map(string)
  default     = {}
}
