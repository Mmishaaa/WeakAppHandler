-- Skeleton local-dev role initialization for the base docker-compose stack (TASK-006).
-- These are intentionally broad (ALL PRIVILEGES), matching every service's current
-- ConnectionStrings.* placeholder credentials in appsettings.json (Processor: processor/
-- processor_password, Auth: auth/auth_password against the single `weakapphandler` database).
-- Real least-privilege roles (gateway_ro read-only, scoped write roles per service) are
-- TASK-043's job, not this one -- this file only has to let already-existing EF Core
-- migrations run against a real, persistent Postgres instance instead of a throwaway
-- manually-run container.
CREATE ROLE processor LOGIN PASSWORD 'processor_password';
GRANT ALL PRIVILEGES ON DATABASE weakapphandler TO processor;

CREATE ROLE auth LOGIN PASSWORD 'auth_password';
GRANT ALL PRIVILEGES ON DATABASE weakapphandler TO auth;

-- Gateway only ever SELECTs (TASK-023) and never migrates, but until TASK-043 introduces the real
-- read-only gateway_ro role, it shares the same broad placeholder credentials as the write services.
CREATE ROLE gateway LOGIN PASSWORD 'gateway_password';
GRANT ALL PRIVILEGES ON DATABASE weakapphandler TO gateway;

-- PostgreSQL 15+ no longer grants CREATE on the public schema to every role by default;
-- without this, EF Core migrations from either service would fail with
-- "permission denied for schema public".
GRANT ALL ON SCHEMA public TO processor, auth, gateway;
