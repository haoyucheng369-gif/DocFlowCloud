resource "azurerm_servicebus_namespace" "this" {
  name                = var.namespace_name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku
  tags                = var.tags
}

resource "azurerm_servicebus_topic" "job_events" {
  name         = var.topic_name
  namespace_id = azurerm_servicebus_namespace.this.id
}

resource "azurerm_servicebus_subscription" "worker" {
  name               = var.worker_subscription_name
  topic_id           = azurerm_servicebus_topic.job_events.id
  max_delivery_count = var.max_delivery_count
}

resource "azurerm_servicebus_subscription" "notification" {
  name               = var.notification_subscription_name
  topic_id           = azurerm_servicebus_topic.job_events.id
  max_delivery_count = var.max_delivery_count
}

resource "azurerm_servicebus_subscription" "api_realtime" {
  name               = var.api_realtime_subscription_name
  topic_id           = azurerm_servicebus_topic.job_events.id
  max_delivery_count = var.max_delivery_count
}
