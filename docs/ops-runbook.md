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
2. Roll back to last known good deployment artifact.
3. Re-run readiness checks.
4. Run smoke tests on critical paths.
5. Communicate incident status and mitigation timeline.

## Post-Incident Actions

- Capture root cause and trigger.
- Add or update automated checks to prevent recurrence.
- Document operational gaps and owners.
