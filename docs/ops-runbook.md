# UniCore Ops Runbook

## Health and Readiness

- Liveness endpoint: `/api/health/live`
- Readiness endpoint: `/api/health/ready`
- Metrics endpoint: `/metrics`

When readiness is degraded:
- Check database connectivity first.
- Verify required configuration is loaded for current environment.
- Inspect recent deployment diff and rollback candidate.

## Release Verification Checklist

- Backend `build` and integration tests passed.
- Frontend quality checks passed.
- Database migration status confirmed.
- Smoke test critical flows:
  - Login
  - User query
  - Permission-protected endpoint access
  - Audit export creation and status query

## Alert Severity

- **P1**: Service unavailable, authentication down, data corruption risk.
- **P2**: Major feature unavailable, partial API failures.
- **P3**: Non-critical degradation, temporary workaround available.

## Troubleshooting Quick Paths

- **Login fails globally**
  - Validate JWT issuer/audience/signing key configuration.
  - Confirm auth service and cache availability.
- **High 5xx rate**
  - Check recent config changes and dependency health.
  - Review structured error logs and trace identifiers.
- **Audit export stuck**
  - Verify background job scheduling is enabled.
  - Inspect export job status and callback delivery logs.

## Rollback Procedure

1. Freeze new releases.
2. Run rollback script:

```powershell
pwsh ./scripts/release/rollback.ps1 -Environment staging -TargetBackendVersion 0.0.9 -TargetFrontendVersion 0.0.9 -Reason "incident rollback"
```

3. Re-run readiness checks (`/api/health/ready`, `/metrics`).
4. Run smoke tests on critical paths:

```powershell
pwsh ./scripts/bootstrap-smoke.ps1 -ProjectRoot .
```

5. Communicate incident status and mitigation timeline.

## Deployment Procedure

Use deployment script to keep release steps consistent:

```powershell
pwsh ./scripts/release/deploy.ps1 -Environment staging -BackendVersion 0.1.0 -FrontendVersion 0.1.0 -Notes "release note"
```

Need one-command initialization + migration for a brand new PostgreSQL environment:

```powershell
pwsh ./scripts/release/deploy.ps1 `
  -Environment staging `
  -BackendVersion 0.1.0 `
  -FrontendVersion 0.1.0 `
  -InitDatabase `
  -DbHost 127.0.0.1 `
  -DbPort 5432 `
  -DbAdminUser postgres `
  -DbAdminPassword (Read-Host "Postgres admin password" -AsSecureString) `
  -DbAppUser unicore_app `
  -DbAppPassword (Read-Host "UniCore app password" -AsSecureString) `
  -DbName unicore_staging
```

For relational database deployments, run EF Core migrations before starting the new app version when `Database:ApplyMigrationsOnStartup=false`:

```powershell
dotnet ef database update --project src/Platform.Infrastructure/Platform.Infrastructure.csproj --startup-project src/Platform.WebApi/Platform.WebApi.csproj
```

If your release process applies SQL externally, generate and apply an idempotent script:

```powershell
dotnet ef migrations script --idempotent --project src/Platform.Infrastructure/Platform.Infrastructure.csproj --startup-project src/Platform.WebApi/Platform.WebApi.csproj
```

After deployment, execute:

- `/api/health/ready`
- `/metrics`
- `/api/modules/contracts/report?protocolVersion=1.0.0&failOnBreaking=true`

## Post-Incident Actions

- Capture root cause and trigger.
- Add or update automated checks to prevent recurrence.
- Document operational gaps and owners.
