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

## Auth Service — user login, refresh and JWKS

`POST /login` accepts `{ "email": "...", "password": "..." }` for one of the seed users above. On
success it returns `{ accessToken, tokenType, expiresInSeconds, role, email }` and sets an
`httpOnly`, `Secure`, `SameSite=Strict` `refresh_token` cookie. `role` is PascalCase (`"Viewer"` /
`"Admin"`), matching the role values `WeakAppHandler.ServiceDefaults.Auth.ServicePolicies`' policies
check — not the lowercase value stored in `users.role`.

Access tokens are RS256-signed JWTs (`iss=weakapphandler-auth`, `aud=weakapphandler`), default
lifetime 15 minutes (`Auth:Tokens:AccessTokenLifetimeMinutes`). `GET /.well-known/jwks.json`
publishes the public half of the signing key any service can validate them against; the private key
itself is generated once and persisted in the `signing_keys` table so it - and the token `kid` - stay
stable across Auth Service restarts.

`POST /refresh` reads the `refresh_token` cookie, and if it is still valid and unused, issues a new
access token and rotates the refresh token (the old one is revoked, a new cookie is set). Refresh
tokens default to a 7-day lifetime (`Auth:Tokens:RefreshTokenLifetimeDays`) and are stored only as a
SHA-256 hash in the `refresh_tokens` table, never in plaintext.