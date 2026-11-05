# Jajcarz

Aplikacja do panelu na lodówkę. Panel ma klawisze i diody pokazujące stan produktów:
jest, kończy się, nie ma. Zużywając produkt, klikasz klawisz, a w sklepie sprawdzasz
w aplikacji, co trzeba kupić.

To druga połowa systemu. Firmware panelu jest w repozytorium
[Jajcarz_esp_sw](https://github.com/HubertBorzymek/Jajcarz_esp_sw), a wspólny kontrakt
opisuje [docs/PROTOCOL.md](docs/PROTOCOL.md).

## Stan projektu

Działa serwer panelu: połączenie, synchronizacja stanów, konflikty i trwałe dane.
Interfejsu dla użytkownika jeszcze nie ma.

| Wersja | Zakres |
|---|---|
| `v0.1.0` | Serwer panelu i baza danych |
| `v0.2.0` | Logowanie, API, zmiany na żywo, lista produktów |
| `v0.3.0` | Widok zakupowy, PWA, praca przy słabym zasięgu |
| `v0.4.0` | Stan panelu, przypisywanie klawiszy, kolory i jasność |
| `v0.5.0` | Docker i wdrożenie testowe |
| `v1.0.0` | Wersja produkcyjna |

## Uruchomienie lokalne

Potrzebny jest .NET SDK 10. Otwórz `Jajcarz.slnx` w Visual Studio 2026 albo uruchom:

```powershell
cd src\Jajcarz.Server
dotnet run --launch-profile Jajcarz.Server
```

Serwer nasłuchuje na porcie 5080. Lokalnie używa pliku SQLite i przy pierwszym starcie
tworzy przykładowe produkty oraz panel z tokenem `test-token`, opisane
w `appsettings.Development.json`. Produkcja używa bazy Postgres.

## Testy

```powershell
dotnet test Jajcarz.slnx
```

Symulator panelu wysyła dokładnie te same wiadomości co firmware. Testy na prawdziwym
sprzęcie opisuje [docs/TESTY_MANUALNE.md](docs/TESTY_MANUALNE.md).

## Budowa

| Folder | Zawartość |
|---|---|
| `src/Jajcarz.Domain` | Produkty, klawisze i reguły konfliktów |
| `src/Jajcarz.Application` | Operacje aplikacji |
| `src/Jajcarz.Infrastructure` | Baza danych |
| `src/Jajcarz.Server` | Połączenie z panelem i uruchomienie serwera |
| `tests` | Testy reguł i symulator panelu |

Stan należy do produktu, a klawisz jest do niego tylko przypięty. Dzięki temu produkt
można przenieść na inny klawisz bez utraty stanu.
