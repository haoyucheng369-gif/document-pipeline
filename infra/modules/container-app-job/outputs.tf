output "name" {
  description = "Resource name."
  value       = azurerm_container_app_job.this.name
}

output "id" {
  description = "Resource ID."
  value       = azurerm_container_app_job.this.id
}

output "principal_id" {
  description = "Principal ID for the system-assigned managed identity."
  value       = try(azurerm_container_app_job.this.identity[0].principal_id, null)
}
