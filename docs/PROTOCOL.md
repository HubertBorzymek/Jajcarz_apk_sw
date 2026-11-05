# Protokół Jajcarz

Wersja protokołu: **1**.

Wspólny kontrakt dla firmware ESP32, serwera testowego i aplikacji webowej.
Zmiana niezgodna wstecz wymaga podniesienia pola `v`.

## Pojęcia

| Pojęcie | Znaczenie |
|---|---|
| `device_id` | 12 znaków hex z adresu MAC, np. `6825dd21a970` |
| `addr` | adres klawisza `segment.klawisz`, np. `0.2` |
| `state` | `OK` (jest), `LOW` (kończy się), `OUT` (nie ma) |
| `rev` | licznik zmian, nadawany wyłącznie przez serwer |

ESP nie zna produktów. Przypisanie `produkt -> device_id + addr` istnieje tylko
w aplikacji webowej. Numer segmentu wynika z adresu sprzętowego modułu, a nie
z jego pozycji, więc przepięcie modułu nie gubi przypisań.

## Źródło prawdy

Serwer przechowuje dla każdego klawisza ostatnią zmianę:

| Pole | Znaczenie |
|---|---|
| `state` | aktualny stan |
| `rev` | wartość licznika z chwili tej zmiany |
| `changed_by` | kto zmienił, np. `device:6825dd21a970`, `web:<id_użytkownika>` |
| `changed_at` | czas zmiany według zegara serwera, UTC, ISO 8601 |

Klient porównuje `rev`: jeśli serwer ma wyższy, klient się aktualizuje.
`changed_by` i `changed_at` służą do wyświetlania informacji i diagnostyki,
nie do decydowania o aktualizacji.

`rev` zawsze rośnie, ale może mieć luki, a nowa wartość nie musi oznaczać zmiany
stanu. Rośnie na przykład przy przypięciu produktu do klawisza. Klient tylko
porównuje wartości i nie wyciąga wniosków z różnicy między nimi.

## Połączenie

- Adres: `wss://<host>/ws/device`, a w sieci lokalnej dla testów `ws://`.
- Autoryzacja: nagłówek `Authorization: Bearer <token>` przy nawiązywaniu
  połączenia. Każde urządzenie ma własny token.
- Połączenie zawsze inicjuje ESP.
- ESP wysyła ping WebSocket co 30 s, gdy jest połączone.
- Każda wiadomość to obiekt JSON z polami `v` (wersja) i `t` (typ).

## Przebieg

1. ESP łączy się i wysyła `hello`.
2. Serwer odpowiada `snapshot`. To podstawowa synchronizacja i występuje przy
   każdym połączeniu, ponieważ panel może długo być offline lub uśpiony.
3. ESP wysyła oczekujące zmiany jako `state_change`.
4. Serwer odpowiada `ack` na każdą z nich.
5. Dopóki połączenie trwa, serwer przesyła zmiany na bieżąco jako `state_set`.

`state_set` tylko przyspiesza aktualizację. Utrata takiej wiadomości jest
nieszkodliwa, bo następny `snapshot` wyrówna stan.

## Wiadomości ESP -> serwer

### `hello`

```json
{ "v": 1, "t": "hello", "device_id": "6825dd21a970", "fw": "v0.2.0",
  "segments": [ { "seg": 0, "keys": 4 } ] }
```

Wysyłane po połączeniu i ponownie po zmianie zestawu modułów.

### `state_change`

```json
{ "v": 1, "t": "state_change", "seq": 17, "addr": "0.2", "state": "LOW",
  "age_ms": 5400 }
```

- `seq`: wiąże zmianę z `ack`. Po restarcie ESP zaczyna od 1, więc serwer tylko
  odsyła tę wartość i nie wyciąga z niej wniosków. Brak `ack` przez 5 s powoduje
  ponowną wysyłkę, a serwer musi to bezpiecznie obsłużyć.
- `age_ms`: ile czasu temu kliknięto.
- `base_rev`: wysyłany zamiast `age_ms`, gdy czas jest nieznany, czyli po restarcie
  panelu. To `rev` klawisza znany panelowi w chwili zmiany, np. `"base_rev": 121`.

### `unassigned_press`

```json
{ "v": 1, "t": "unassigned_press", "addr": "0.3" }
```

Naciśnięcie klawisza bez produktu. Pozwala przypisać produkt w aplikacji przez
naciśnięcie klawisza. Nie jest kolejkowane offline.

## Wiadomości serwer -> ESP

### `snapshot`

```json
{ "v": 1, "t": "snapshot", "rev": 128,
  "keys": [
    { "addr": "0.0", "assigned": true, "state": "OK", "rev": 121 },
    { "addr": "0.1", "assigned": true, "state": "OUT", "rev": 128 },
    { "addr": "0.2", "assigned": false }
  ],
  "config": { "brightness": 51,
    "colors": { "OK": [0, 255, 0], "LOW": [255, 120, 0], "OUT": [255, 0, 0] } } }
```

ESP stosuje `snapshot`, z wyjątkiem klawiszy z niewysłaną zmianą. Te rozstrzyga
`ack`.

Serwer może wysłać `snapshot` w dowolnej chwili połączenia: po zmianie przypisań,
a przy zmianie wielu klawiszy naraz zamiast serii `state_set`.

### `ack`

```json
{ "v": 1, "t": "ack", "seq": 17, "addr": "0.2", "accepted": true,
  "state": "LOW", "rev": 129 }
```

Odpowiedź zawsze zawiera stan obowiązujący na serwerze. Przy `accepted: false`
ESP ustawia na klawiszu ten stan.

Zmiana klawisza bez produktu dostaje `accepted: false` ze stanem `OK` i bieżącym
`rev`, a zaraz po niej serwer wysyła `snapshot`, który gasi ten klawisz.

### `state_set`

```json
{ "v": 1, "t": "state_set", "addr": "0.1", "state": "OK", "rev": 130,
  "changed_by": "web:user1", "changed_at": "2026-11-04T17:20:31Z" }
```

ESP ignoruje wiadomość, jeśli `rev` nie jest wyższy od znanego dla klawisza.

### `config`

```json
{ "v": 1, "t": "config", "brightness": 30,
  "colors": { "OK": [0, 255, 0], "LOW": [255, 120, 0], "OUT": [255, 0, 0] } }
```

ESP stosuje i zapisuje konfigurację od razu. Jest ona wysyłana po zmianie,
a jej aktualna wersja zawsze znajduje się też w `snapshot`.

### `identify`

```json
{ "v": 1, "t": "identify", "addr": "0.2", "duration_ms": 3000 }
```

Dioda klawisza miga przez podany czas. To jedyny wyjątek od stałego świecenia
diod produktów.

## Rozstrzyganie konfliktów

Wygrywa nowsza zmiana.

1. Serwer liczy czas zmiany: `czas_odebrania - age_ms`.
2. Jeśli ten czas jest nowszy niż `changed_at` klawisza, zmiana jest przyjęta,
   a `rev` rośnie.
3. W przeciwnym razie serwer odsyła `accepted: false` i obowiązujący stan.

Zmiana z `base_rev` powstała przed awarią, więc jej czas jest nieznany i wygrywa
serwer. Jest przyjęta tylko wtedy, gdy `rev` klawisza na serwerze nie jest wyższy
niż `base_rev`, czyli nikt nie zmienił klawisza w tym czasie. Jako `changed_at`
serwer zapisuje czas odebrania. Wiadomość bez `age_ms` i `base_rev` jest odrzucana.

Po przyjęciu zmiany serwer powiadamia pozostałych klientów.
