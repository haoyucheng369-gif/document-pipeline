variable "location" {
  description = "Azure region for prod resources."
  type        = string
  default     = "francecentral"
}

variable "project_name" {
  description = "Configuration value for project name."
  type        = string
  default     = "docflow"
}

variable "sql_administrator_login" {
  description = "Configuration value for SQL administrator login."
  type        = string
  default     = "docflowadmin"
}

variable "sql_administrator_login_password" {
  description = "Configuration value for SQL administrator login password."
  type        = string
  sensitive   = true
}

variable "sql_connection_string" {
  description = "SQL connection string used by the prod runtime."
  type        = string
  sensitive   = true
}

variable "blob_connection_string" {
  description = "Blob Storage connection string used by the prod runtime."
  type        = string
  sensitive   = true
}

variable "service_bus_connection_string" {
  description = "Service Bus connection string used by the prod runtime."
  type        = string
  sensitive   = true
}

variable "sql_sku_name" {
  description = "Configuration value for SQL SKU name."
  type        = string
  default     = "Basic"
}

variable "sql_storage_account_type" {
  description = "Configuration value for SQL storage account type."
  type        = string
  default     = "Geo"
}

variable "storage_account_tier" {
  description = "Configuration value for storage account tier."
  type        = string
  default     = "Standard"
}

variable "storage_account_replication_type" {
  description = "Configuration value for storage account replication type."
  type        = string
  default     = "LRS"
}

variable "storage_allow_nested_items_to_be_public" {
  description = "Configuration value for storage allow nested items to be public."
  type        = bool
  default     = true
}

variable "service_bus_sku" {
  description = "Configuration value for service bus SKU."
  type        = string
  default     = "Standard"
}

variable "service_bus_max_delivery_count" {
  description = "Configuration value for service bus max delivery count."
  type        = number
  default     = 10
}

variable "service_bus_topic_default_message_ttl" {
  description = "Configuration value for service bus topic default message TTL."
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "service_bus_topic_enable_batched_operations" {
  description = "Configuration value for service bus topic enable batched operations."
  type        = bool
  default     = false
}

variable "service_bus_subscription_default_message_ttl" {
  description = "Configuration value for service bus subscription default message TTL."
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "service_bus_subscription_auto_delete_on_idle" {
  description = "Configuration value for service bus subscription auto delete on idle."
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "service_bus_subscription_enable_batched_operations" {
  description = "Configuration value for service bus subscription enable batched operations."
  type        = bool
  default     = false
}

variable "service_bus_subscription_dead_lettering_on_filter_evaluation_error" {
  description = "Configuration value for service bus subscription dead lettering on filter evaluation error."
  type        = bool
  default     = true
}

variable "key_vault_sku_name" {
  description = "Configuration value for key vault SKU name."
  type        = string
  default     = "standard"
}

variable "key_vault_soft_delete_retention_days" {
  description = "Configuration value for key vault soft delete retention days."
  type        = number
  default     = 7
}

variable "log_analytics_daily_quota_gb" {
  description = "Configuration value for log analytics daily quota gb."
  type        = number
  default     = -1
}

variable "log_analytics_local_authentication_enabled" {
  description = "Configuration value for log analytics local authentication enabled."
  type        = bool
  default     = true
}

variable "container_app_environment_workload_profile_name" {
  description = "Configuration value for container app environment workload profile name."
  type        = string
  default     = null
}

variable "container_app_environment_workload_profile_type" {
  description = "Configuration value for container app environment workload profile type."
  type        = string
  default     = null
}

variable "ghcr_registry_server" {
  description = "Configuration value for GHCR registry server."
  type        = string
  default     = "ghcr.io"
}

variable "ghcr_registry_username" {
  description = "Configuration value for GHCR registry username."
  type        = string
}

variable "ghcr_registry_password" {
  description = "Configuration value for GHCR registry password."
  type        = string
  sensitive   = true
}

variable "api_revision_mode" {
  description = "Configuration value for API revision mode."
  type        = string
  default     = "Single"
}

variable "api_allow_insecure_connections" {
  description = "Configuration value for API allow insecure connections."
  type        = bool
  default     = false
}

variable "api_ingress_transport" {
  description = "Configuration value for API ingress transport."
  type        = string
  default     = "auto"
}

variable "api_min_replicas" {
  description = "Configuration value for API min replicas."
  type        = number
  default     = 1
}

variable "api_max_replicas" {
  description = "Configuration value for API max replicas."
  type        = number
  default     = 1
}

variable "web_min_replicas" {
  description = "Configuration value for web min replicas."
  type        = number
  default     = 1
}

variable "web_max_replicas" {
  description = "Configuration value for web max replicas."
  type        = number
  default     = 1
}

variable "worker_min_replicas" {
  description = "Configuration value for worker min replicas."
  type        = number
  default     = 1
}

variable "worker_max_replicas" {
  description = "Configuration value for worker max replicas."
  type        = number
  default     = 1
}

variable "notification_min_replicas" {
  description = "Configuration value for notification min replicas."
  type        = number
  default     = 1
}

variable "notification_max_replicas" {
  description = "Configuration value for notification max replicas."
  type        = number
  default     = 1
}

variable "migrator_parallelism" {
  description = "Configuration value for migrator parallelism."
  type        = number
  default     = 1
}

variable "migrator_replica_timeout_in_seconds" {
  description = "Configuration value for migrator replica timeout in seconds."
  type        = number
  default     = 1800
}

variable "migrator_replica_retry_limit" {
  description = "Configuration value for migrator replica retry limit."
  type        = number
  default     = 0
}

variable "api_image" {
  description = "Configuration value for API image."
  type        = string
}

variable "web_image" {
  description = "Configuration value for web image."
  type        = string
}

variable "web_runtime_api_base_url" {
  description = "Configuration value for web runtime API base url."
  type        = string
  default     = "https://api.prod.example.com"
}

variable "worker_image" {
  description = "Configuration value for worker image."
  type        = string
}

variable "notification_image" {
  description = "Configuration value for notification image."
  type        = string
}

variable "migrator_image" {
  description = "Configuration value for migrator image."
  type        = string
}
