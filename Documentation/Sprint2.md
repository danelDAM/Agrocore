# Sprint 2 — Zerbitzariko koordinazioa eta makina autonomoak

## Zer eraiki da

### Makina autonomoa (`AgroCore.MachineClient`)

- **Makina bakoitza prozesu independente bat da.** TCP bidez konektatzen da, `MACHINE_AUTH` bidez
  autentifikatzen da (id-a + bere ustiategiaren flota-gakoa) eta handik aurrera bakarrik lan egiten du.
- **Flota-abiarazlea**: `--fleet --farm 1 --key AGRO-DEMO-1` aukerak ustiategi horretako makinen zerrenda
  eskatzen dio zerbitzariari eta **prozesu bat makina bakoitzeko** abiarazten du (`--windows` aukerarekin, bakoitza bere leihoan).
- **Makinaren hariak**:
  - hari nagusia: "zeregina eskatu → sorora joan → lan egin → itzuli" begizta;
  - irakurle-haria (`ServerLink`): erantzunak eta aginduak jasotzen ditu (STOP, RESUME, RETURN_TO_BASE...);
  - telemetria-haria: 200 ms-ro posizioa, bateria, tenperatura, egoera eta aurrerapena bidaltzen ditu **UDP** bidez.
- **Eskaera sinkrono sinpleak**: `link.Request<T>(mezua, erantzunMota)` metodoak bidali eta erantzunaren
  zain geratzen da `Monitor.Wait` bidez; irakurle-hariak "postontzi" batean uzten du eta `Monitor.PulseAll` egiten du.
- **Mugimendu ikusgarria** (segundoko 20 aldiz, teletransportik gabe) planoaren bideetatik.
- **Soroko lana** pasaldika (sigi-saga). Pasaldi bakoitzaren aurretik, hura amaitu eta itzultzeko
  adina bateria duen egiaztatzen du; ez badu, aurrerapena gorde eta kargatzera itzultzen da.
- **Uzta-biltzaileak** edukierarekin: depositua betetzen denean, gelditu egiten dira hustutzeko.
- **Fumigazio-droneak**: zuzen hegan egiten dute beren hegaldi-altueran (ez dute errepiderik ez gunerik erabiltzen).
- **Profil desberdinak** makina bakoitzeko (`machine-profiles.json`): abiadura, kontsumoa, edukiera, eraginkortasuna.

### Zerbitzariko koordinazioa (instantzia bat ustiategi bakoitzeko: `FarmRuntime`)

Klase bakoitzak bere `sync` objektua du eta **`lock` + `Monitor.Wait` + `Monitor.PulseAll`** erabiltzen ditu:

| Klasea | Zer koordinatzen du | Nor dago zain | Nork esnatzen du (PulseAll) |
|---|---|---|---|
| `TaskCoordinator` | Zereginak eta mendekotasunak | Zeregin erabilgarririk ez duen makina | Osatutako zeregina, hazten ari den laborea, labore-plan berria, askatutako makina |
| `ZoneManager` | Planoaren gelaxkak (talkak saihestea) | Hurrengo gelaxka okupatuta duen makina | Gelaxka batetik ateratzen den makina |
| `ChargingStationManager` | Kargagailuak (FIFO ilara) | Kargagailu librerik gabeko makina | Kargagailu bat askatzen duen makina |

- **Zereginen mendekotasunak**: `SOW → GROWTH → HARVEST`. Zainketak (`IRRIGATE`, `SPRAY`) ereitearen eta
  hazkuntza-ehuneko baten mende daude; hazkuntza ehuneko horretan gelditzen da zainketak egin arte.
- **Zereginen egoerak**: `PENDING`, `WAITING`, `ASSIGNED`, `RUNNING`, `COMPLETED`, `CANCELLED`
  (Egiteko, Zain, Esleituta, Martxan, Osatuta, Bertan behera utzita).
- **Makinarik egokiena aukeratzea**: zain daudenen artean puntuazio bat kalkulatzen da (bidaia- eta lan-denbora,
  bateriagatiko zigorra, uzta-bilketan edukieragatiko saria) eta onenak hartzen du zeregina.
- **Bateria**: zerbitzariak ez dio makinari esleitzen amaitu eta itzultzeko gai ez den zereginik
  (`NEEDS_CHARGE`); 3 kargagailu FIFO ilararekin (`WAITING_FOR_CHARGER`).
- **Talkak saihestea guneen bidez**: planoa sareta bat da; errepideko gelaxka bakoitza makina bakar batek
  okupa dezakeen gune bat da. **Elkarblokeoak**: zain geratu aurretik "A B-ren zain, B C-ren zain..." katea
  zeharkatzen da; eskatzen duen makinara itzultzen bada, ukatu egiten da (`DENIED_DEADLOCK`)
  eta makina bide-bazterrera aldentzen da, bere gelaxka askatzen du eta berriro saiatzen da.
- **Aginduak**: STOP / RESUME / RETURN_TO_BASE / EMERGENCY_STOP / STOP_ALL / RESUME_ALL /
  RETURN_ALL_TO_BASE, makina erabiltzailearen ustiategikoa dela beti egiaztatuta.
- **Makina bat deskonektatzean**: bere guneak, kargagailua eta zeregina askatzen dira (zereginak aurrerapena gordetzen du).
- **UDP telemetria**: makinak → zerbitzaria (5001 ataka) eta zerbitzaria → Kontrol Zentroa 200 ms-ro
  (erabiltzaile horrek ikusten ari den ustiategikoa bakarrik).
- **Egoera osoa** (`FARM_STATE`) TCP bidez segundoro Kontrol Zentroetara.

## Proba automatikoak (21)

Sprint 1ekoez gain, `ConcurrencyTests` klaseak hau egiaztatzen du:

- makina bat gune bat askatu arte itxaroten du (eta ez lehenago);
- bi makinaren arteko elkarblokeo bat detektatu eta saihesten da;
- STOP aginduak itxaronaldi bat bertan behera uzten du;
- kargagailuen FIFO ilara: 3 kargatzen, 2 zain, iritsi diren ordenan sartzen dira;
- uzta-biltzailea zain geratzen da laborea heldu arte (ureztatzea barne);
- zain dauden bi ereileetatik, azkarrenak hartzen du zeregina;
- bateria nahikorik gabe, zerbitzariak `NEEDS_CHARGE` erantzuten du.

## Nola probatu

```
dotnet build AgroCore.sln
dotnet test AgroCore.sln

# 1. terminala: zerbitzaria
dotnet run --project src/AgroCore.Server

# 2. terminala: Juanen flota (11 makina, 11 prozesu)
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 1 --key AGRO-DEMO-1 --windows

# 3. terminala (aukerakoa): Mariaren flota
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 2 --key AGRO-DEMO-2
```

Zerbitzariaren kontsolan `status` aginduak egoera erakusten du eta `debug` aginduak guneen erreserbak.
Erregistroko lerro bakoitzak `[F1]`, `[F2]`... darama, zein ustiategitakoa den jakiteko.
