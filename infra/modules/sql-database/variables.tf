variable "server_name" {
  description = "Azure SQL Server 的全局唯一名称。"
  type        = string
}

variable "database_name" {
  description = "Azure SQL Database 的名称。"
  type        = string
}

variable "resource_group_name" {
  description = "数据库所属的资源组名称。"
  type        = string
}

variable "location" {
  description = "数据库部署的 Azure 区域。"
  type        = string
}

variable "administrator_login" {
  description = "SQL Server 管理员登录名。"
  type        = string
}

variable "administrator_login_password" {
  description = "SQL Server 管理员密码。"
  type        = string
  sensitive   = true
}

variable "sku_name" {
  description = "数据库 SKU。先保持简单，后面可以再按环境细化。"
  type        = string
  default     = "Basic"
}

variable "storage_account_type" {
  description = "SQL Database 备份存储冗余类型。"
  type        = string
  default     = "Geo"
}

variable "tags" {
  description = "统一资源标签。"
  type        = map(string)
  default     = {}
}
