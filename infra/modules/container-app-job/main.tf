resource "azurerm_container_app_job" "this" {
  name                         = var.name
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  tags                         = var.tags

  dynamic "identity" {
    for_each = var.enable_system_assigned_identity ? [1] : []
    content {
      type = "SystemAssigned"
    }
  }

  dynamic "secret" {
    for_each = var.key_vault_secret_refs
    content {
      name                = secret.key
      identity            = "System"
      key_vault_secret_id = secret.value
    }
  }

  dynamic "secret" {
    for_each = var.registry_password == null ? [] : [var.registry_password]
    content {
      name  = "registry-password"
      value = secret.value
    }
  }

  dynamic "registry" {
    for_each = var.registry_username == null || var.registry_password == null ? [] : [1]
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = "registry-password"
    }
  }

  replica_timeout_in_seconds = var.replica_timeout_in_seconds
  replica_retry_limit        = var.replica_retry_limit

  manual_trigger_config {
    parallelism = var.parallelism
  }

  template {
    container {
      name   = var.container_name
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      dynamic "env" {
        for_each = var.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.secret_env_vars
        content {
          name        = env.key
          secret_name = env.value
        }
      }
    }
  }
}
