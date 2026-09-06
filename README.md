# WeakAppHandler

## Running the stack

```bash
cp .env.example .env
docker compose up -d
```

`docker compose ps` shows `weakapp` as `healthy` once it is ready to serve.

## WeakApp

A vendored third-party API that reports meter readings and fails on purpose — roughly one
response in ten is a 5xx, a rate-limit or a truncated body. See [THIRD_PARTY.md](THIRD_PARTY.md)
for provenance and licence.

| What | Where |
|------|-------|
| Readings | `GET http://localhost:8080/meters` |
| Health | `GET http://localhost:8080/health` |
| OpenAPI document | `GET http://localhost:8080/openapi/v1.json` |

Every request needs the API key header:

```bash
curl -i -H "X-Api-Key: supersecret" http://localhost:8080/meters
```

Without it the API answers `401 Invalid or missing API key`. There is no Swagger UI — the
package the app uses serves the OpenAPI document as JSON only.

From inside the `backend` network the service is reachable as `http://weakapp:8080`.
