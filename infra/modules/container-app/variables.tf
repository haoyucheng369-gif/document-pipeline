variable "name" {
  description = "Resource name."
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group."
  type        = string
}

variable "container_app_environment_id" {
  description = "Resource ID of the Azure Container Apps environment."
  type        = string
}

variable "revision_mode" {
  description = "Container App revision mode."
  type        = string
  default     = "Single"
}

variable "external_ingress_enabled" {
  description = "Configuration value for external ingress enabled."
  type        = bool
  default     = false
}

variable "allow_insecure_connections" {
  description = "Whether insecure HTTP connections are allowed."
  type        = bool
  default     = false
}

variable "target_port" {
  description = "Application port exposed by ingress."
  type        = number
  default     = null
}

variable "transport" {
  description = "Ingress transport mode."
  type        = string
  default     = "auto"
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

variable "min_replicas" {
  description = "Minimum replica count."
  type        = number
  default     = 1
}

variable "max_replicas" {
  description = "Maximum replica count."
  type        = number
  default     = 1
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

variable "liveness_probe" {
  description = "Optional liveness probe configuration."
  type = object({
    transport               = string
    port                    = number
    path                    = string
    interval_seconds        = number
    timeout                 = number
    failure_count_threshold = number
    initial_delay           = number
  })
  default = null
}

variable "readiness_probe" {
  description = "Optional readiness probe configuration."
  type = object({
    transport               = string
    port                    = number
    path                    = string
    interval_seconds        = number
    timeout                 = number
    failure_count_threshold = number
    success_count_threshold = number
    initial_delay           = number
  })
  default = null
}

variable "startup_probe" {
  description = "Optional startup probe configuration."
  type = object({
    transport               = string
    port                    = number
    path                    = optional(string)
    interval_seconds        = number
    timeout                 = number
    failure_count_threshold = number
    success_count_threshold = number
    initial_delay           = number
  })
  default = null
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
