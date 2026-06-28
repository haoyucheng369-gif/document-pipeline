locals {
  environment                           = "prod"
  resource_group_name                   = "rg-${var.project_name}-${local.environment}"
  log_analytics_name                    = "log-${var.project_name}-${local.environment}"
  container_app_environment_name        = "cae-${var.project_name}-${local.environment}"
  sql_server_name                       = "${var.project_name}-${local.environment}-sql"
  sql_database_name                     = "CloudDocumentPipelineProdDb"
  storage_account_name                  = "stg${var.project_name}${local.environment}"
  blob_container_name                   = "uploads"
  service_bus_namespace_name            = "sb-${var.project_name}-${local.environment}"
  service_bus_topic_name                = "job-events"
  service_bus_worker_subscription       = "worker"
  service_bus_notification_subscription = "notification"
  service_bus_api_realtime_subscription = "api-realtime"
  key_vault_name                        = "kv-${var.project_name}-${local.environment}"
  sql_connection_secret_name            = "sql-connection-string"
  blob_connection_secret_name           = "blob-connection-string"
  service_bus_connection_secret_name    = "servicebus-connection-string"
  api_container_app_name                = "${var.project_name}-api-${local.environment}"
  web_container_app_name                = "${var.project_name}-web-${local.environment}"
  worker_container_app_name             = "${var.project_name}-worker-${local.environment}"
  notification_container_app_name       = "${var.project_name}-notification-${local.environment}"
  migrator_job_name                     = "${var.project_name}-migrator-${local.environment}"

  tags = {
    environment = local.environment
    project     = var.project_name
    managed_by  = "terraform"
  }

  api_env_vars = {
    ASPNETCORE_ENVIRONMENT                   = "Production"
    DOTNET_ENVIRONMENT                       = "Production"
    Messaging__Provider                      = "ServiceBus"
    Storage__Provider                        = "AzureBlob"
    Storage__AzureBlob__ContainerName        = local.blob_container_name
    ServiceBus__TopicName                    = local.service_bus_topic_name
    ServiceBus__WorkerSubscriptionName       = local.service_bus_worker_subscription
    ServiceBus__NotificationSubscriptionName = local.service_bus_notification_subscription
    ServiceBus__ApiRealtimeSubscriptionName  = local.service_bus_api_realtime_subscription
  }

  api_env_entries = [
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    { name = "DOTNET_ENVIRONMENT", value = "Production" },
    { name = "Realtime__EnableJobStatusConsumer", value = "true" },
    { name = "Storage__Provider", value = "AzureBlob" },
    { name = "Storage__AzureBlob__ConnectionString", secret_name = local.blob_connection_secret_name },
    { name = "Storage__AzureBlob__ContainerName", value = local.blob_container_name },
    { name = "ConnectionStrings__DefaultConnection", secret_name = local.sql_connection_secret_name },
    { name = "Messaging__Provider", value = "ServiceBus" },
    { name = "ServiceBus__ConnectionString", secret_name = local.service_bus_connection_secret_name },
    { name = "ServiceBus__TopicName", value = local.service_bus_topic_name },
    { name = "ServiceBus__WorkerSubscriptionName", value = local.service_bus_worker_subscription },
    { name = "ServiceBus__NotificationSubscriptionName", value = local.service_bus_notification_subscription },
    { name = "ServiceBus__ApiRealtimeSubscriptionName", value = local.service_bus_api_realtime_subscription }
  ]

  web_env_vars = {
    RUNTIME_APP_ENV      = "production"
    RUNTIME_API_BASE_URL = var.web_runtime_api_base_url
  }

  worker_env_vars = {
    ASPNETCORE_ENVIRONMENT                   = "Production"
    DOTNET_ENVIRONMENT                       = "Production"
    Messaging__Provider                      = "ServiceBus"
    Storage__Provider                        = "AzureBlob"
    Storage__AzureBlob__ContainerName        = local.blob_container_name
    ServiceBus__TopicName                    = local.service_bus_topic_name
    ServiceBus__WorkerSubscriptionName       = local.service_bus_worker_subscription
    ServiceBus__NotificationSubscriptionName = local.service_bus_notification_subscription
    ServiceBus__ApiRealtimeSubscriptionName  = local.service_bus_api_realtime_subscription
  }

  worker_env_entries = [
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    { name = "DOTNET_ENVIRONMENT", value = "Production" },
    { name = "Messaging__Provider", value = "ServiceBus" },
    { name = "ConnectionStrings__DefaultConnection", secret_name = local.sql_connection_secret_name },
    { name = "Storage__Provider", value = "AzureBlob" },
    { name = "Storage__AzureBlob__ConnectionString", secret_name = local.blob_connection_secret_name },
    { name = "Storage__AzureBlob__ContainerName", value = local.blob_container_name },
    { name = "ServiceBus__ConnectionString", secret_name = local.service_bus_connection_secret_name },
    { name = "ServiceBus__TopicName", value = local.service_bus_topic_name },
    { name = "ServiceBus__WorkerSubscriptionName", value = local.service_bus_worker_subscription },
    { name = "ServiceBus__NotificationSubscriptionName", value = local.service_bus_notification_subscription },
    { name = "ServiceBus__ApiRealtimeSubscriptionName", value = local.service_bus_api_realtime_subscription }
  ]

  notification_env_vars = {
    ASPNETCORE_ENVIRONMENT                   = "Production"
    DOTNET_ENVIRONMENT                       = "Production"
    Messaging__Provider                      = "ServiceBus"
    Storage__Provider                        = "AzureBlob"
    Storage__AzureBlob__ContainerName        = local.blob_container_name
    ServiceBus__TopicName                    = local.service_bus_topic_name
    ServiceBus__WorkerSubscriptionName       = local.service_bus_worker_subscription
    ServiceBus__NotificationSubscriptionName = local.service_bus_notification_subscription
    ServiceBus__ApiRealtimeSubscriptionName  = local.service_bus_api_realtime_subscription
  }

  notification_env_entries = [
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    { name = "DOTNET_ENVIRONMENT", value = "Production" },
    { name = "Messaging__Provider", value = "ServiceBus" },
    { name = "ConnectionStrings__DefaultConnection", secret_name = local.sql_connection_secret_name },
    { name = "Storage__Provider", value = "AzureBlob" },
    { name = "Storage__AzureBlob__ConnectionString", secret_name = local.blob_connection_secret_name },
    { name = "Storage__AzureBlob__ContainerName", value = local.blob_container_name },
    { name = "ServiceBus__ConnectionString", secret_name = local.service_bus_connection_secret_name },
    { name = "ServiceBus__TopicName", value = local.service_bus_topic_name },
    { name = "ServiceBus__WorkerSubscriptionName", value = local.service_bus_worker_subscription },
    { name = "ServiceBus__NotificationSubscriptionName", value = local.service_bus_notification_subscription },
    { name = "ServiceBus__ApiRealtimeSubscriptionName", value = local.service_bus_api_realtime_subscription }
  ]

  migrator_env_vars = {
    ASPNETCORE_ENVIRONMENT = "Production"
    DOTNET_ENVIRONMENT     = "Production"
  }

  migrator_env_entries = [
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    { name = "DOTNET_ENVIRONMENT", value = "Production" },
    { name = "ConnectionStrings__DefaultConnection", secret_name = local.sql_connection_secret_name }
  ]

  app_secret_env_vars = {
    ConnectionStrings__DefaultConnection = local.sql_connection_secret_name
    Storage__AzureBlob__ConnectionString = local.blob_connection_secret_name
    ServiceBus__ConnectionString         = local.service_bus_connection_secret_name
  }

  api_liveness_probe = {
    transport               = "HTTP"
    port                    = 8080
    path                    = "/health/live"
    interval_seconds        = 10
    timeout                 = 5
    failure_count_threshold = 3
    initial_delay           = 10
  }

  api_readiness_probe = {
    transport               = "HTTP"
    port                    = 8080
    path                    = "/health/ready"
    interval_seconds        = 10
    timeout                 = 5
    failure_count_threshold = 3
    success_count_threshold = 1
    initial_delay           = 10
  }

  api_startup_probe = {
    transport               = "TCP"
    port                    = 8080
    interval_seconds        = 1
    timeout                 = 3
    failure_count_threshold = 240
    success_count_threshold = 1
    initial_delay           = 1
  }
}
