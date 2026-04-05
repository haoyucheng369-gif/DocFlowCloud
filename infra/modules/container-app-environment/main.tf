resource "azurerm_container_app_environment" "this" {
  name                       = var.name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = var.log_analytics_workspace_id
  tags                       = var.tags

  dynamic "workload_profile" {
    for_each = var.workload_profile_name == null || var.workload_profile_type == null ? [] : [1]
    content {
      name                  = var.workload_profile_name
      workload_profile_type = var.workload_profile_type
    }
  }
}
