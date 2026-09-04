-- Least-privilege role initialization for the compose-managed PostgreSQL instance (TASK-043,
-- PRD §8: "One role per service. `gateway_ro` holds SELECT only ... `notification_rw` owns only
-- the alerting tables."). Replaces TASK-006's ALL-PRIVILEGES skeleton.
--
-- Every table in this single `weakapphandler` database lives in the default `public` schema
-- (confirmed: no service uses `ToTable(name, schema:)`), so isolation between services' tables is
-- achieved purely through PostgreSQL's default-deny table ownership model: each writer role below
-- creates (via its own EF Core migrations, run under that same role) and therefore owns exactly the
-- tables it is responsible for, and PostgreSQL grants zero access on another role's tables unless
-- explicitly granted. Nothing here grants any writer role access to another writer's tables.
--
-- `gateway_ro` never migrates (ADR-0003) and only ever runs read queries (TASK-023/024/032) against
-- Processor's core tables (meters, metrics, readings, meter_current_state) and Notification's
-- alerting tables (alerts, alert_rules) -- never Auth's. Since those tables don't exist yet when
-- this script runs (EF Core migrations create them later, at each service's own first startup),
-- `gateway_ro`'s read access is granted via ALTER DEFAULT PRIVILEGES: a standing rule, registered
-- now, that applies automatically to every table `processor_rw`/`notification_rw` create from this
-- point on -- rather than a one-time GRANT this script could only apply to tables that already exist.
CREATE ROLE processor_rw LOGIN PASSWORD 'processor_password';
CREATE ROLE auth_rw LOGIN PASSWORD 'auth_password';
CREATE ROLE notification_rw LOGIN PASSWORD 'notification_password';
CREATE ROLE gateway_ro LOGIN PASSWORD 'gateway_password';

-- PostgreSQL 15+ no longer grants CREATE on the public schema to every role by default; each
-- writer needs it to run its own EF Core migrations. `gateway_ro` deliberately does not get CREATE
-- (or any DML grant) here -- it is structurally incapable of writing anything, which is the whole
-- point of the role, not just a documented convention.
GRANT CREATE ON SCHEMA public TO processor_rw, auth_rw, notification_rw;

-- USAGE on the public schema remains PUBLIC by default even on PostgreSQL 15+ (only CREATE was
-- revoked from PUBLIC), so gateway_ro already has it; granted explicitly anyway so this script
-- documents gateway_ro's full set of rights in one place rather than relying on an unstated default.
GRANT USAGE ON SCHEMA public TO gateway_ro;

-- CONNECT on the database is likewise already PUBLIC by default; granted explicitly for the same
-- documentation reason.
GRANT CONNECT ON DATABASE weakapphandler TO processor_rw, auth_rw, notification_rw, gateway_ro;

-- The standing read-only rule: every table processor_rw or notification_rw creates from now on
-- (i.e. via their own future `dotnet ef database update` runs) automatically grants SELECT to
-- gateway_ro, with no further action needed as new migrations land. Auth's tables are deliberately
-- excluded -- gateway_ro has no reason to read users/service_clients.
ALTER DEFAULT PRIVILEGES FOR ROLE processor_rw IN SCHEMA public GRANT SELECT ON TABLES TO gateway_ro;
ALTER DEFAULT PRIVILEGES FOR ROLE notification_rw IN SCHEMA public GRANT SELECT ON TABLES TO gateway_ro;
