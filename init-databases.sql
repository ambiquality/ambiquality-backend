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
