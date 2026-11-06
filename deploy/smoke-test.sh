#!/usr/bin/env bash
#
# Test obrazu w CI: start na czystym Postgresie, migracje, konto, panel, logowanie.
# Użycie: deploy/smoke-test.sh <obraz>

set -euo pipefail

IMAGE=${1:?Podaj obraz}
NETWORK=jajcarz-smoke
PASSWORD=smoke-only
CONNECTION="Host=db;Database=jajcarz;Username=jajcarz;Password=$PASSWORD"
ENVIRONMENT=(-e ASPNETCORE_ENVIRONMENT=Production -e Database__Provider=Postgres
    -e "ConnectionStrings__Jajcarz=$CONNECTION" -e AllowedHosts=localhost)

cleanup() {
    docker logs jajcarz-smoke-app 2>&1 | tail -30 || true
    docker rm -f jajcarz-smoke-app jajcarz-smoke-db > /dev/null 2>&1 || true
    docker network rm "$NETWORK" > /dev/null 2>&1 || true
}
trap cleanup EXIT

docker network create "$NETWORK" > /dev/null
docker run -d --name jajcarz-smoke-db --network "$NETWORK" --network-alias db \
    -e POSTGRES_DB=jajcarz -e POSTGRES_USER=jajcarz -e POSTGRES_PASSWORD="$PASSWORD" \
    postgres:17-alpine > /dev/null
until docker exec jajcarz-smoke-db pg_isready -U jajcarz -d jajcarz > /dev/null 2>&1; do sleep 1; done

echo "==> Polecenia na czystej bazie"
echo "haslo-smoke" | docker run -i --rm --network "$NETWORK" "${ENVIRONMENT[@]}" "$IMAGE" user set hubert
TOKEN=$(docker run --rm --network "$NETWORK" "${ENVIRONMENT[@]}" "$IMAGE" panel set aabbccddeeff)
[ "${#TOKEN}" -eq 64 ] || { echo "Niepoprawny token" >&2; exit 1; }

echo "==> Start aplikacji"
docker run -d --name jajcarz-smoke-app --network "$NETWORK" -p 8080:8080 "${ENVIRONMENT[@]}" "$IMAGE" > /dev/null
for _ in $(seq 1 60); do
    curl --fail --silent http://localhost:8080/healthz > /dev/null && break
    sleep 1
done
curl --fail --silent --show-error http://localhost:8080/healthz
echo

echo "==> Interfejs"
curl --fail --silent --show-error http://localhost:8080/ | grep -q '<div id="root">'
curl --fail --silent --show-error --output /dev/null http://localhost:8080/sw.js
curl --fail --silent --show-error --output /dev/null http://localhost:8080/manifest.webmanifest

echo "==> Logowanie i lista"
JAR=$(mktemp)
SESSION=$(curl --fail --silent --cookie-jar "$JAR" http://localhost:8080/api/session)
CSRF=$(echo "$SESSION" | sed 's/.*"csrfToken":"\([^"]*\)".*/\1/')
curl --fail --silent --show-error --cookie "$JAR" --cookie-jar "$JAR" \
    -H "X-CSRF-TOKEN: $CSRF" -H 'Content-Type: application/json' \
    -d '{"login":"hubert","password":"haslo-smoke"}' http://localhost:8080/api/session
curl --fail --silent --show-error --cookie "$JAR" http://localhost:8080/api/panels | grep -q aabbccddeeff

echo "==> Obraz działa"
