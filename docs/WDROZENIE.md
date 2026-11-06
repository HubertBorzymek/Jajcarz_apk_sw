# Wdrożenie

Aplikacja działa na maszynie `vm-trumpfish` obok Trumpfisha: własne kontenery aplikacji
i bazy w `/opt/jajcarz`, a ruch przechodzi przez działającego tam Caddy.

**Stan: nic z tej instrukcji nie zostało jeszcze wykonane na maszynie.**

## Decyzje przed startem

| Decyzja | Rekomendacja |
|---|---|
| Adres aplikacji | Własna domena albo na start `jajcarz.<IP>.sslip.io`, działa bez zakupu i z certyfikatem |
| Zmiana w Trumpfishu | Dwie drobne zmiany poniżej, do uzgodnienia z właścicielem tamtego repozytorium |
| Pamięć maszyny | Przed startem sprawdź `free -h`. Jajcarz ma limit około 580 MB |

## 1. Zmiany po stronie Trumpfisha

Porty 80 i 443 zajmuje Caddy Trumpfisha, więc tylko on może przekazać ruch dalej.

W `deploy/compose.prod.yaml` Trumpfisha, w usłudze `caddy`:

```yaml
    environment:
      JAJCARZ_HOSTNAME: ${JAJCARZ_HOSTNAME}
    volumes:
      - /opt/jajcarz/Caddyfile.jajcarz:/etc/caddy/Caddyfile.jajcarz:ro
    networks:
      - default
      - jajcarz-proxy
```

oraz na końcu pliku:

```yaml
networks:
  jajcarz-proxy:
    external: true
```

W `deploy/Caddyfile` Trumpfisha, na końcu:

```text
import /etc/caddy/Caddyfile.jajcarz
```

Zmienną `JAJCARZ_HOSTNAME` dopisz do `/opt/trumpfish/.env`. Każde wdrożenie Trumpfisha
restartuje Caddy, więc te zmiany muszą być w jego repozytorium, a nie tylko na maszynie.

## 2. Przygotowanie maszyny

```bash
sudo mkdir -p /opt/jajcarz
docker network create jajcarz-proxy
```

Skopiuj do `/opt/jajcarz` pliki `compose.prod.yaml`, `Caddyfile.jajcarz`, `update.sh`
i `backup.sh` z folderu `deploy`, a `.env.example` jako `.env`. Uzupełnij `.env`.

## 3. Pierwsza wersja

Tag `vX.Y.Z` publikuje obraz w GHCR. Obraz jest publiczny, gdy publiczny jest pakiet,
więc po pierwszej publikacji sprawdź jego widoczność w ustawieniach pakietu.

```bash
cd /opt/jajcarz
./update.sh v0.5.0
```

Potem przeładuj Caddy Trumpfisha:

```bash
docker compose -f /opt/trumpfish/compose.prod.yaml --env-file /opt/trumpfish/.env up -d caddy
```

## 4. Konta i panel

Hasło podaj na standardowym wejściu. Nie wpisuj go jako argumentu polecenia:

```bash
docker compose -f compose.prod.yaml --env-file .env exec -T app dotnet Jajcarz.Server.dll user set hubert
```

Rejestracja panelu wypisuje token tylko raz:

```bash
docker compose -f compose.prod.yaml --env-file .env exec app dotnet Jajcarz.Server.dll panel set 6825dd21a970
```

W `config/secrets.h` w repozytorium firmware ustaw:

```c
#define SECRET_SERVER_URL    "wss://<adres>/ws/device"
#define SECRET_DEVICE_TOKEN  "<token>"
```

## 5. Kopie zapasowe

```bash
echo '30 3 * * * root /opt/jajcarz/backup.sh' | sudo tee /etc/cron.d/jajcarz-backup
```

Przywrócenie opisuje `backup.sh`. Sprawdź je przynajmniej raz.

## 6. Sprawdzenie

- [ ] `https://<adres>/healthz` zwraca `OK`.
- [ ] Trumpfish działa bez zmian.
- [ ] Logowanie na telefonie i zmiany na żywo działają.
- [ ] Panel łączy się po `wss` i ekran Panel pokazuje „Połączony”.
- [ ] „Dodaj do ekranu głównego” daje ikonę i pełny ekran.
- [ ] Aplikacja otwiera się w trybie samolotowym z ostatnią listą.
- [ ] Po `./update.sh` telefony pozostają zalogowane.
- [ ] Zrzut z `backup.sh` przywraca dane na próbnej bazie.
