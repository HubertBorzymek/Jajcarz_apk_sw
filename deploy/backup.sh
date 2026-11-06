#!/usr/bin/env bash
#
# Nocny zrzut bazy, uruchamiany z crona na maszynie. Kopie są na tym samym dysku, więc
# chronią przed błędem w danych, ale nie przed utratą maszyny.
#
# Przywracanie:
#   gunzip -c backups/jajcarz-<czas>.sql.gz | docker compose -f compose.prod.yaml \
#       --env-file .env exec -T db psql -U jajcarz -d jajcarz

set -euo pipefail

DIR=/opt/jajcarz
BACKUPS="$DIR/backups"
KEEP_DAYS=14

mkdir -p "$BACKUPS"
DUMP="$BACKUPS/jajcarz-$(date -u +%Y%m%dT%H%M%SZ).sql.gz"

docker compose -f "$DIR/compose.prod.yaml" --env-file "$DIR/.env" exec -T db \
    pg_dump -U jajcarz -d jajcarz --format=plain | gzip -9 > "$DUMP"

# Pusty plik oznacza błąd, a stare kopie nie mogą wtedy zniknąć.
if [ "$(gunzip -c "$DUMP" | head -c 1 | wc -c)" -eq 0 ]; then
    rm -f "$DUMP"
    echo "Pusty zrzut, przerwano" >&2
    exit 1
fi

find "$BACKUPS" -name 'jajcarz-*.sql.gz' -mtime +"$KEEP_DAYS" -delete
