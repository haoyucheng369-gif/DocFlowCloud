output "name" {
  description = "Key Vault 名称。"
  value       = azurerm_key_vault.this.name
}

output "id" {
  description = "Key Vault 资源 ID。"
  value       = azurerm_key_vault.this.id
}

output "vault_uri" {
  description = "Key Vault URI。"
  value       = azurerm_key_vault.this.vault_uri
}
