output "server_name" {
  description = "Azure SQL Server 名称。"
  value       = azurerm_mssql_server.this.name
}

output "server_id" {
  description = "Azure SQL Server 资源 ID。"
  value       = azurerm_mssql_server.this.id
}

output "fully_qualified_domain_name" {
  description = "Azure SQL Server 的完整域名。"
  value       = azurerm_mssql_server.this.fully_qualified_domain_name
}

output "database_name" {
  description = "Azure SQL Database 名称。"
  value       = azurerm_mssql_database.this.name
}

output "database_id" {
  description = "Azure SQL Database 资源 ID。"
  value       = azurerm_mssql_database.this.id
}
