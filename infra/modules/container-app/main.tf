resource "azurerm_container_app" "this" {
  name                         = var.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  revision_mode                = var.revision_mode
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

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

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

      dynamic "liveness_probe" {
        for_each = var.liveness_probe == null ? [] : [var.liveness_probe]
        content {
          transport               = liveness_probe.value.transport
          port                    = liveness_probe.value.port
          path                    = liveness_probe.value.path
          interval_seconds        = liveness_probe.value.interval_seconds
          timeout                 = liveness_probe.value.timeout
          failure_count_threshold = liveness_probe.value.failure_count_threshold
          initial_delay           = liveness_probe.value.initial_delay
        }
      }

      dynamic "readiness_probe" {
        for_each = var.readiness_probe == null ? [] : [var.readiness_probe]
        content {
          transport               = readiness_probe.value.transport
          port                    = readiness_probe.value.port
          path                    = readiness_probe.value.path
          interval_seconds        = readiness_probe.value.interval_seconds
          timeout                 = readiness_probe.value.timeout
          failure_count_threshold = readiness_probe.value.failure_count_threshold
          success_count_threshold = readiness_probe.value.success_count_threshold
          initial_delay           = readiness_probe.value.initial_delay
        }
      }
    }
  }

  dynamic "ingress" {
    for_each = var.target_port == null ? [] : [1]
    content {
      external_enabled           = var.external_ingress_enabled
      allow_insecure_connections = var.allow_insecure_connections
      target_port                = var.target_port
      transport                  = var.transport

      traffic_weight {
        percentage      = 100
        latest_revision = true
      }
    }
  }
}
