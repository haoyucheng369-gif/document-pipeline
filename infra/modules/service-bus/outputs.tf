output "namespace_name" {
  description = "Output value for namespace name."
  value       = azurerm_servicebus_namespace.this.name
}

output "namespace_id" {
  description = "Output value for namespace ID."
  value       = azurerm_servicebus_namespace.this.id
}

output "topic_name" {
  description = "Service Bus topic name used for business events."
  value       = azurerm_servicebus_topic.job_events.name
}

output "worker_subscription_name" {
  description = "Service Bus subscription name used by the worker."
  value       = azurerm_servicebus_subscription.worker.name
}

output "notification_subscription_name" {
  description = "Service Bus subscription name used by the notification service."
  value       = azurerm_servicebus_subscription.notification.name
}

output "api_realtime_subscription_name" {
  description = "Service Bus subscription name used by API realtime updates."
  value       = azurerm_servicebus_subscription.api_realtime.name
}
