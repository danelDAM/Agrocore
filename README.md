# AGROCORE

Plataforma distribuida de gestión y coordinación de maquinaria agrícola autónoma.
Proyecto de DAM (C#, WPF, TCP/UDP, hilos, concurrencia, seguridad, informes).

```
CENTRAL DE CONTROL WPF  <->  SERVIDOR AGROCORE  <->  MÁQUINAS AUTÓNOMAS
```

## Estructura

```
AgroCore.sln
src/
  AgroCore.Shared/      Modelos, enums, mensajes del protocolo, plano de la explotación
  AgroCore.Server/      Servidor TCP/UDP, seguridad, coordinación, logs
  AgroCore.MachineClient/  (Sprint 2) Máquina autónoma
  AgroCore.ControlCenter/  (Sprint 3) Centro de control WPF
tests/
  AgroCore.Tests/       Pruebas automáticas (xUnit)
Documentation/          Documentación de cada sprint
```

## Requisitos

- .NET 8 SDK (Windows para el centro de control WPF)

## Ejecutar

```
dotnet build AgroCore.sln
dotnet test AgroCore.sln
dotnet run --project src/AgroCore.Server
```

Usuarios de ejemplo: ver [Documentation/Sprint1.md](Documentation/Sprint1.md).
