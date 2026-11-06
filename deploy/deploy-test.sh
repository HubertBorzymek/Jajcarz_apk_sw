#!/usr/bin/env bash
#
# Próba wdrożenia, tylko w CI: pliki z deploy/ w katalogu tymczasowym, obraz z lokalnego
# rejestru i prawdziwy Caddy. Sprawdza drogę z docs/WDROZENIE.md bez maszyny docelowej.
# Użycie: deploy/deploy-test.sh <obraz>

set -euo pipefail

[ "${CI:-}" = true ] || { echo "Tylko w CI: skrypt usuwa wolumeny" >&2; exit 1; }

IMAGE=${1:?Podaj obraz}
SOURCE=$(cd "$(dirname "$0")" && pwd)
HOST=test.example.com
BASE="http://$HOST"
PASSWORD=haslo-proby
REGISTRY_PORT=5000

export COMPOSE_PROJECT_NAME=jajcarz-test
export JAJCARZ_IMAGE_REPO="localhost:$REGISTRY_PORT/jajcarz"
JAJCARZ_DIR=$(mktemp -d)
export JAJCARZ_DIR
JAR=$(mktemp -d)
chmod 777 "$JAR"

compose() {
    docker compose -f "$JAJCARZ_DIR/compose.prod.yaml" --env-file "$JAJCARZ_DIR/.env" "$@"
}

cleanup() {
    compose logs --tail 30 app 2>&1 || true
    docker logs --tail 30 jajcarz-test-caddy 2>&1 || true
    compose down --volumes > /dev/null 2>&1 || true
    docker rm -f jajcarz-test-caddy jajcarz-test-registry > /dev/null 2>&1 || true
}
trap cleanup EXIT

# Klient w sieci jajcarz-proxy. Idzie przez Caddy jak telefon, a ciasteczka zostają między wywołaniami.
client() {
    docker run --rm --network jajcarz-proxy -v "$JAR:/jar" curlimages/curl:8.11.1 \
        --fail --silent --show-error --cookie /jar/cookies --cookie-jar /jar/cookies \
        --connect-to "$HOST:80:jajcarz-test-caddy:80" "$@"
}

csrf() {
    client "$BASE/api/session" | sed 's/.*"csrfToken":"\([^"]*\)".*/\1/'
}

send() {
    local method=$1 path=$2 body=${3:-} token
    token=$(csrf)
    client -X "$method" -H "X-CSRF-TOKEN: $token" -H 'Content-Type: application/json' \
        ${body:+-d "$body"} "$BASE$path"
}

wait_for_app() {
    for _ in $(seq 1 60); do
        client --output /dev/null "$BASE/healthz" 2> /dev/null && return
        sleep 1
    done
    echo "Aplikacja nie odpowiada przez Caddy" >&2
    exit 1
}

echo "==> Przygotowanie"
docker network inspect jajcarz-proxy > /dev/null 2>&1 || docker network create jajcarz-proxy > /dev/null
docker run -d --name jajcarz-test-registry -p "$REGISTRY_PORT:5000" registry:2 > /dev/null
until curl --fail --silent "http://localhost:$REGISTRY_PORT/v2/" > /dev/null; do sleep 1; done

# Druga wersja musi być innym obrazem, inaczej compose nie wymieni kontenera.
docker tag "$IMAGE" "$JAJCARZ_IMAGE_REPO:v-first"
printf 'FROM %s\nLABEL jajcarz.test=second\n' "$IMAGE" \
    | docker build --quiet --tag "$JAJCARZ_IMAGE_REPO:v-second" - > /dev/null
docker push --quiet "$JAJCARZ_IMAGE_REPO:v-first" > /dev/null
docker push --quiet "$JAJCARZ_IMAGE_REPO:v-second" > /dev/null
docker rmi "$JAJCARZ_IMAGE_REPO:v-first" "$JAJCARZ_IMAGE_REPO:v-second" > /dev/null

cp "$SOURCE/compose.prod.yaml" "$SOURCE/update.sh" "$SOURCE/backup.sh" "$JAJCARZ_DIR/"
sed -e "s|^JAJCARZ_HOSTNAME=.*|JAJCARZ_HOSTNAME=$HOST|" \
    -e "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$(openssl rand -hex 32)|" \
    "$SOURCE/.env.example" > "$JAJCARZ_DIR/.env"

echo "==> Pierwsza wersja przez update.sh"
bash "$JAJCARZ_DIR/update.sh" v-first
docker run -d --name jajcarz-test-caddy --network jajcarz-proxy -e "JAJCARZ_HOSTNAME=$BASE" \
    -v "$SOURCE/Caddyfile.jajcarz:/etc/caddy/Caddyfile:ro" caddy:2-alpine > /dev/null
wait_for_app

echo "==> Konto i panel w działającym kontenerze"
echo "$PASSWORD" | compose exec -T app dotnet Jajcarz.Server.dll user set hubert
TOKEN=$(compose exec -T app dotnet Jajcarz.Server.dll panel set aabbccddeeff)
[ "${#TOKEN}" -eq 64 ] || { echo "Niepoprawny token" >&2; exit 1; }

echo "==> Ciasteczka są bezpieczne za proxy z https"
HEADERS=$(docker run --rm --network jajcarz-proxy curlimages/curl:8.11.1 --silent --dump-header - \
    --output /dev/null -H "Host: $HOST" -H 'X-Forwarded-Proto: https' http://jajcarz-app:8080/api/session)
echo "$HEADERS" | grep -qi '^set-cookie: jajcarz\.csrf=.*secure'

echo "==> Logowanie i dane przez Caddy"
send POST /api/session "{\"login\":\"hubert\",\"password\":\"$PASSWORD\"}"
PRODUCT=$(send POST /api/products '{"name":"Jajka"}' | sed 's/.*"id":"\([^"]*\)".*/\1/')
client "$BASE/api/products" | grep -q Jajka

echo "==> Kopia zapasowa"
bash "$JAJCARZ_DIR/backup.sh"
DUMP=$(ls "$JAJCARZ_DIR"/backups/jajcarz-*.sql.gz)
gunzip -c "$DUMP" | grep -q Jajka

echo "==> Aktualizacja nie wylogowuje"
BEFORE=$(compose ps -q app)
bash "$JAJCARZ_DIR/update.sh" v-second
[ "$(compose ps -q app)" != "$BEFORE" ] || { echo "Kontener nie został wymieniony" >&2; exit 1; }
wait_for_app
client "$BASE/api/session" | grep -q '"login":"hubert"'
client "$BASE/api/products" | grep -q Jajka

echo "==> Przywrócenie kopii po utracie danych"
send DELETE "/api/products/$PRODUCT"
if client "$BASE/api/products" | grep -q Jajka; then
    echo "Produkt nie został usunięty" >&2
    exit 1
fi
compose stop app
gunzip -c "$DUMP" | compose exec -T db psql --quiet --set ON_ERROR_STOP=1 --single-transaction \
    -U jajcarz -d jajcarz > /dev/null
compose start app
wait_for_app
client "$BASE/api/products" | grep -q Jajka

echo "==> Wdrożenie działa"
