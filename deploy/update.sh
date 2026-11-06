#!/usr/bin/env bash
#
# Aktualizacja na maszynie: ./update.sh v0.5.0
# Zapisuje wersję w .env, pobiera obraz i czeka, aż nowy kontener będzie zdrowy.

set -euo pipefail

VERSION=${1:?Podaj wersję, np. v0.5.0}
DIR=/opt/jajcarz
ENV_FILE="$DIR/.env"
HEALTH_TIMEOUT=180

compose() {
    docker compose -f "$DIR/compose.prod.yaml" --env-file "$ENV_FILE" "$@"
}

sed -i "s|^JAJCARZ_IMAGE=.*|JAJCARZ_IMAGE=ghcr.io/hubertborzymek/jajcarz:$VERSION|" "$ENV_FILE"
compose pull app
compose up -d

CONTAINER=$(compose ps -q app)
deadline=$((SECONDS + HEALTH_TIMEOUT))
until [ "$(docker inspect --format '{{.State.Health.Status}}' "$CONTAINER")" = healthy ]; do
    if [ "$SECONDS" -ge "$deadline" ]; then
        echo "Kontener nie jest zdrowy po $HEALTH_TIMEOUT s" >&2
        compose logs --tail 50 app >&2
        exit 1
    fi
    sleep 5
done
echo "Działa wersja $VERSION"
