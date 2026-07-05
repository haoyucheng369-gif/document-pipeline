variable "name" {
  description = "Resource name."
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

variable "tenant_id" {
  description = "Azure tenant ID."
  type        = string
}

variable "sku_name" {
  description = "SKU name."
  type        = string
  default     = "standard"
}

variable "soft_delete_retention_days" {
  description = "Configuration value for soft delete retention days."
  type        = number
  default     = 7
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
