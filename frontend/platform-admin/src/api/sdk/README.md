# SDK Generation

## Prerequisite

- Run backend API and ensure Swagger is reachable.
- Install Node.js and `npx`.

## Generate

Run in `frontend/platform-admin/scripts`:

```powershell
./generate-sdk.ps1
```

Custom OpenAPI URL:

```powershell
./generate-sdk.ps1 -OpenApiUrl "http://localhost:5000/swagger/v1/swagger.json"
```

Export OpenAPI first, then generate SDK:

```powershell
./export-openapi-and-generate-sdk.ps1
```

Check if committed SDK is in sync with current OpenAPI:

```powershell
./check-sdk-up-to-date.ps1
```

Generated output:

- `frontend/platform-admin/src/api/sdk/unicore-sdk.ts`
- `frontend/platform-admin/src/api/sdk/openapi.json` (optional exported spec)
