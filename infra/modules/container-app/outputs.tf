output "name" {
  description = "Container App 名称。"
  value       = azurerm_container_app.this.name
}

output "id" {
  description = "Container App 资源 ID。"
  value       = azurerm_container_app.this.id
}

output "latest_revision_name" {
  description = "最新 revision 名称。"
  value       = azurerm_container_app.this.latest_revision_name
}

output "latest_revision_fqdn" {
  description = "最新 revision 的 FQDN。"
  value       = azurerm_container_app.this.latest_revision_fqdn
}

output "principal_id" {
  description = "Container App 的 SystemAssigned Managed Identity principal id。"
  value       = try(azurerm_container_app.this.identity[0].principal_id, null)
}
