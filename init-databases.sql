-- Create auth database and auth_api user
CREATE DATABASE auth OWNER postgres;
GRANT ALL PRIVILEGES ON DATABASE auth TO postgres;

-- Connect to auth database to create auth_api user and schema
\c auth postgres

CREATE ROLE auth_api WITH LOGIN PASSWORD 'auth_api_dev_password';
GRANT CONNECT ON DATABASE auth TO auth_api;

CREATE SCHEMA IF NOT EXISTS auth AUTHORIZATION auth_api;
GRANT ALL PRIVILEGES ON SCHEMA auth TO auth_api;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA auth TO auth_api;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA auth TO auth_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA auth GRANT ALL PRIVILEGES ON TABLES TO auth_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA auth GRANT ALL PRIVILEGES ON SEQUENCES TO auth_api;

-- Create evidence database and evidence_api user
CREATE DATABASE evidence OWNER postgres;
GRANT ALL PRIVILEGES ON DATABASE evidence TO postgres;

-- Connect to evidence database to create evidence_api user and schema
\c evidence postgres

CREATE ROLE evidence_api WITH LOGIN PASSWORD 'evidence_api_dev_password';
GRANT CONNECT ON DATABASE evidence TO evidence_api;

CREATE SCHEMA IF NOT EXISTS evidence AUTHORIZATION evidence_api;
GRANT ALL PRIVILEGES ON SCHEMA evidence TO evidence_api;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA evidence TO evidence_api;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA evidence TO evidence_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA evidence GRANT ALL PRIVILEGES ON TABLES TO evidence_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA evidence GRANT ALL PRIVILEGES ON SEQUENCES TO evidence_api;

-- Enable required extensions for temporal data
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- Create ieq database for time-series measurements (Ingestion.Api / Public.Api)
CREATE DATABASE ieq OWNER postgres;
GRANT ALL PRIVILEGES ON DATABASE ieq TO postgres;

-- Connect to ieq database to enable TimescaleDB and create least-privilege roles
\c ieq postgres

CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Read-write role owned by Ingestion.Api (runs migrations, writes measurements)
CREATE ROLE ingestion_api WITH LOGIN PASSWORD 'ingestion_api_dev_password';
GRANT CONNECT ON DATABASE ieq TO ingestion_api;

CREATE SCHEMA IF NOT EXISTS ieq AUTHORIZATION ingestion_api;
GRANT ALL PRIVILEGES ON SCHEMA ieq TO ingestion_api;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA ieq TO ingestion_api;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA ieq TO ingestion_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA ieq GRANT ALL PRIVILEGES ON TABLES TO ingestion_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA ieq GRANT ALL PRIVILEGES ON SEQUENCES TO ingestion_api;

-- Read-only role for the planned Public.Api (never writes, never migrates)
CREATE ROLE public_api WITH LOGIN PASSWORD 'public_api_dev_password';
GRANT CONNECT ON DATABASE ieq TO public_api;
GRANT USAGE ON SCHEMA ieq TO public_api;
GRANT SELECT ON ALL TABLES IN SCHEMA ieq TO public_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA ieq GRANT SELECT ON TABLES TO public_api;

-- Cross-database read access: Ingestion.Api validates incoming measurements
-- against the sensor catalog owned by Evidence.Api (sensor status, declared
-- parameters, API-key hash). Postgres roles are cluster-wide, so the same
-- ingestion_api login connects to the evidence database read-only. The evidence
-- tables do not exist yet at cluster-init time (the evidence-migrate container
-- creates them later as the postgres role), so grant via default privileges on
-- future postgres-owned tables rather than per-table.
\c evidence postgres

GRANT CONNECT ON DATABASE evidence TO ingestion_api;
GRANT USAGE ON SCHEMA evidence TO ingestion_api;
GRANT SELECT ON ALL TABLES IN SCHEMA evidence TO ingestion_api;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA evidence
    GRANT SELECT ON TABLES TO ingestion_api;
