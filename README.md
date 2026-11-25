# AGROCORE

Nekazaritzako makineria autonomoa kudeatu eta koordinatzeko plataforma banatua.
DAMeko proiektua (C#, WPF, TCP/UDP, hariak, konkurrentzia, segurtasuna, txostenak).

```
KONTROL ZENTROA (WPF)  <->  AGROCORE ZERBITZARIA  <->  MAKINA AUTONOMOAK
```

## Egitura

```
AgroCore.sln
src/
  AgroCore.Shared/      Ereduak, enum-ak, protokoloaren mezuak, ustiategiaren planoa
  AgroCore.Server/      TCP/UDP zerbitzaria, segurtasuna, koordinazioa, gertaeren erregistroa
  AgroCore.MachineClient/  Makina autonomoa (prozesu bat makina bakoitzeko)
  AgroCore.ControlCenter/  WPF Kontrol Zentroa, 3D ikuspegiarekin
tests/
  AgroCore.Tests/       Proba automatikoak (xUnit)
Documentation/          Sprint bakoitzaren dokumentazioa
```

## Betekizunak

- .NET 8 SDK (Windows, WPF Kontrol Zentroarentzat)

## Exekutatu

```
dotnet build AgroCore.sln
dotnet test AgroCore.sln
dotnet run --project src/AgroCore.Server
```

Adibide-flota (beste terminal batean):

```
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 1 --key AGRO-DEMO-1 --windows
```

Kontrol Zentroa (beste terminal batean):

```
dotnet run --project src/AgroCore.ControlCenter
```

Adibide-erabiltzaileak: ikus [Documentation/Sprint1.md](Documentation/Sprint1.md).
- [Sprint 2](Documentation/Sprint2.md): koordinazioa eta makina autonomoak
- [Sprint 3](Documentation/Sprint3.md): WPF Kontrol Zentroa eta 3D ikuspegia
- [Sprint 4](Documentation/Sprint4.md): estatistikak, PDF txostenak, gertaeren erregistroa, profila eta administrazioa
