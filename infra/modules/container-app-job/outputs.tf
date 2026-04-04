output "name" {
  description = "Container App Job 名称。"
  value       = azurerm_container_app_job.this.name
}

output "id" {
  description = "Container App Job 资源 ID。"
  value       = azurerm_container_app_job.this.id
}

output "principal_id" {
  description = "Container App Job 的 SystemAssigned Managed Identity principal id。"
  value       = try(azurerm_container_app_job.this.identity[0].principal_id, null)
}
