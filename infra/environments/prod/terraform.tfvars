project_name = "docflow"
location     = "francecentral"

sql_administrator_login          = "docflowadmin"
sql_sku_name                     = "Basic"
sql_storage_account_type         = "Geo"

storage_account_tier             = "Standard"
storage_account_replication_type = "LRS"
storage_allow_nested_items_to_be_public = true

service_bus_sku                  = "Standard"
service_bus_max_delivery_count   = 10
service_bus_topic_default_message_ttl = "P10675199DT2H48M5.4775807S"
service_bus_topic_enable_batched_operations = false
service_bus_subscription_default_message_ttl = "P10675199DT2H48M5.4775807S"
service_bus_subscription_auto_delete_on_idle = "P10675199DT2H48M5.4775807S"
service_bus_subscription_enable_batched_operations = false
service_bus_subscription_dead_lettering_on_filter_evaluation_error = true

key_vault_sku_name               = "standard"
key_vault_soft_delete_retention_days = 7
log_analytics_daily_quota_gb     = -1
log_analytics_local_authentication_enabled = true
container_app_environment_workload_profile_name = null
container_app_environment_workload_profile_type = null
web_runtime_api_base_url         = "https://api.prod.example.com"
api_revision_mode                = "Single"
api_allow_insecure_connections   = false
api_ingress_transport            = "auto"
api_min_replicas                 = 1
api_max_replicas                 = 1
web_min_replicas                 = 1
web_max_replicas                 = 1
worker_min_replicas              = 1
worker_max_replicas              = 1
notification_min_replicas        = 1
notification_max_replicas        = 1
migrator_parallelism             = 1
migrator_replica_timeout_in_seconds = 1800
migrator_replica_retry_limit        = 0
