output "namespace_name" {
  description = "Service Bus Namespace 名称。"
  value       = azurerm_servicebus_namespace.this.name
}

output "namespace_id" {
  description = "Service Bus Namespace 资源 ID。"
  value       = azurerm_servicebus_namespace.this.id
}

output "topic_name" {
  description = "Service Bus Topic 名称。"
  value       = azurerm_servicebus_topic.job_events.name
}

output "worker_subscription_name" {
  description = "worker subscription 名称。"
  value       = azurerm_servicebus_subscription.worker.name
}

output "notification_subscription_name" {
  description = "notification subscription 名称。"
  value       = azurerm_servicebus_subscription.notification.name
}

output "api_realtime_subscription_name" {
  description = "api realtime subscription 名称。"
  value       = azurerm_servicebus_subscription.api_realtime.name
}
