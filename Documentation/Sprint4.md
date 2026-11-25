# Sprint 4 — Estatistikak, PDF txostenak, gertaeren erregistroa, profila eta administrazioa

## Zer eraiki da

### PDF txostenak (`Services/PdfReportGenerator.cs` + `Views/ReportsPage`)
- Txostena nekazari bakoitzeko sortzen da, **QuestPDF** liburutegiarekin (Community lizentzia, erabilera akademikoa).
- **Txostenak** orrian aldia aukeratzen da: azken ordua, gaur, azken 7 egunak, azken 30 egunak edo hasieratik.
- Urratsak:
  1. Kontrol Zentroak `REPORT_REQUEST` bidaltzen dio zerbitzariari (ustiategia + aldia).
  2. Zerbitzariak baimena (`GenerateReports`) eta ustiategia egiaztatzen ditu, eta **ustiategi horren datuak
     bakarrik** itzultzen ditu `REPORT_RESPONSE` mezuan (`ReportData`).
  3. Kontrol Zentroak non gorde galdetzen du (`SaveFileDialog`) eta PDFa sortzen du. "Ireki" botoiak
     sortutako fitxategia irekitzen du.
- PDFaren egitura:
  - **Goiburua**: AGROCORE, "ERAGIKETA-TXOSTENA", sortze-data, nork sortu duen, nekazaria, ustiategia,
    aldia eta flota (makina eta soro kopurua).
  - **1. Laburpena**: osatutako, bertan behera utzitako eta etendako zereginak, distantzia, ereindako /
    ureztatutako / fumigatutako / uzta bildutako soroak, jarduera-, itxaronaldi- eta karga-denbora,
    kontsumitutako bateria, saihestutako gatazkak eta elkarblokeoak, karga kopurua eta larrialdiko geldialdiak.
  - **2. Soroak**: egoera, laborea, hazkuntza, labore-plana (birlandaketa automatikoa "(auto)") eta uzta-bilketak.
  - **3. Makinak**: makina bakoitzaren jarduera (distantzia, lanean, zain, kargatzen, zereginak, kargak, eraginkortasuna).
  - **4. Zereginak**: aldiko zereginak (gehienez 80 lerro, gainerakoak "... eta N zeregin gehiago").
  - **5. Gorabeherak**: aldiko abisuak, erroreak eta segurtasun-gertaerak (`Warning`, `Error`, `Security`).
  - **Orri-zenbakiak** orri-oinean: "AGROCORE · ustiategia · X. orria / Y".

### Estatistikak (`Views/StatisticsPage`)
- Orria ikusgai jartzean edo "Freskatu" sakatzean `STATISTICS_REQUEST` bidaltzen da.
- Zerbitzariak (`StatisticsService`) estatistikak **erregistroko gertaerak zenbatuz** kalkulatzen ditu
  (`events.jsonl`), eta makina bakoitzaren kontagailu metatuekin.
- **Guztizkoak** txarteletan: osatutako zereginak, ereindako / ureztatutako / fumigatutako / uzta bildutako soroak,
  distantzia, jarduera-, itxaronaldi- eta karga-denbora, saihestutako gatazkak eta elkarblokeoak, larrialdiak.
- **Barra-diagrama**: makina bakoitzak osatutako zereginak (kolorea makina motaren arabera).
- **Makina bakoitzeko taula**: distantzia, aktibo, lanean, mugimenduan, zain, kargatzen, erabilitako bateria,
  zereginak, etenaldiak, kargak eta eraginkortasuna.
- **Taula globala** (administratzaileak bakarrik, `ViewGlobalStatistics` baimena): `GLOBAL_STATISTICS_REQUEST`
  bidez ustiategi guztien datuak (zereginak, ereindakoak, uzta bildutakoak, distantzia, gatazkak, larrialdiak,
  ukatutako sarbideak).

### Gertaeren erregistroa (`Views/LogsPage`)
- Ustiategiaren azken 400 gertaerak `LOGS_REQUEST` bidez eskatzen ditu.
- **5 segundoro freskatzen da** (`DispatcherTimer`) orria ikusgai dagoen bitartean; ezkutatzean gelditu egiten da.
- **Iragazkia**: testu-bilaketa eta "garrantzitsuak bakarrik" (abisuak, erroreak eta segurtasuna).
  Larritasun-maila bakoitzak bere kolorea du.

### Nire profila (`Views/ProfilePage`)
- Erabiltzailearen datuak: izena, erabiltzailea, rola, ustiategia, saio-hasieraren data eta rolaren baimenak.
- **Pasahitza aldatu**: `CHANGE_PASSWORD_REQUEST`. Kontrol Zentroak bi pasahitz berriak berdinak direla
  egiaztatzen du; zerbitzariak **uneko pasahitza** egiaztatzen du eta erregistroan gordetzen du.

### Administrazioa (`Views/AdminPage`, administratzaileak bakarrik)
- **Erabiltzaileak** (`LIST_USERS`): taula ekintzekin:
  - editatu (izen osoa eta, nekazaria bada, ustiategiaren izena) — `UPDATE_USER`;
  - pasahitza aldatu — `UPDATE_USER`;
  - gaitu / desgaitu — `SET_USER_ACTIVE`;
  - ezabatu, berrespenarekin (nekazaria bada, bere ustiategia ere bai: soroak, makinak eta erabiltzaileak) — `DELETE_USER`.
- **Ustiategiak**: zerrenda (nekazaria, soroak, makinak, konektatutako makinak, kargagailuak) eta
  **flota-gakoa birsortzea** (`REGENERATE_FLEET_KEY`); gako zaharra erabiltzen duten makinak ezin dira gehiago konektatu.
- **Sistemaren konfigurazioa** (`GET_CONFIG` / `UPDATE_CONFIG`): laboreen hazkuntza-denbora (azenarioa, artoa,
  tomatea), uzta bildu ondoren sorora itzultzeko denbora, bateria baxuaren muga (5–60), karga-abiadura,
  saio-hasierako saiakera kopurua eta blokeo-denbora.
- **Altak**:
  - **nekazari berria** (`CREATE_FARMER`): erabiltzailea eta bere ustiategia sortzen ditu soro, kargagailu,
    ereile, ureztatzaile, fumigazio-drone eta uzta-biltzaileekin. Flota-gakoa behin bakarrik erakusten da,
    flota abiarazteko aginduarekin batera;
  - **operadorea / behatzailea** (`CREATE_USER`), ustiategi bati lotuta.

### Zerbitzaria (`src/AgroCore.Server/Clients/ControlCenterClient.cs`)
- `switch` berean mezu berriak atenditzen dira: `CHANGE_PASSWORD_REQUEST`, `LIST_USERS`, `CREATE_FARMER`,
  `CREATE_USER`, `UPDATE_USER`, `SET_USER_ACTIVE`, `DELETE_USER`, `REGENERATE_FLEET_KEY`, `GET_CONFIG`,
  `UPDATE_CONFIG`, `LOGS_REQUEST` eta, `ControlCenterClient.Farm.cs` fitxategian, `STATISTICS_REQUEST`,
  `GLOBAL_STATISTICS_REQUEST` eta `REPORT_REQUEST`.
- Eskaera bakoitzean, zerbitzarian:
  - `Require(...)`: erabiltzailearen rolak baimena duen. Ez badu: "Sarbidea ukatuta..." erantzuten da
    eta `Security` gertaera gisa erregistratzen da;
  - `ResolveFarm(...)`: **datuen isolamendua**. Erabiltzaile arruntak beti bere ustiategia jasotzen du;
    beste bat eskatzen badu, ukatu egiten da. Administratzaileak edozein eska dezake.
- Zerbitzaria abiaraztean TCP (5000) edo UDP (5001) ataka **erabiltzen ari badira**, errore argi bat
  idazten du kontsolan ("Beste AGROCORE zerbitzari bat martxan al dago?") eta 1 irteera-kodearekin amaitzen da.

### Simulazioaren abiadura (x1, x2, x5, x10)

Aginte-paneleko "FLOTAREN KONTROLA" barran **DENBORA** botoiak daude: x1 / x2 / x5 / x10.
Denbora azkartzen dute probak egiteko (gehienez x10), eta uneko abiadura berdez nabarmentzen da
eta egoera-barran ere agertzen da ("Denbora: x10").

- WPF -> zerbitzaria: `SET_SIMULATION_SPEED` mezua (`SimulationSpeedMessage`: `FarmId`, `Speed`).
  `GlobalCommands` baimena behar da (administratzailea, nekazaria eta operadorea).
- Zerbitzariak `FarmRuntime.SimulationSpeed`-en gordetzen du, laboreen hazkundea azkartzen du
  (`GrowthService`: `seconds * farm.SimulationSpeed`) eta mezu bera konektatutako makina guztiei bidaltzen die.
  Konektatzen den makina berriak `MachineAuthResponse.SimulationSpeed`-en jasotzen du.
- Makinan, `SimulationClock`-ek abiadura gordetzen du. Makinak 20 urrats egiten ditu segundoko beti,
  baina urrats bakoitzean `Speed` aldiz denbora gehiago simulatzen du: mugimendua, bateria-kontsumoa,
  karga eta tenperatura. Guneen erreserba gelaxkaz gelaxka egiten denez, talka-saihestea x10-ean ere bermatuta dago.

## Proba automatikoak (21)

```
dotnet test AgroCore.sln
```

- `SecurityTests` (9): hash-a (testu arruntik ez, gatz desberdina), rol bakoitzaren baimenak, saio-hasiera zuzena,
  pasahitz okerra (mezu generikoa), desgaitutako erabiltzailea, blokeoa hainbat hutsegiteren ondoren,
  pasahitza aldatzeko uneko pasahitza behar da, nekazaria sortzeak erabiltzailea eta ustiategia lotuta sortzen ditu.
- `ProtocolTests` (5): JSONaren formatua, enum-ak maiuskulaz, basetik sorora bidea, saio-hasiera TCP bidez
  eta administratzaileak erabiltzaileak zerrendatzea.
- `ConcurrencyTests` (7): guneak, elkarblokeoak, STOP, kargagailuen FIFO ilara, zereginen mendekotasunak,
  makinarik egokiena eta `NEEDS_CHARGE` (ikus Sprint 2).

## Nola probatu

```
dotnet build AgroCore.sln
dotnet test AgroCore.sln

# 1. terminala: zerbitzaria
dotnet run --project src/AgroCore.Server

# 2. terminala: Juanen flota (makina-bezeroak)
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 1 --key AGRO-DEMO-1 --windows

# 3. terminala: Kontrol Zentroa
dotnet run --project src/AgroCore.ControlCenter
```

- Sartu `juan / juan123` gisa: **Estatistikak**, **Txostenak** (sortu PDF bat), **Gertaeren erregistroa** eta
  **Nire profila** (aldatu pasahitza).
- Sartu `admin / admin123` gisa: **Administrazioa** orria (erabiltzaileak, ustiategiak, flota-gakoa, konfigurazioa,
  nekazari berria) eta estatistika globalen taula.
- Zerbitzaria bi aldiz abiarazten bada, bigarrenak atakak okupatuta daudela adierazi eta amaitu egiten du.
