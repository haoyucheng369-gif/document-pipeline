variable "name" {
  description = "Resource name."
  type        = string
}

variable "blob_container_name" {
  description = "Blob container name used for uploads and result files."
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

variable "account_tier" {
  description = "Storage Account performance tier."
  type        = string
  default     = "Standard"
}

variable "account_replication_type" {
  description = "Storage Account replication type."
  type        = string
  default     = "LRS"
}

variable "allow_nested_items_to_be_public" {
  description = "Whether nested items in the storage account can be public."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
