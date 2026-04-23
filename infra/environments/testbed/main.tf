data "azurerm_client_config" "current" {}

module "resource_group" {
  source   = "../../modules/resource-group"
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags
}

module "log_analytics" {
  source                       = "../../modules/log-analytics"
  name                         = local.log_analytics_name
  location                     = var.location
  resource_group_name          = module.resource_group.name
  daily_quota_gb               = var.log_analytics_daily_quota_gb
  local_authentication_enabled = var.log_analytics_local_authentication_enabled
  tags                         = local.tags
}

module "container_app_environment" {
  source                     = "../../modules/container-app-environment"
  name                       = local.container_app_environment_name
  location                   = var.location
  resource_group_name        = module.resource_group.name
  log_analytics_workspace_id = module.log_analytics.id
  workload_profile_name      = var.container_app_environment_workload_profile_name
  workload_profile_type      = var.container_app_environment_workload_profile_type
  tags                       = local.tags
}

module "sql_database" {
  source                       = "../../modules/sql-database"
  server_name                  = local.sql_server_name
  database_name                = local.sql_database_name
  resource_group_name          = module.resource_group.name
  location                     = var.location
  administrator_login          = var.sql_administrator_login
  administrator_login_password = var.sql_administrator_login_password
  sku_name                     = var.sql_sku_name
  storage_account_type         = var.sql_storage_account_type
  min_capacity                 = var.sql_min_capacity
  auto_pause_delay_in_minutes  = var.sql_auto_pause_delay_in_minutes
  tags                         = local.tags
}

module "storage_account" {
  source                          = "../../modules/storage-account"
  name                            = local.storage_account_name
  blob_container_name             = local.blob_container_name
  resource_group_name             = module.resource_group.name
  location                        = var.location
  account_tier                    = var.storage_account_tier
  account_replication_type        = var.storage_account_replication_type
  allow_nested_items_to_be_public = var.storage_allow_nested_items_to_be_public
  tags                            = local.tags
}

module "service_bus" {
  source                                                 = "../../modules/service-bus"
  namespace_name                                         = local.service_bus_namespace_name
  topic_name                                             = local.service_bus_topic_name
  worker_subscription_name                               = local.service_bus_worker_subscription
  notification_subscription_name                         = local.service_bus_notification_subscription
  api_realtime_subscription_name                         = local.service_bus_api_realtime_subscription
  resource_group_name                                    = module.resource_group.name
  location                                               = var.location
  sku                                                    = var.service_bus_sku
  max_delivery_count                                     = var.service_bus_max_delivery_count
  topic_default_message_ttl                              = var.service_bus_topic_default_message_ttl
  topic_enable_batched_operations                        = var.service_bus_topic_enable_batched_operations
  subscription_default_message_ttl                       = var.service_bus_subscription_default_message_ttl
  subscription_auto_delete_on_idle                       = var.service_bus_subscription_auto_delete_on_idle
  subscription_enable_batched_operations                 = var.service_bus_subscription_enable_batched_operations
  subscription_dead_lettering_on_filter_evaluation_error = var.service_bus_subscription_dead_lettering_on_filter_evaluation_error
  tags                                                   = local.tags
}

module "key_vault" {
  source                     = "../../modules/key-vault"
  name                       = local.key_vault_name
  resource_group_name        = module.resource_group.name
  location                   = var.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = var.key_vault_sku_name
  soft_delete_retention_days = var.key_vault_soft_delete_retention_days
  tags                       = local.tags
}

resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = local.sql_connection_secret_name
  value        = var.sql_connection_string
  key_vault_id = module.key_vault.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "blob_connection_string" {
  name         = local.blob_connection_secret_name
  value        = var.blob_connection_string
  key_vault_id = module.key_vault.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "service_bus_connection_string" {
  name         = local.service_bus_connection_secret_name
  value        = var.service_bus_connection_string
  key_vault_id = module.key_vault.id
  tags         = local.tags
}

module "api_container_app" {
  source                          = "../../modules/container-app"
  name                            = local.api_container_app_name
  resource_group_name             = module.resource_group.name
  container_app_environment_id    = module.container_app_environment.id
  external_ingress_enabled        = true
  allow_insecure_connections      = var.api_allow_insecure_connections
  target_port                     = 8080
  transport                       = var.api_ingress_transport
  container_name                  = local.api_container_app_name
  image                           = var.api_image
  enable_system_assigned_identity = true
  revision_mode                   = var.api_revision_mode
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  min_replicas                    = var.api_min_replicas
  max_replicas                    = var.api_max_replicas
  env_entries                     = local.api_env_entries
  liveness_probe                  = local.api_liveness_probe
  readiness_probe                 = local.api_readiness_probe
  startup_probe                   = local.api_startup_probe
  key_vault_secret_refs = {
    (local.sql_connection_secret_name)         = azurerm_key_vault_secret.sql_connection_string.id
    (local.blob_connection_secret_name)        = azurerm_key_vault_secret.blob_connection_string.id
    (local.service_bus_connection_secret_name) = azurerm_key_vault_secret.service_bus_connection_string.id
  }
  tags = local.tags
}

module "web_container_app" {
  source                       = "../../modules/container-app"
  name                         = local.web_container_app_name
  resource_group_name          = module.resource_group.name
  container_app_environment_id = module.container_app_environment.id
  external_ingress_enabled     = true
  target_port                  = 80
  container_name               = local.web_container_app_name
  image                        = var.web_image
  registry_server              = var.ghcr_registry_server
  registry_username            = var.ghcr_registry_username
  registry_password            = var.ghcr_registry_password
  min_replicas                 = var.web_min_replicas
  max_replicas                 = var.web_max_replicas
  env_vars                     = local.web_env_vars
  tags                         = local.tags
}

module "worker_container_app" {
  source                          = "../../modules/container-app"
  name                            = local.worker_container_app_name
  resource_group_name             = module.resource_group.name
  container_app_environment_id    = module.container_app_environment.id
  external_ingress_enabled        = false
  target_port                     = null
  container_name                  = local.worker_container_app_name
  image                           = var.worker_image
  enable_system_assigned_identity = true
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  min_replicas                    = var.worker_min_replicas
  max_replicas                    = var.worker_max_replicas
  env_entries                     = local.worker_env_entries
  key_vault_secret_refs = {
    (local.sql_connection_secret_name)         = azurerm_key_vault_secret.sql_connection_string.id
    (local.blob_connection_secret_name)        = azurerm_key_vault_secret.blob_connection_string.id
    (local.service_bus_connection_secret_name) = azurerm_key_vault_secret.service_bus_connection_string.id
  }
  tags = local.tags
}

module "notification_container_app" {
  source                          = "../../modules/container-app"
  name                            = local.notification_container_app_name
  resource_group_name             = module.resource_group.name
  container_app_environment_id    = module.container_app_environment.id
  external_ingress_enabled        = false
  target_port                     = null
  container_name                  = local.notification_container_app_name
  image                           = var.notification_image
  enable_system_assigned_identity = true
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  min_replicas                    = var.notification_min_replicas
  max_replicas                    = var.notification_max_replicas
  env_entries                     = local.notification_env_entries
  key_vault_secret_refs = {
    (local.sql_connection_secret_name)         = azurerm_key_vault_secret.sql_connection_string.id
    (local.blob_connection_secret_name)        = azurerm_key_vault_secret.blob_connection_string.id
    (local.service_bus_connection_secret_name) = azurerm_key_vault_secret.service_bus_connection_string.id
  }
  tags = local.tags
}

module "migrator_job" {
  source                          = "../../modules/container-app-job"
  name                            = local.migrator_job_name
  resource_group_name             = module.resource_group.name
  location                        = var.location
  container_app_environment_id    = module.container_app_environment.id
  container_name                  = local.migrator_job_name
  image                           = var.migrator_image
  cpu                             = 0.25
  memory                          = "0.5Gi"
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  env_entries                     = local.migrator_env_entries
  enable_system_assigned_identity = true
  parallelism                     = var.migrator_parallelism
  replica_timeout_in_seconds      = var.migrator_replica_timeout_in_seconds
  replica_retry_limit             = var.migrator_replica_retry_limit
  key_vault_secret_refs = {
    (local.sql_connection_secret_name) = azurerm_key_vault_secret.sql_connection_string.id
  }
  tags = local.tags
}

resource "azurerm_role_assignment" "api_key_vault_secrets_user" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.api_container_app.principal_id
}

resource "azurerm_role_assignment" "worker_key_vault_secrets_user" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.worker_container_app.principal_id
}

resource "azurerm_role_assignment" "notification_key_vault_secrets_user" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.notification_container_app.principal_id
}

resource "azurerm_role_assignment" "migrator_key_vault_secrets_user" {
  count                = 1
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.migrator_job.principal_id
}
