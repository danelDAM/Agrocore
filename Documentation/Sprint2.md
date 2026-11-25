# Sprint 2 — Coordinación en el servidor y máquinas autónomas

## Qué se ha construido

### Máquina autónoma (`AgroCore.MachineClient`)

- **Cada máquina es un proceso independiente.** Se conecta por TCP, se autentica con
  `MACHINE_AUTH` (id + clave de flota de su explotación) y a partir de ahí trabaja sola.
- **Lanzador de flota**: `--fleet --farm 1 --key AGRO-DEMO-1` pide al servidor la lista de máquinas
  de esa explotación y arranca **un proceso por máquina** (con `--windows`, cada una en su ventana).
- **Hilos de la máquina**:
  - hilo principal: bucle "pedir tarea → ir al campo → trabajar → volver";
  - hilo lector (`ServerLink`): recibe las respuestas y las órdenes (STOP, RESUME, RETURN_TO_BASE...);
  - hilo de telemetría: cada 200 ms envía por **UDP** posición, batería, temperatura, estado y progreso.
- **Peticiones síncronas sencillas**: `link.Request<T>(mensaje, tipoRespuesta)` envía y espera la
  respuesta con `Monitor.Wait`; el hilo lector la deja en un "buzón" y hace `Monitor.PulseAll`.
- **Movimiento visible** (20 veces por segundo, sin teletransporte) por caminos del plano.
- **Trabajo en el campo** por pasadas (serpentina). Antes de cada pasada comprueba si le queda
  batería para terminarla y volver; si no, guarda el progreso y vuelve a cargar.
- **Recolectoras** con capacidad: cuando la tolva se llena paran a descargar.
- **Fumigadoras = drones**: vuelan en línea recta a su altura de crucero (no usan carreteras ni zonas).
- **Perfiles distintos** por máquina (`machine-profiles.json`): velocidad, consumo, capacidad, eficiencia.

### Coordinación en el servidor (una instancia por explotación: `FarmRuntime`)

Cada clase tiene su propio objeto `sync` y usa **`lock` + `Monitor.Wait` + `Monitor.PulseAll`**:

| Clase | Qué coordina | Quién espera | Quién despierta (PulseAll) |
|---|---|---|---|
| `TaskCoordinator` | Tareas y dependencias | Máquina sin tarea disponible | Tarea completada, cultivo que crece, nuevo plan, máquina liberada |
| `ZoneManager` | Celdas del plano (anticolisión) | Máquina cuya siguiente celda está ocupada | Máquina que sale de una celda |
| `ChargingStationManager` | Cargadores (cola FIFO) | Máquina sin cargador libre | Máquina que libera un cargador |

- **Dependencias de tareas**: `SOW → GROWTH → HARVEST`. Los cuidados (`IRRIGATE`, `SPRAY`) dependen
  de la siembra y de un % de crecimiento; el crecimiento se detiene en ese % hasta que se hacen.
- **Estados de tarea**: `PENDING`, `WAITING`, `ASSIGNED`, `RUNNING`, `COMPLETED`, `CANCELLED`.
- **Elección de la mejor máquina**: entre las que esperan, se puntúa (tiempo de viaje + trabajo,
  penalización por batería, bonus de capacidad en la cosecha) y se queda la tarea la mejor.
- **Batería**: el servidor no asigna una tarea que la máquina no pueda terminar y volver
  (`NEEDS_CHARGE`); 3 cargadores con cola FIFO (`WAITING_FOR_CHARGER`).
- **Anticolisión por zonas**: el plano es una cuadrícula; cada celda de carretera es una zona que
  solo puede ocupar una máquina. **Interbloqueos**: antes de esperar se recorre la cadena
  "A espera a B que espera a C..."; si vuelve a la máquina que pide, se deniega (`DENIED_DEADLOCK`)
  y la máquina se aparta al arcén, libera su celda y vuelve a intentarlo.
- **Órdenes**: STOP / RESUME / RETURN_TO_BASE / EMERGENCY_STOP / STOP_ALL / RESUME_ALL /
  RETURN_ALL_TO_BASE, comprobando siempre que la máquina pertenece a la explotación del usuario.
- **Desconexión de una máquina**: se liberan sus zonas, su cargador y su tarea (que conserva el progreso).
- **Telemetría UDP**: máquinas → servidor (puerto 5001) y servidor → centro de control cada 200 ms
  (solo la de la explotación que está viendo ese usuario).
- **Estado completo** (`FARM_STATE`) por TCP cada segundo a los centros de control.

## Pruebas automáticas (21)

Además de las del Sprint 1, `ConcurrencyTests` comprueba:

- una máquina espera a que se libere una zona (y no antes);
- se detecta y evita un interbloqueo entre dos máquinas;
- STOP cancela una espera;
- cola FIFO de cargadores: 3 cargando, 2 esperando, entran en orden de llegada;
- la recolectora se queda esperando hasta que el cultivo madura (riego incluido);
- de dos sembradoras esperando, la tarea se la queda la más rápida;
- sin batería suficiente el servidor responde `NEEDS_CHARGE`.

## Cómo probarlo

```
dotnet build AgroCore.sln
dotnet test AgroCore.sln

# Terminal 1: servidor
dotnet run --project src/AgroCore.Server

# Terminal 2: flota de Juan (11 máquinas, 11 procesos)
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 1 --key AGRO-DEMO-1 --windows

# Terminal 3 (opcional): flota de María
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 2 --key AGRO-DEMO-2
```

En la consola del servidor `status` muestra el estado y `debug` muestra las reservas de zonas.
Cada línea del log lleva `[F1]`, `[F2]`... para saber de qué explotación es.
