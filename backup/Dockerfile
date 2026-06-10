# Backup sidecar (SPO-04): daily pg_dump of every platform database, pushed
# off-site to an S3-compatible bucket. Built FROM the same image as the postgres
# service so the client tools always match the server major version.
FROM docker.io/timescale/timescaledb:2.27.0-pg18 AS final

RUN apk add --no-cache aws-cli

COPY backup/backup.sh backup/entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
