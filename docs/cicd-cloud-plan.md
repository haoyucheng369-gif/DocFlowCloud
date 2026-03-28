# CI/CD And Cloud Plan

This document defines the first delivery target for DocFlowCloud.

## Goal

Move the project from:

- local IDE / local Docker execution

to:

- automatic build and test
- Docker image publishing
- testbed deployment
- production deployment behind approval

## Target Operating Model

- one repository
- one system-level GitHub Actions workflow
- one CI/CD pipeline
- one branch for test validation:
  - `test`
- one branch for production promotion:
  - `master`
- two deployment stages:
  - automatic `testbed`
  - controlled `production`

This matches the current repository structure better than splitting pipelines per project.

## What The Workflow Does

The workflow file is:

- `.github/workflows/docflowcloud-ci-cd.yml`

It currently contains five jobs:

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
   - push backend images to GHCR
   - runs only for the `test` branch

3. `build-and-push-web-image`
   - build the `web` image
   - push the web image to GHCR
   - runs only for the `test` branch

4. `run-testbed-migrator`
   - runs the existing `migrator` image as a one-off Azure Container Apps Job
   - applies EF Core migrations to the Azure SQL testbed database
   - must succeed before `api` and `web` are deployed

5. `deploy-testbed`
   - first deployment stage
   - runs automatically after a successful `test` image build
   - deploys `api` and `web` to Azure Container Apps
   - injects the real Azure SQL connection string into the API
   - injects the real Azure Blob connection string and container name into the API
   - injects runtime configuration into the web container

6. `deploy-production`
   - second deployment stage
   - runs only from `master`
   - requires a manually provided `image_tag`
   - is intended to stay behind GitHub environment approval

## Why GHCR First

GHCR is a good first registry for this project because:

- it is simple to integrate with GitHub Actions
- it keeps pipeline setup lightweight
- it is enough for portfolio, testbed and first cloud deployment

Production can later move to Azure Container Registry if tighter Azure-native integration becomes necessary.

## Recommended Azure Target

For the first cloud deployment, the recommended target is:

- `Azure Container Apps`

Why:

- the system is already containerized
- multiple services exist already
- it is simpler than AKS
- it is more realistic than keeping production-like deployments in local Compose

Planned deployed services:

- `web`
- `api`
- `worker`
- `notification-service`

The `migrator` image is now used as a one-off migration job in testbed before the API is deployed.

### First cloud deployment scope

The first real cloud deployment should stay intentionally narrow:

- deploy `api`
- deploy `web`
- do not deploy `worker` yet
- do not deploy `notification-service` yet

This keeps the first Azure deployment focused on validating:

- GitHub Actions -> Azure login
- GHCR -> Azure Container Apps image pull
- basic `testbed` promotion flow

To reduce external dependencies in the first pass:

- API disables the realtime RabbitMQ consumer in cloud testbed
- API now reads Azure SQL and Azure Blob settings from testbed environment configuration

This first pass proves deployment mechanics.
Background services can be added right after.

## Environment Responsibilities

### Development

- local IDE
- local Docker Compose dev
- local SQL Server / RabbitMQ / Local storage

### Testbed

- first cloud deployment target
- separate Azure SQL database
- separate messaging namespace / vhost
- separate secrets
- Azure Blob storage

### Production

- second cloud deployment target
- separate data stores and secrets
- protected by approval

## Secrets Strategy

### Current state

Local secrets/config still exist in:

- `appsettings.*.json`
- `docker-compose.*.yml`

This is acceptable for local development.

### Target state

Sensitive values should move out of the repository.

Recommended storage:

- GitHub repository or environment secrets for pipeline authentication
- Azure Key Vault for application runtime secrets

Typical secrets:

- Azure login credentials for deployment
- SQL connection strings
- Blob connection string or managed identity configuration
- messaging credentials

## Branch Strategy

### `test`

Used for:

- full CI
- Docker image build
- GHCR push
- automatic deployment to testbed

The image tag used for testbed is the commit SHA of the `test` branch push.

### `master`

Used for:

- merge of code already validated in testbed
- production promotion

Production should not rebuild a different image.
It should reuse the exact image tag that already passed through testbed.

Current workflow design:

- push to `master` still runs CI
- production deployment is manual through `workflow_dispatch`
- the operator provides the already-validated `image_tag`

This keeps the first production flow simple while preserving the "build once, deploy many" principle.

## GitHub Environments

Create two GitHub environments:

- `testbed`
- `production`

Use them for:

- scoped secrets
- deployment history
- approval rules for production

## Minimum Secrets To Add Later

Repository or environment secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Optional variables:

- `TESTBED_API_BASE_URL`

Later, when real deployment is added, also define Azure-side names such as:

- resource group name
- container app environment name
- container app names

## What Is Still Placeholder

The workflow is intentionally staged.

Already real:

- build
- test
- Docker image build
- GHCR push

Still placeholder:

- actual production promotion commands using the provided image tag
- actual Azure Key Vault integration
- worker and notification-service deployment to Azure Container Apps
- messaging infrastructure for full async cloud processing

## Recommended Next Implementation Order

1. Push to `test` and confirm CI + GHCR image push works
2. Create Azure testbed resource group and Container Apps environment
3. Push to `test` and let the first Azure deployment create/update `api` and `web`
4. Update `TESTBED_API_BASE_URL` with the real Azure API FQDN and push `test` again
5. Add `worker` and `notification-service` to testbed
6. Add Key Vault-backed secrets
7. Merge validated code to `master`
8. Run `workflow_dispatch` on `master` with the already-validated image tag to promote to production
