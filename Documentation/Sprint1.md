# Sprint 1 — Proiektuaren oinarria, protokoloa eta autentifikazioa

## Zer eraiki da

- **Soluzioa**, `AgroCore.Shared`, `AgroCore.Server` eta `AgroCore.Tests` proiektuekin.
- **AgroCore.Shared**: ereduak (`User`, `Farm`, `Machine`, `Field`, `FarmTask`, `Charger`, `Position`,
  `LogEntry`, `Telemetry`...), enum-ak, DTOak, rol bakoitzaren baimen-taula eta **protokoloaren mezu guztiak**.
- **Protokoloa**: lerro bakarreko JSON bat mezu bakoitzeko, TCP bidez. Mezu bakoitzak `type` eremu bat du
  (`LOGIN_REQUEST`, `TASK_ASSIGNMENT`...). Jasotzaileak `type` irakurri eta `switch` bat egiten du.
- **`Connection` klasea**: lerroak bidali/jasotzen ditu TCP bidez (`StreamReader.ReadLine` / `StreamWriter.WriteLine`).
  Bidalketa `lock` baten barruan doa, hainbat harik socket berean idatz dezaketelako.
- **Bezero anitzeko TCP zerbitzaria** (`TcpServer`): `TcpListener` + `AcceptTcpClient()` + `Thread` bat bezero bakoitzeko.
- **Saioa hasi zerbitzariaren aurka**: erabiltzailea bilatu → pasahitza egiaztatu → erabiltzailea aktibo dagoen → rola → ustiategia.
  Aldi baterako blokeoa 5 saiakera huts egin ondoren. Mezu generikoa: "Erabiltzailea edo pasahitza okerrak dira".
- **Pasahitzak** PBKDF2-SHA256 bidez (100.000 iterazio, ausazko gatza). Inoiz ez testu arruntean.
- **Saioak** zerbitzarian (`SessionManager`, `ConcurrentDictionary`-rekin).
- **Erabiltzaileen kudeaketa** (administratzaileak bakarrik): nekazaria sortu (`User` + `Farm` sortzen ditu,
  soro, makina eta kargagailuekin), operadorea/behatzailea sortu, editatu, desaktibatu, ezabatu, flota-gakoa birsortu.
- **Pasahitza aldatzea**, uneko pasahitza egiaztatuta.
- **Gertaeren erregistroa** ekoizle/kontsumitzaile ereduarekin (`BlockingCollection` + idazle-haria):
  testu-fitxategi irakurgarria eta `events.jsonl` estatistiketarako.
- **Iraunkortasuna** `data/agrocore-data.json` fitxategian (`lock` bidez babestuta).

## Adibide-datuak

| Erabiltzailea | Pasahitza | Rola | Ustiategia |
|---|---|---|---|
| admin | admin123 | Administratzailea | (guztiak) |
| juan | juan123 | Nekazaria | Esperantza baserria (6 soro, 11 makina, 3 kargagailu) |
| maria | maria123 | Nekazaria | Olibondoak baserria (4 soro, 7 makina, 2 kargagailu) |
| pedro | pedro123 | Nekazaria | Haritzdi baserria (3 soro, 5 makina, 2 kargagailu) |
| operador | operador123 | Operadorea | Esperantza baserria |
| visor | visor123 | Behatzailea | Esperantza baserria |

## Nola egiaztatu

```
dotnet build AgroCore.sln
dotnet test AgroCore.sln
dotnet run --project src/AgroCore.Server
```

Proben artean zerbitzariaren aurkako **benetako saio-hasiera bat TCP bidez** dago (pasahitz okerra, zuzena,
sarbidea ukatuta erabiltzaileen zerrenda eskatzen duen nekazari bati, eta saioa ixtea).
