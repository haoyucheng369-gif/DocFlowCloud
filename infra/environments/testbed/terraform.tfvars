project_name = "docflow"
location     = "francecentral"

sql_administrator_login          = "docflowadmin"
sql_sku_name                     = "Basic"

storage_account_tier             = "Standard"
storage_account_replication_type = "LRS"

service_bus_sku                  = "Standard"
service_bus_max_delivery_count   = 10

key_vault_sku_name               = "standard"
web_runtime_api_base_url         = "https://api.testbed.example.com"
api_revision_mode                = "Single"
api_allow_insecure_connections   = false
api_ingress_transport            = "auto"
worker_min_replicas              = 1
worker_max_replicas              = 1
notification_min_replicas        = 1
notification_max_replicas        = 1
migrator_parallelism             = 1
migrator_replica_timeout_in_seconds = 1800
migrator_replica_retry_limit        = 0
