-- Create auth database and auth_api user
CREATE DATABASE auth OWNER postgres;
GRANT ALL PRIVILEGES ON DATABASE auth TO postgres;

-- Connect to auth database to create auth_api user
\c auth postgres

CREATE ROLE auth_api WITH LOGIN PASSWORD 'auth_api_dev_password';
GRANT CONNECT ON DATABASE auth TO auth_api;
GRANT USAGE, CREATE ON SCHEMA public TO auth_api;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO auth_api;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO auth_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON TABLES TO auth_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON SEQUENCES TO auth_api;
