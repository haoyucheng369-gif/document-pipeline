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

variable "container_app_environment_id" {
  description = "Resource ID of the Azure Container Apps environment."
  type        = string
}

variable "trigger_type" {
  description = "Container App Job trigger type."
  type        = string
  default     = "Manual"
}

variable "container_name" {
  description = "Container name."
  type        = string
}

variable "image" {
  description = "Container image reference."
  type        = string
}

variable "cpu" {
  description = "Container CPU allocation."
  type        = number
  default     = 0.5
}

variable "memory" {
  description = "Container memory allocation."
  type        = string
  default     = "1Gi"
}

variable "env_vars" {
  description = "Configuration value for env vars."
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "Configuration value for secret env vars."
  type        = map(string)
  default     = {}
}

variable "env_entries" {
  description = "Configuration value for env entries."
  type = list(object({
    name        = string
    value       = optional(string)
    secret_name = optional(string)
  }))
  default = []
}

variable "key_vault_secret_refs" {
  description = "Key Vault secret references used by the app."
  type        = map(string)
  default     = {}
}

variable "enable_system_assigned_identity" {
  description = "Whether to enable a system-assigned managed identity."
  type        = bool
  default     = false
}

variable "registry_server" {
  description = "Private container registry server."
  type        = string
  default     = "ghcr.io"
}

variable "registry_username" {
  description = "Private container registry username."
  type        = string
  default     = null
}

variable "registry_password" {
  description = "Private container registry password or token."
  type        = string
  default     = null
  sensitive   = true
}

variable "parallelism" {
  description = "Container App Job parallelism."
  type        = number
  default     = 1
}

variable "replica_timeout_in_seconds" {
  description = "Container App Job execution timeout in seconds."
  type        = number
  default     = 1800
}

variable "replica_retry_limit" {
  description = "Container App Job retry limit."
  type        = number
  default     = 0
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
