# Sprint 1 — Base del proyecto, protocolo y autenticación

## Qué se ha construido

- **Solución** con los proyectos `AgroCore.Shared`, `AgroCore.Server` y `AgroCore.Tests`.
- **AgroCore.Shared**: modelos (`User`, `Farm`, `Machine`, `Field`, `FarmTask`, `Charger`, `Position`,
  `LogEntry`, `Telemetry`...), enums, DTOs, tabla de permisos por rol y **todos los mensajes del protocolo**.
- **Protocolo**: JSON de una línea por mensaje sobre TCP. Cada mensaje lleva un campo `type`
  (`LOGIN_REQUEST`, `TASK_ASSIGNMENT`...). Quien recibe lee el `type` y hace un `switch`.
- **Clase `Connection`**: envía/recibe líneas por TCP (`StreamReader.ReadLine` / `StreamWriter.WriteLine`).
  El envío va dentro de `lock` porque varios hilos pueden escribir en el mismo socket.
- **Servidor TCP multicliente** (`TcpServer`): `TcpListener` + `AcceptTcpClient()` + un `Thread` por cliente.
- **Login contra el servidor**: busca usuario → comprueba contraseña → usuario activo → rol → explotación.
  Bloqueo temporal tras 5 intentos fallidos. Mensaje genérico "Usuario o contraseña incorrectos".
- **Contraseñas** con PBKDF2-SHA256 (100.000 iteraciones, sal aleatoria). Nunca en texto plano.
- **Sesiones** en el servidor (`SessionManager` con `ConcurrentDictionary`).
- **Gestión de usuarios** (solo administrador): crear agricultor (crea `User` + `Farm` con campos,
  máquinas y cargadores), crear operador/observador, editar, desactivar, eliminar, regenerar clave de flota.
- **Cambio de contraseña** validando la contraseña actual.
- **Logs** con patrón productor/consumidor (`BlockingCollection` + hilo escritor):
  archivo de texto legible y `events.jsonl` para estadísticas.
- **Persistencia** en `data/agrocore-data.json` (protegida con `lock`).

## Datos de ejemplo

| Usuario | Contraseña | Rol | Explotación |
|---|---|---|---|
| admin | admin123 | Administrador | (todas) |
| juan | juan123 | Agricultor | Finca La Esperanza (6 campos, 11 máquinas, 3 cargadores) |
| maria | maria123 | Agricultor | Finca Los Olivos (4 campos, 7 máquinas, 2 cargadores) |
| pedro | pedro123 | Agricultor | El Robledal (3 campos, 5 máquinas, 2 cargadores) |
| operador | operador123 | Operador | Finca La Esperanza |
| visor | visor123 | Observador | Finca La Esperanza |

## Cómo comprobarlo

```
dotnet build AgroCore.sln
dotnet test AgroCore.sln
dotnet run --project src/AgroCore.Server
```

Las pruebas incluyen un login **real por TCP** contra el servidor (contraseña mala, buena,
acceso denegado a un agricultor que pide la lista de usuarios y cierre de sesión).
