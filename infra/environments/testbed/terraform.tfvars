project_name = "docflow"
location     = "francecentral"

sql_administrator_login          = "docflowadmin"
sql_sku_name                     = "GP_S_Gen5_2"
sql_storage_account_type         = "Local"

storage_account_tier             = "Standard"
storage_account_replication_type = "LRS"
storage_allow_nested_items_to_be_public = false

service_bus_sku                  = "Standard"
service_bus_max_delivery_count   = 10
service_bus_topic_default_message_ttl = "P14D"
service_bus_topic_enable_batched_operations = true
service_bus_subscription_default_message_ttl = "P14D"
service_bus_subscription_auto_delete_on_idle = "P10675198DT2H48M5.477S"
service_bus_subscription_enable_batched_operations = true
service_bus_subscription_dead_lettering_on_filter_evaluation_error = false

key_vault_sku_name               = "standard"
key_vault_soft_delete_retention_days = 90
log_analytics_daily_quota_gb     = 0.024
log_analytics_local_authentication_enabled = true
container_app_environment_workload_profile_name = "Consumption"
container_app_environment_workload_profile_type = "Consumption"
web_runtime_api_base_url         = "https://docflow-api-testbed.icycoast-47c00095.francecentral.azurecontainerapps.io"
api_revision_mode                = "Single"
api_allow_insecure_connections   = false
api_ingress_transport            = "auto"
api_min_replicas                 = 0
api_max_replicas                 = 10
web_min_replicas                 = 0
web_max_replicas                 = 10
worker_min_replicas              = 1
worker_max_replicas              = 1
notification_min_replicas        = 1
notification_max_replicas        = 1
migrator_parallelism             = 1
migrator_replica_timeout_in_seconds = 1800
migrator_replica_retry_limit        = 1
