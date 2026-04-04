variable "namespace_name" {
  description = "Service Bus Namespace 名称。"
  type        = string
}

variable "topic_name" {
  description = "业务事件 Topic 名称。"
  type        = string
}

variable "worker_subscription_name" {
  description = "worker 使用的 subscription 名称。"
  type        = string
}

variable "notification_subscription_name" {
  description = "notification 使用的 subscription 名称。"
  type        = string
}

variable "api_realtime_subscription_name" {
  description = "API 实时状态更新使用的 subscription 名称。"
  type        = string
}

variable "resource_group_name" {
  description = "Service Bus 所属的资源组名称。"
  type        = string
}

variable "location" {
  description = "Service Bus 部署的 Azure 区域。"
  type        = string
}

variable "sku" {
  description = "Service Bus SKU。"
  type        = string
  default     = "Standard"
}

variable "max_delivery_count" {
  description = "Subscription 进入死信前允许的最大投递次数。"
  type        = number
  default     = 10
}

variable "tags" {
  description = "统一资源标签。"
  type        = map(string)
  default     = {}
}
