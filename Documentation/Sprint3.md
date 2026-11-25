# Sprint 3 — WPF Kontrol Zentroa eta 3D ikuspegia

## Zer eraiki da (`AgroCore.ControlCenter`, .NET 8 WPF)

### Saioa hasi zerbitzariaren aurka
- Saio-hasierako leihoa erabiltzailea, pasahitza eta zerbitzaria eremuekin (`host` edo `host:ataka`).
- Konexioa **bigarren mailako hari batean** egiten da eta emaitza interfazera itzultzen da
  `Dispatcher.BeginInvoke` bidez (leihoa ez da izozten).
- Mezu argiak: kredentzial okerrak ("Erabiltzailea edo pasahitza okerrak dira"), kontu blokeatua
  ("Saiakera gehiegi..."), zerbitzaria itzalita. "Sarbide-arazoak?" esteka.
- **Saio lokala** `%APPDATA%\AgroCore\session.json` fitxategian (inoiz ez pasahitza ez tokena).
  Saioa ixtean ezabatu egiten da eta saio-hasierako leihora itzultzen da.

### Komunikazioa (klaseko adibidearen eskema bera)
- `ServerConnection`: `TcpClient` + `StreamReader`/`StreamWriter`; **irakurle-hari bat**
  `ReadLine()` begiztan duela. Mezu bakoitza interfazeko harira pasatzen da eta han `switch` bat egiten da `type`-ren arabera.
- `TelemetryReceiver`: `UdpClient` ataka libre batean + hartzaile-haria. Ataka zerbitzariari
  `UDP_SUBSCRIBE` bidez jakinarazten zaio; telemetria 200 ms-ro iristen da.
- Birkonexio-logikarik gabe: konexioa galtzen bada, abisua ematen da eta saio-hasierara itzultzen da.

### Aginte-panel pertsonalizatua
- Erabiltzailearen ustiategiaren KPIak: makinak, soroak, batez besteko bateria, lanean, kargatzen, erabilgarri.
- **Low-poly 3D ikuspegia**, `Viewport3D`-rekin egina (liburutegirik eta deskargatutako eredurik gabe):
  - Uzta-biltzailea (erreferentziako ereduan oinarritua), ereilea (traktorea + ereilea),
    ureztatzailea depositu eta ureztatze-barrarekin, eta **fumigazio-droneak 4 errotorerekin**.
  - Animazioak: errotoreak biraka, uzta-biltzailearen errota, fumigazio-hodeia, ura, hautsa.
  - Laboreak hazkuntzaren arabera aldatzen dira (azenarioa, artoa buruekin, tomate-landareak
    tomate berdeak→gorriak); ereitean/uzta biltzean landutako zatia bakarrik ikusten da.
  - Telemetria-paketeen arteko mugimendu interpolatua (teletransportik gabe).
  - Kargagailuak argi berde/laranjarekin, ukuilua, siloa, zuhaitzak, bideak.
  - 2D etiketak izenarekin eta bateriarekin; kamera orbitala; klik makina bat hautatzeko;
    "jarraitu hautapenari"; erreserbatutako guneak ikusi (talkak saihestea).
- Hautatutako makinaren fitxa START / STOP / RESUME / BASERA ITZULI botoiekin.
- Alertak (bateria baxua, itxaronaldiak, saihestutako elkarblokeoak...).
- Flota osorako aginduak: GELDITU GUZTIAK, BERREKIN GUZTIAK, GUZTIAK BASERA, LARRIALDIKO GELDIALDIA
  (berrespenarekin).

### Beste orri batzuk
- **Makinak**: denbora errealeko taula + makina bakoitzeko aginduak + fitxa teknikoa (gaitasun desberdinak).
- **Soroak (planifikatzailea)**: laborea aukeratu → zerbitzariak zereginak sortzen ditu; birlandaketa automatikoa;
  labore-plana bertan behera utzi.
- **Zereginak**: PENDING/WAITING/ASSIGNED/RUNNING/COMPLETED/CANCELLED egoerak (Egiteko, Zain, Esleituta,
  Martxan, Osatuta, Bertan behera utzita), itxaronaldiaren arrazoia, aurrerapena, etenaldiak, bertan behera utzi.

### Rolak
Botoiak `RolePermissions`-en arabera ezkutatzen dira (behatzaileak ikusi bakarrik egiten du), baina **zerbitzariak
beti egiaztatzen du berriro** baimena eta makina erabiltzailearen ustiategikoa dela.
Administratzaileak hautatzaile bat du edozein ustiategi ikusteko.

## Nola probatu

```
dotnet run --project src/AgroCore.Server
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 1 --key AGRO-DEMO-1
dotnet run --project src/AgroCore.ControlCenter
```

Sartu `juan / juan123` gisa (Esperantza baserria bakarrik ikusten du) edo `admin / admin123` gisa (guztiak).
