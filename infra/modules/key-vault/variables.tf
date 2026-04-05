variable "name" {
  description = "Key Vault 名称。必须全局唯一。"
  type        = string
}

variable "resource_group_name" {
  description = "Key Vault 所属的资源组名称。"
  type        = string
}

variable "location" {
  description = "Key Vault 部署的 Azure 区域。"
  type        = string
}

variable "tenant_id" {
  description = "Key Vault 所属租户的 Tenant ID。"
  type        = string
}

variable "sku_name" {
  description = "Key Vault SKU。"
  type        = string
  default     = "standard"
}

variable "soft_delete_retention_days" {
  description = "Key Vault 软删除保留天数。"
  type        = number
  default     = 7
}

variable "tags" {
  description = "统一资源标签。"
  type        = map(string)
  default     = {}
}
