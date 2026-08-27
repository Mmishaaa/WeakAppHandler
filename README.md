# WeakAppHandler

## Auth Service — seed credentials

Applying the Auth Service's EF Core migrations (`InitialAuth`) seeds one user per role plus one
machine-to-machine service client, for local development only:

| Role | Email | Password |
|------|-------|----------|
| `viewer` | `viewer@weakapphandler.local` | `Viewer#12345` |
| `admin` | `admin@weakapphandler.local` | `Admin#12345` |

| Service client | Client ID | Client secret | Scope |
|-----------------|-----------|----------------|-------|
| Gateway → Ingestor | `gateway-ingestor` | `gateway-ingestor-secret-CHANGE-ME` | `ingestion:admin` |

These values are defined in `src/Auth/WeakAppHandler.Auth/Persistence/Configurations/AuthSeedData.cs`
and are baked into the `InitialAuth` migration as password/secret hashes (PBKDF2-SHA256), not
plaintext. They are development-only defaults, not meant to be used as-is in any shared or
production-like environment.