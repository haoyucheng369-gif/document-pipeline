variable "server_name" {
  description = "Configuration value for server name."
  type        = string
}

variable "database_name" {
  description = "Azure SQL Database name."
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group."
  type        = string
}

variable "location" {
  description = "Azure region for resources."
  type        = string
}

variable "administrator_login" {
  description = "Administrator username for Azure SQL Server."
  type        = string
}

variable "administrator_login_password" {
  description = "Administrator password for Azure SQL Server."
  type        = string
  sensitive   = true
}

variable "sku_name" {
  description = "SKU name."
  type        = string
  default     = "Basic"
}

variable "storage_account_type" {
  description = "Configuration value for storage account type."
  type        = string
  default     = "Geo"
}

variable "min_capacity" {
  type    = number
  default = null
}

variable "auto_pause_delay_in_minutes" {
  type    = number
  default = null
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
