output "name" {
  description = "Resource name."
  value       = azurerm_container_app.this.name
}

output "id" {
  description = "Resource ID."
  value       = azurerm_container_app.this.id
}

output "latest_revision_name" {
  description = "Output value for latest revision name."
  value       = azurerm_container_app.this.latest_revision_name
}

output "latest_revision_fqdn" {
  description = "Output value for latest revision FQDN."
  value       = azurerm_container_app.this.latest_revision_fqdn
}

output "principal_id" {
  description = "Principal ID for the system-assigned managed identity."
  value       = try(azurerm_container_app.this.identity[0].principal_id, null)
}
