output "resource_group_name" {
  description = "testbed 资源组名称。"
  value       = module.resource_group.name
}

output "log_analytics_name" {
  description = "testbed Log Analytics Workspace 名称。"
  value       = module.log_analytics.name
}

output "container_app_environment_name" {
  description = "testbed Container Apps Environment 名称。"
  value       = module.container_app_environment.name
}

output "sql_server_name" {
  description = "testbed Azure SQL Server 名称。"
  value       = module.sql_database.server_name
}

output "sql_database_name" {
  description = "testbed Azure SQL Database 名称。"
  value       = module.sql_database.database_name
}

output "storage_account_name" {
  description = "testbed Storage Account 名称。"
  value       = module.storage_account.name
}

output "blob_container_name" {
  description = "testbed Blob 容器名称。"
  value       = module.storage_account.blob_container_name
}

output "service_bus_namespace_name" {
  description = "testbed Service Bus Namespace 名称。"
  value       = module.service_bus.namespace_name
}

output "service_bus_topic_name" {
  description = "testbed Service Bus Topic 名称。"
  value       = module.service_bus.topic_name
}

output "key_vault_name" {
  description = "testbed Key Vault 名称。"
  value       = module.key_vault.name
}

output "api_container_app_name" {
  description = "testbed API Container App 名称。"
  value       = module.api_container_app.name
}

output "web_container_app_name" {
  description = "testbed Web Container App 名称。"
  value       = module.web_container_app.name
}

output "worker_container_app_name" {
  description = "testbed Worker Container App 名称。"
  value       = module.worker_container_app.name
}

output "notification_container_app_name" {
  description = "testbed Notification Container App 名称。"
  value       = module.notification_container_app.name
}

output "migrator_job_name" {
  description = "testbed Migrator Job 名称。"
  value       = module.migrator_job.name
}
