data "azurerm_client_config" "current" {}

module "resource_group" {
  source   = "../../modules/resource-group"
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags
}

module "log_analytics" {
  source              = "../../modules/log-analytics"
  name                = local.log_analytics_name
  location            = var.location
  resource_group_name = module.resource_group.name
  tags                = local.tags
}

module "container_app_environment" {
  source                     = "../../modules/container-app-environment"
  name                       = local.container_app_environment_name
  location                   = var.location
  resource_group_name        = module.resource_group.name
  log_analytics_workspace_id = module.log_analytics.id
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
  tags                         = local.tags
}

module "storage_account" {
  source                   = "../../modules/storage-account"
  name                     = local.storage_account_name
  blob_container_name      = local.blob_container_name
  resource_group_name      = module.resource_group.name
  location                 = var.location
  account_tier             = var.storage_account_tier
  account_replication_type = var.storage_account_replication_type
  tags                     = local.tags
}

module "service_bus" {
  source                         = "../../modules/service-bus"
  namespace_name                 = local.service_bus_namespace_name
  topic_name                     = local.service_bus_topic_name
  worker_subscription_name       = local.service_bus_worker_subscription
  notification_subscription_name = local.service_bus_notification_subscription
  api_realtime_subscription_name = local.service_bus_api_realtime_subscription
  resource_group_name            = module.resource_group.name
  location                       = var.location
  sku                            = var.service_bus_sku
  max_delivery_count             = var.service_bus_max_delivery_count
  tags                           = local.tags
}

module "key_vault" {
  source              = "../../modules/key-vault"
  name                = local.key_vault_name
  resource_group_name = module.resource_group.name
  location            = var.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = var.key_vault_sku_name
  tags                = local.tags
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
  container_name                  = "api"
  image                           = var.api_image
  enable_system_assigned_identity = true
  revision_mode                   = var.api_revision_mode
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  min_replicas                    = 1
  max_replicas                    = 1
  env_vars                        = local.api_env_vars
  secret_env_vars                 = local.app_secret_env_vars
  liveness_probe                  = local.api_liveness_probe
  readiness_probe                 = local.api_readiness_probe
  key_vault_secret_refs = {
    (local.sql_connection_secret_name)         = azurerm_key_vault_secret.sql_connection_string.versionless_id
    (local.blob_connection_secret_name)        = azurerm_key_vault_secret.blob_connection_string.versionless_id
    (local.service_bus_connection_secret_name) = azurerm_key_vault_secret.service_bus_connection_string.versionless_id
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
  container_name               = "web"
  image                        = var.web_image
  registry_server              = var.ghcr_registry_server
  registry_username            = var.ghcr_registry_username
  registry_password            = var.ghcr_registry_password
  min_replicas                 = 1
  max_replicas                 = 1
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
  container_name                  = "worker"
  image                           = var.worker_image
  enable_system_assigned_identity = true
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  min_replicas                    = var.worker_min_replicas
  max_replicas                    = var.worker_max_replicas
  env_vars                        = local.worker_env_vars
  secret_env_vars                 = local.app_secret_env_vars
  key_vault_secret_refs = {
    (local.sql_connection_secret_name)         = azurerm_key_vault_secret.sql_connection_string.versionless_id
    (local.blob_connection_secret_name)        = azurerm_key_vault_secret.blob_connection_string.versionless_id
    (local.service_bus_connection_secret_name) = azurerm_key_vault_secret.service_bus_connection_string.versionless_id
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
  container_name                  = "notification"
  image                           = var.notification_image
  enable_system_assigned_identity = true
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  min_replicas                    = var.notification_min_replicas
  max_replicas                    = var.notification_max_replicas
  env_vars                        = local.notification_env_vars
  secret_env_vars                 = local.app_secret_env_vars
  key_vault_secret_refs = {
    (local.sql_connection_secret_name)         = azurerm_key_vault_secret.sql_connection_string.versionless_id
    (local.blob_connection_secret_name)        = azurerm_key_vault_secret.blob_connection_string.versionless_id
    (local.service_bus_connection_secret_name) = azurerm_key_vault_secret.service_bus_connection_string.versionless_id
  }
  tags = local.tags
}

module "migrator_job" {
  source                          = "../../modules/container-app-job"
  name                            = local.migrator_job_name
  resource_group_name             = module.resource_group.name
  location                        = var.location
  container_app_environment_id    = module.container_app_environment.id
  container_name                  = "migrator"
  image                           = var.migrator_image
  registry_server                 = var.ghcr_registry_server
  registry_username               = var.ghcr_registry_username
  registry_password               = var.ghcr_registry_password
  env_vars                        = local.migrator_env_vars
  secret_env_vars                 = local.app_secret_env_vars
  enable_system_assigned_identity = true
  parallelism                     = var.migrator_parallelism
  replica_timeout_in_seconds      = var.migrator_replica_timeout_in_seconds
  replica_retry_limit             = var.migrator_replica_retry_limit
  key_vault_secret_refs = {
    (local.sql_connection_secret_name)         = azurerm_key_vault_secret.sql_connection_string.versionless_id
    (local.blob_connection_secret_name)        = azurerm_key_vault_secret.blob_connection_string.versionless_id
    (local.service_bus_connection_secret_name) = azurerm_key_vault_secret.service_bus_connection_string.versionless_id
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
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.migrator_job.principal_id
}
