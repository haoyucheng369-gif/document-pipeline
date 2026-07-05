output "name" {
  description = "Resource name."
  value       = azurerm_key_vault.this.name
}

output "id" {
  description = "Resource ID."
  value       = azurerm_key_vault.this.id
}

output "vault_uri" {
  description = "Output value for vault uri."
  value       = azurerm_key_vault.this.vault_uri
}
