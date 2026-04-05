# CI/CD And Cloud Plan

This document describes the current delivery model for `DocFlowCloud`.

## Goal

Move the project from:

- local IDE / local Docker execution

to:

- automatic build and test
- Docker image publishing
- testbed deployment
- production promotion
- infrastructure as code for Azure runtime shape

## Current Operating Model

- one repository
- one application delivery workflow
- one `test` branch for automatic testbed delivery
- one `master` branch for controlled production promotion
- two cloud environments:
  - `testbed`
  - `production`

This still matches the current repository structure better than splitting pipelines per project.

## Application Delivery Workflow

Workflow file:

- `.github/workflows/docflowcloud-ci-cd.yml`

Current logical flow:

1. `build-and-test`
   - restore and build the .NET solution
   - run unit tests
   - run integration tests
   - install frontend dependencies
   - run frontend tests
   - build the frontend

2. `build-and-push-backend-images`
   - build Docker images for:
     - `api`
     - `worker`
     - `notification-service`
     - `migrator`
   - publish to GHCR
   - runs for the `test` branch

3. `build-and-push-web-image`
   - build the `web` image
   - publish to GHCR
   - runs for the `test` branch

4. `run-testbed-migrator`
   - detect whether EF migrations changed
   - run the migrator as an Azure Container Apps Job when needed

5. `deploy-testbed`
   - deploy validated images to Azure Container Apps in testbed
   - update `web`, `api`, `worker`, and `notification-service`
   - keep runtime secrets in Key Vault

6. `deploy-production`
   - manual production promotion from `master`
   - operator provides a validated `image_tag`
   - reuses the same artifact already validated in testbed
   - runs prod migrator only when migrations changed

## Delivery Principle

This project follows:

- build once
- validate in testbed
- promote the same artifact to production

Production does not rebuild from source during promotion.

## Current Cloud Runtime

Azure target:

- Azure Container Apps
- Azure SQL Database
- Azure Blob Storage
- Azure Service Bus
- Azure Key Vault
- Managed Identity

Runtime services:

- `web`
- `api`
- `worker`
- `notification-service`
- `migrator` as a Container Apps Job

## Secret Strategy

### Delivery-time secrets

GitHub environments hold deployment-facing values such as:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

### Runtime secrets

Application runtime secrets come from:

- Azure Key Vault
- Container App secret references
- Managed Identity

Typical runtime secrets:

- SQL connection string
- Blob connection string
- Service Bus connection string
- GHCR registry pull credential

## Branch Strategy

### `test`

Used for:

- full CI
- image build and push
- automatic deployment to testbed

### `master`

Used for:

- merging code already validated in testbed
- manual production promotion by `image_tag`

This keeps production controlled while preserving artifact promotion.

## Terraform Boundary

Terraform and CI/CD are intentionally separate concerns.

### Terraform manages

- Azure resource structure
- Container Apps / Job existence and baseline configuration
- managed identities
- Key Vault references
- probes, scale defaults, ingress defaults, and job execution settings

### CI/CD manages

- restore / build / test
- build and push images
- testbed deployment cadence
- production promotion by image tag

In short:

- Terraform defines the environment shape
- CI/CD defines which image version is running now

## Terraform Status

The repository now includes Terraform structure for:

- `infra/environments/testbed`
- `infra/environments/prod`

Current coverage includes:

- resource group
- log analytics
- container apps environment
- SQL
- storage
- service bus
- key vault
- container apps:
  - `api`
  - `web`
  - `worker`
  - `notification`
- container app job:
  - `migrator`
- managed identity
- key vault secret references
- GHCR pull auth
- probes, scale, ingress, and job execution settings

Remote state backend and dedicated infra workflow are still later-stage enhancements.

## Recommended Next Implementation Order

1. Fill real environment values for Terraform local secret files
2. Run the first real `terraform plan` against `testbed`
3. Align any remaining Azure-side drift
4. Optionally add a dedicated infra workflow
5. Optionally move Terraform state to a remote backend
