variable "name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "sku" {
  description = "Configuration value for SKU."
  type        = string
  default     = "PerGB2018"
}

variable "retention_in_days" {
  description = "Log retention period in days."
  type        = number
  default     = 30
}

variable "daily_quota_gb" {
  description = "Daily Log Analytics quota in GB. Use -1 for no limit."
  type        = number
  default     = -1
}

variable "local_authentication_enabled" {
  description = "Configuration value for local authentication enabled."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
