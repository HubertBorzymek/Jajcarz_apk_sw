#!/usr/bin/env bash
#
# Nocny zrzut bazy, uruchamiany z crona na maszynie. Kopie są na tym samym dysku, więc
# chronią przed błędem w danych, ale nie przed utratą maszyny.
#
# Przywracanie zastępuje całą zawartość bazy, a przy błędzie niczego nie zmienia:
#   docker compose -f compose.prod.yaml --env-file .env stop app
#   gunzip -c backups/jajcarz-<czas>.sql.gz | docker compose -f compose.prod.yaml \
#       --env-file .env exec -T db psql --set ON_ERROR_STOP=1 --single-transaction \
#       -U jajcarz -d jajcarz
#   docker compose -f compose.prod.yaml --env-file .env start app

set -euo pipefail

DIR=${JAJCARZ_DIR:-/opt/jajcarz}
BACKUPS="$DIR/backups"
KEEP_DAYS=14

mkdir -p "$BACKUPS"
DUMP="$BACKUPS/jajcarz-$(date -u +%Y%m%dT%H%M%SZ).sql.gz"
PARTIAL="$DUMP.partial"
trap 'rm -f "$PARTIAL"' EXIT

# Stare kopie znikają dopiero po udanym zrzucie, a przerwany zrzut nie udaje kopii.
docker compose -f "$DIR/compose.prod.yaml" --env-file "$DIR/.env" exec -T db \
    pg_dump -U jajcarz -d jajcarz --format=plain --clean --if-exists | gzip -9 > "$PARTIAL"
mv "$PARTIAL" "$DUMP"

find "$BACKUPS" -name 'jajcarz-*.sql.gz' -mtime +"$KEEP_DAYS" -delete
