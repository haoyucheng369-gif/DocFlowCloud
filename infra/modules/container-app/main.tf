resource "azurerm_container_app" "this" {
  name                         = var.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  revision_mode                = var.revision_mode
  tags                         = var.tags

  lifecycle {
    ignore_changes = [
      max_inactive_revisions,
      secret,
      workload_profile_name,
    ]
  }

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
        for_each = length(var.env_entries) > 0 ? var.env_entries : concat(
          [for k, v in var.env_vars : { name = k, value = v, secret_name = null }],
          [for k, v in var.secret_env_vars : { name = k, value = null, secret_name = v }]
        )
        content {
          name        = env.value.name
          value       = try(env.value.value, null)
          secret_name = try(env.value.secret_name, null)
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

      dynamic "startup_probe" {
        for_each = var.startup_probe == null ? [] : [var.startup_probe]
        content {
          transport               = startup_probe.value.transport
          port                    = startup_probe.value.port
          path                    = try(startup_probe.value.path, null)
          interval_seconds        = startup_probe.value.interval_seconds
          timeout                 = startup_probe.value.timeout
          failure_count_threshold = startup_probe.value.failure_count_threshold
          initial_delay           = startup_probe.value.initial_delay
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
