variable "namespace_name" {
  description = "Configuration value for namespace name."
  type        = string
}

variable "topic_name" {
  description = "Service Bus topic name used for business events."
  type        = string
}

variable "worker_subscription_name" {
  description = "Service Bus subscription name used by the worker."
  type        = string
}

variable "notification_subscription_name" {
  description = "Service Bus subscription name used by the notification service."
  type        = string
}

variable "api_realtime_subscription_name" {
  description = "Service Bus subscription name used by API realtime updates."
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

variable "sku" {
  description = "Configuration value for SKU."
  type        = string
  default     = "Standard"
}

variable "max_delivery_count" {
  description = "Maximum delivery count before dead-lettering."
  type        = number
  default     = 10
}

variable "topic_default_message_ttl" {
  description = "Configuration value for topic default message TTL."
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "topic_enable_batched_operations" {
  description = "Configuration value for topic enable batched operations."
  type        = bool
  default     = false
}

variable "subscription_default_message_ttl" {
  description = "Configuration value for subscription default message TTL."
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "subscription_auto_delete_on_idle" {
  description = "Configuration value for subscription auto delete on idle."
  type        = string
  default     = "P10675199DT2H48M5.4775807S"
}

variable "subscription_enable_batched_operations" {
  description = "Configuration value for subscription enable batched operations."
  type        = bool
  default     = false
}

variable "subscription_dead_lettering_on_filter_evaluation_error" {
  description = "Configuration value for subscription dead lettering on filter evaluation error."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default     = {}
}
