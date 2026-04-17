# UniCore Security Baseline

## Configuration Rules

- Never commit real credentials, tokens, or signing keys to source control.
- Keep `ConnectionStrings:Default`, `Jwt:SigningKey`, and object storage secrets injected by environment or secret manager.
- In non-local environments, startup should fail fast when required security configuration is missing.

## Secret Management

- Use environment variables or a managed secret store for:
  - Database passwords
  - JWT signing keys
  - Object storage access keys
  - Third-party API keys
- Rotate high-risk secrets regularly and after any suspected leak.
- Restrict access scope by environment and least privilege.

## Authentication and Session

- JWT signing key length must be at least 32 characters.
- Access token validity should stay short-lived.
- Refresh tokens must be revocable and server-side auditable.
- Frontend token handling should prioritize in-memory storage and avoid long-lived exposure.

## API Protection

- Keep authorization policies explicit per endpoint.
- Return standardized error envelopes without leaking internal details.
- Log audit events for security-sensitive actions (login, permission changes, exports).

## Delivery Gate

- Required CI checks:
  - Backend build and integration tests
  - Frontend lint/build checks
- Block merge on failed quality gates.

## Incident Basics

- On credential leak suspicion:
  1. Revoke/rotate compromised secrets immediately.
  2. Invalidate active sessions if token signing key may be exposed.
  3. Review recent access/audit logs and preserve evidence.
  4. Publish a post-incident summary with remediation actions.
