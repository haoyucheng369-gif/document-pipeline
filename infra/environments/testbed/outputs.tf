output "resource_group_name" {
  description = "Name of the resource group."
  value       = module.resource_group.name
}

output "log_analytics_name" {
  description = "Output value for log analytics name."
  value       = module.log_analytics.name
}

output "container_app_environment_name" {
  description = "Output value for container app environment name."
  value       = module.container_app_environment.name
}

output "sql_server_name" {
  description = "Globally unique Azure SQL Server name."
  value       = module.sql_database.server_name
}

output "sql_database_name" {
  description = "Azure SQL Database name."
  value       = module.sql_database.database_name
}

output "storage_account_name" {
  description = "Globally unique Storage Account name."
  value       = module.storage_account.name
}

output "blob_container_name" {
  description = "Blob container name used for uploads and result files."
  value       = module.storage_account.blob_container_name
}

output "service_bus_namespace_name" {
  description = "Service Bus Namespace name."
  value       = module.service_bus.namespace_name
}

output "service_bus_topic_name" {
  description = "Output value for service bus topic name."
  value       = module.service_bus.topic_name
}

output "key_vault_name" {
  description = "Output value for key vault name."
  value       = module.key_vault.name
}

output "api_container_app_name" {
  description = "Output value for API container app name."
  value       = module.api_container_app.name
}

output "web_container_app_name" {
  description = "Output value for web container app name."
  value       = module.web_container_app.name
}

output "worker_container_app_name" {
  description = "Output value for worker container app name."
  value       = module.worker_container_app.name
}

output "notification_container_app_name" {
  description = "Output value for notification container app name."
  value       = module.notification_container_app.name
}

output "migrator_job_name" {
  description = "Output value for migrator job name."
  value       = module.migrator_job.name
}
