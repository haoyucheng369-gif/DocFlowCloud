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

variable "topic_default_message_ttl" {
  description = "Topic 默认消息 TTL。"
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "topic_enable_batched_operations" {
  description = "Topic 是否启用 batched operations。"
  type        = bool
  default     = false
}

variable "subscription_default_message_ttl" {
  description = "Subscription 默认消息 TTL。"
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "subscription_auto_delete_on_idle" {
  description = "Subscription 自动删除闲置时间。"
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "subscription_enable_batched_operations" {
  description = "Subscription 是否启用 batched operations。"
  type        = bool
  default     = false
}

variable "subscription_dead_lettering_on_filter_evaluation_error" {
  description = "Subscription 过滤规则评估错误时是否死信。"
  type        = bool
  default     = true
}

variable "tags" {
  description = "统一资源标签。"
  type        = map(string)
  default     = {}
}
