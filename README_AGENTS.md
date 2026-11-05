# README_AGENTS

Skrót dla agentów. Historia: `CHANGELOG_AGENTS.md`. Protokół: `docs/PROTOCOL.md`.
Przy każdej zmianie dopisz wpis do obu changelogów.

## Projekt

Aplikacja webowa do panelu na lodówkę: produkty ze stanem OK/LOW/OUT. Serwer jest
źródłem prawdy, a panel ESP32 łączy się z `/ws/device`. Komunikacja po polsku.
Repo publiczne: `https://github.com/HubertBorzymek/Jajcarz_apk_sw`, gałąź `main`.
Firmware: `C:\Users\leemi\Documents\Projects\Esp32\Jajcarz_sw`, gałąź `rev0`.

## Zasady właściciela

- Push tylko po zgodzie. Commit, gdy praca ma sens jako całość. Wersje jako tagi `vX.Y.Z`.
- Nie ruszać produkcji, Azure ani repo Trumpfisha bez wyraźnej zgody.
- Zmiana firmware lub protokołu: najpierw uzasadnić i uzgodnić. `docs/PROTOCOL.md`
  jest kopią pliku z repo firmware, a tamten jest wiążący. Zmieniaj oba.
- Sekrety nigdy w repo. Czysty kod: plik do ok. 300 linii, jedna odpowiedzialność.
- Nazwy w kodzie po angielsku, komentarze i dokumentacja po polsku.

## Warstwy

```text
src/Jajcarz.Domain/          produkty, klawisze, ConflictPolicy, bez EF i HTTP
src/Jajcarz.Application/     PanelService, ProductService, PanelLinks, IStore
src/Jajcarz.Infrastructure/  EF Core, EfStore, migracje (tylko Postgres)
src/Jajcarz.Server/          /ws/device, Program, seed lokalny
tests/Jajcarz.Domain.Tests/       reguły
tests/Jajcarz.Integration.Tests/  FakePanel, wiadomości jak z firmware
```

## Wiążące decyzje

- Stan należy do produktu. `PanelKey` tylko wskazuje produkt, a produkt ma najwyżej
  jeden klawisz. Rev klawisza w protokole to `Product.Rev`.
- Jeden licznik `RevCounters` dla wszystkich produktów. Ma luki, firmware tylko porównuje.
- Przypięcie daje nowy rev i `BoundAt`. Autor i czas zmiany zostają.
- `ConflictPolicy`: `age_ms` porównuje z `max(ChangedAt, BoundAt)`, `base_rev` z `Rev`.
  Dzięki temu stare kliknięcie nie trafi na nowy produkt.
- Ponowienia: `LastChangeKey` + ten sam stan = `Duplicate`, bez podnoszenia rev.
- Każda zmiana dostaje `ack`. Klawisz bez produktu: `accepted: false`, potem `snapshot`.
- Kilka zmian na panelu naraz: jeden `snapshot`, bo kolejka panelu ma 8 miejsc.
- `snapshot` tylko z klawiszami z ostatniego `hello`. Panel odrzuca całość powyżej 32.
- Firmware stosuje `snapshot` bez sprawdzania rev, więc kolejność wysyłki musi być
  taka jak zmian: `BeginWriteAsync` to blokada na proces, a `Send` wołamy przed jej
  zwolnieniem. Wysyłkę robi kolejka w `PanelConnection`.
- Aplikacja działa w jednym egzemplarzu: blokada i `PanelLinks` są w pamięci.
- Zerwanie wykrywa ping serwera (`KeepAliveTimeout`), bo pingi panelu nie docierają do kodu.
- Token panelu: w bazie tylko SHA-256, `device_id` z `hello` musi zgadzać się z tokenem.
- Nowy panel na pustej bazie jest ciemny, dopóki produkty nie zostaną przypięte.

## Środowisko

- .NET SDK 10.0.401, Node 24. Brak lokalnie: `az`, `docker`, `psql`, `gh`.
- Lokalnie SQLite (`EnsureCreated`), w produkcji Postgres (migracje). Bez Dockera.
- Testy: `dotnet test Jajcarz.slnx`. Na Postgresie: zmienna `JAJCARZ_TEST_POSTGRES`,
  działa w CI. Migracja: `dotnet ef migrations add <Nazwa> --project
  src/Jajcarz.Infrastructure --startup-project src/Jajcarz.Infrastructure --output-dir Data/Migrations`.
- Serwer lokalny: `dotnet run --launch-profile Jajcarz.Server` w `src/Jajcarz.Server`,
  port 5080, dane z `DevSeed`, token `test-token`.
- Nie używaj `dotnet run --environment`: ustawienie nie dociera do aplikacji.

## Plan

- `v0.1.0` panel i dane, `v0.2.0` logowanie, API, SSE, lista, `v0.3.0` zakupy i PWA,
  `v0.4.0` ekran panelu, `v0.5.0` Docker i wdrożenie testowe, `v1.0.0` produkcja.
- Wdrożenie: wspólna maszyna `vm-trumpfish`, własne kontenery i Postgres w `/opt/jajcarz`,
  własna nazwa hosta przez Caddy Trumpfisha. React + Vite + PWA, SSE do przeglądarki.
- Konta: 4 osoby, bez rejestracji, cookie roczne. Produkt: nazwa i opcjonalna kategoria.
- „Kupione” w sklepie: od razu `OK`, przekreślenie do wyjścia z widoku, chwila na cofnięcie.

## Otwarte

- SW2 (`0.1`, GPIO33) nie zgłasza naciśnięć. Podejrzany sprzęt, potrzebny pomiar na płytce.
- `LastSeenAt` zmienia się tylko przy części wiadomości. Popraw przed ekranem panelu w `v0.4.0`.
- Brak domeny. Właściciel jej nie ma, a decyzja o zakupie należy do niego.
- Przed `v0.5.0`: odczyt pamięci maszyny i ustalenie dostępu do Azure.
