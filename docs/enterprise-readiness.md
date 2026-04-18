# UniCore Enterprise Readiness Checklist

## Scope

This checklist defines the minimum bar for running UniCore as an enterprise reusable foundation across multiple teams/environments.

## 1) Delivery and Runtime

- [ ] Backend image built from `Dockerfile` and tagged by immutable release version.
- [ ] Kubernetes baseline manifest applied or translated to your platform standard.
- [ ] Production configuration managed outside source control (secret manager).
- [ ] Release pipeline enforces migration-before-startup strategy.

## 2) Database Governance

- [ ] New environment runs `scripts/release/init-postgres.ps1`.
- [ ] `dotnet ef migrations list` includes all expected migrations.
- [ ] `dotnet ef database update` runs in release job (or DBA applies generated idempotent SQL).
- [ ] `__EFMigrationsHistory` checked after release.

## 3) Quality Gates

- [ ] `scripts/release/preflight-enterprise.ps1` passes in CI.
- [ ] Backend tests pass with coverage threshold policy.
- [ ] Frontend lint/build/test all pass.
- [ ] Module contract report (`failOnBreaking=true`) passes before merge.

## 4) Security and Compliance

- [ ] Secrets are not hardcoded in config files or manifests.
- [ ] Security workflows (CodeQL, dependency checks, secret scans) remain mandatory.
- [ ] JWT key rotation procedure and incident response runbook are documented.
- [ ] SBOM and release manifest generated and archived for each release.

## 5) Operations and Reliability

- [ ] Health probes and metrics endpoint monitored.
- [ ] SLO/SLI dashboards and alert thresholds configured.
- [ ] Rollback drill completed at least once per major release.
- [ ] Post-incident review template and ownership are defined.

## Suggested Release Cadence

1. Feature freeze and changelog finalize.
2. Run enterprise preflight.
3. Run database init/migration (for new env) or migration only (existing env).
4. Deploy backend/frontend.
5. Verify health + metrics + contract report.
6. Archive evidence (`release-manifest`, `sbom`, logs, validation report).
