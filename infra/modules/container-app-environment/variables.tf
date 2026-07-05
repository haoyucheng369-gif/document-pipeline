variable "name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics workspace."
  type        = string
}

variable "workload_profile_name" {
  description = "Container Apps workload profile name."
  type        = string
  default     = null
}

variable "workload_profile_type" {
  description = "Container Apps workload profile type."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
