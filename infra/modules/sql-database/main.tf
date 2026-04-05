resource "azurerm_mssql_server" "this" {
  name                         = var.server_name
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.administrator_login
  administrator_login_password = var.administrator_login_password
  minimum_tls_version          = "1.2"
  tags                         = var.tags

  lifecycle {
    ignore_changes = [
      administrator_login_password,
    ]
  }
}

resource "azurerm_mssql_database" "this" {
  name                 = var.database_name
  server_id            = azurerm_mssql_server.this.id
  sku_name             = var.sku_name
  storage_account_type = var.storage_account_type
  tags                 = var.tags
}
