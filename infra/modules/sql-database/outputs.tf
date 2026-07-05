output "server_name" {
  description = "Output value for server name."
  value       = azurerm_mssql_server.this.name
}

output "server_id" {
  description = "Output value for server ID."
  value       = azurerm_mssql_server.this.id
}

output "fully_qualified_domain_name" {
  description = "Output value for fully qualified domain name."
  value       = azurerm_mssql_server.this.fully_qualified_domain_name
}

output "database_name" {
  description = "Azure SQL Database name."
  value       = azurerm_mssql_database.this.name
}

output "database_id" {
  description = "Output value for database ID."
  value       = azurerm_mssql_database.this.id
}
