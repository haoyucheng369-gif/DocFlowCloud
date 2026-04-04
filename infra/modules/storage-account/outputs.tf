output "name" {
  description = "Storage Account 名称。"
  value       = azurerm_storage_account.this.name
}

output "id" {
  description = "Storage Account 资源 ID。"
  value       = azurerm_storage_account.this.id
}

output "primary_blob_endpoint" {
  description = "主 Blob Endpoint。"
  value       = azurerm_storage_account.this.primary_blob_endpoint
}

output "blob_container_name" {
  description = "Blob 容器名称。"
  value       = azurerm_storage_container.uploads.name
}
