output "name" {
  description = "Resource name."
  value       = azurerm_storage_account.this.name
}

output "id" {
  description = "Resource ID."
  value       = azurerm_storage_account.this.id
}

output "primary_blob_endpoint" {
  description = "Primary Blob service endpoint."
  value       = azurerm_storage_account.this.primary_blob_endpoint
}

output "blob_container_name" {
  description = "Blob container name used for uploads and result files."
  value       = azurerm_storage_container.uploads.name
}
