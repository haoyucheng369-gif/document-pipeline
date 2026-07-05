# Terraform Structure

This directory contains the Terraform code for the Azure infrastructure used by `CloudDocumentPipeline`.

## Layout

- `modules/`
  - Reusable Azure resource modules.
- `environments/testbed/`
  - Testbed environment entry point.
- `environments/prod/`
  - Production environment entry point.

Each environment directory is an independent Terraform root module:

- Provider configuration lives under `environments/*`.
- Environment variables and naming rules live under `environments/*`.
- `main.tf` composes the reusable modules from `modules/*`.

## Modules

Current reusable modules:

- `resource-group`
- `log-analytics`
- `container-app-environment`
- `sql-database`
- `storage-account`
- `service-bus`
- `key-vault`
- `container-app`
- `container-app-job`

## Covered Resources

### Infrastructure Baseline

- Resource Group
- Log Analytics Workspace
- Azure Container Apps Environment
- Azure SQL Server and Database
- Storage Account and Blob container
- Service Bus Namespace, Topic, and Subscriptions
- Key Vault

### Runtime Layer

- `api`
- `web`
- `worker`
- `notification`
- `migrator` job

### Runtime Configuration

- Managed Identity
- Key Vault secret references
- SQL, Blob Storage, and Service Bus secret injection
- GHCR private image pull credentials
- API probes
- worker and notification replica settings
- ingress configuration
- revision mode
- migrator job timeout, retry, and parallelism settings

## Execution Flow

Each environment follows this flow:

```text
terraform.tfvars
-> variables.tf
-> locals.tf
-> main.tf
-> modules/*
-> Azure resources
-> outputs.tf
```

In practice:

- `variables.tf` defines inputs.
- `terraform.tfvars` provides environment-specific values.
- `locals.tf` builds names and shared derived values.
- `main.tf` calls the reusable modules.
- `outputs.tf` exposes key deployment results.

## Secrets And Image Parameters

Local sensitive values and image references should stay outside committed `terraform.tfvars`.

- `terraform.tfvars`
  - Non-sensitive environment parameters only.
- `secrets.auto.tfvars`
  - Local sensitive values and image addresses.
  - Ignored by `.gitignore`.
- `secrets.auto.tfvars.example`
  - Example template only. Do not commit real secret values.

Common local sensitive parameters include:

- `sql_administrator_login_password`
- `sql_connection_string`
- `blob_connection_string`
- `service_bus_connection_string`
- `ghcr_registry_username`
- `ghcr_registry_password`
- `api_image`
- `web_image`
- `worker_image`
- `notification_image`
- `migrator_image`

For CI/CD, prefer:

- `TF_VAR_*` environment variables
- GitHub environment secrets

## Current Status

The Terraform baseline currently covers:

- Infrastructure resource structure.
- Runtime app and job structure.
- Core runtime configuration.

Remaining setup work before a real environment rollout:

- Inject real environment values.
- Run the first real `terraform plan` and `terraform apply`.
- Optionally configure a remote state backend.
- Optionally add an infrastructure workflow.
