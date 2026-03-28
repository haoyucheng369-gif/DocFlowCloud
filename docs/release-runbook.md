# Release Runbook

This runbook describes the current image-tag promotion model for `DocFlowCloud`.

## Current Model

- `test` branch
  - automatic CI
  - build and push images to GHCR
  - automatic testbed deployment
- `master`
  - production is promoted manually by `image_tag`
  - production reuses an image tag already validated in testbed

## How To Find a Valid `image_tag`

Use a tag that satisfies all of the following:

1. It was built by the `test` branch workflow.
2. The images exist in GHCR for all runtime components.
3. The corresponding testbed deployment passed validation.

Typical places to find it:

- GitHub Packages / GHCR
  - `docflowcloud-api`
  - `docflowcloud-web`
  - `docflowcloud-worker`
  - `docflowcloud-notification-service`
  - `docflowcloud-migrator`
- the commit SHA from a successful `test` deployment

In this project, the most common tag format is a commit SHA such as:

- `02bba06`

## How To Promote Production

1. Open GitHub Actions.
2. Select the `DocFlowCloud CI/CD` workflow.
3. Click `Run workflow`.
4. Choose branch `master`.
5. Enter the validated `image_tag`.
6. Start the workflow.

Expected production behavior:

1. validate required production resources
2. run prod migrator only if EF migrations changed
3. update:
   - `docflow-web-prod`
   - `docflow-api-prod`
   - `docflow-worker-prod`
   - `docflow-notification-prod`

## How To Roll Back Production

Rollback uses the same workflow.

1. Find an older validated `image_tag`.
2. Run the production promotion workflow again.
3. Enter the older tag.
4. Let the workflow update production back to that known-good version.

This keeps rollback simple because production always promotes an already-built artifact.

## Troubleshooting Guide

### API startup issues

Check:

- `docflow-api-prod` logs
- SQL connection string
- Service Bus connection string
- Key Vault secret references
- managed identity access to Key Vault

Typical symptoms:

- `ServiceBusClient..ctor(String connectionString)`
  - Service Bus connection string missing or invalid
- `Login failed for user ...`
  - SQL connection string password mismatch

### Worker not processing jobs

Check:

- `docflow-worker-*` logs
- Service Bus topic/subscription names
- `Messaging__Provider=ServiceBus`
- `worker` subscription existence
- worker managed identity / Key Vault access

### Key Vault reference failures

Check:

1. Container App `Identity -> System assigned = On`
2. Key Vault role assignment:
   - `Key Vault Secrets User`
3. Container App secret is configured as:
   - `Key Vault reference`
4. secret URL points to the correct vault and secret
5. environment variable points to the correct `secretref`

If needed:

- create a fresh secret version in Key Vault
- recreate the ACA secret reference
- restart the app / create a new revision

### Migrator issues

Check:

- `docflow-migrator-*` logs
- SQL connectivity
- prod/testbed secret references
- whether EF migrations actually changed

Current workflow behavior:

- if `src/DocFlowCloud.Infrastructure/Migrations/` has no changes, migrator is skipped

## Operational Notes

- local development still uses RabbitMQ
- cloud testbed and production use Azure Service Bus
- runtime secrets should come from Key Vault + managed identity, not repo config
