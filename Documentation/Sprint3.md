# Sprint 3 — Centro de Control WPF y vista 3D

## Qué se ha construido (`AgroCore.ControlCenter`, .NET 8 WPF)

### Login contra el servidor
- Ventana de login con usuario, contraseña y servidor (`host` o `host:puerto`).
- La conexión se hace en un **hilo secundario** y el resultado vuelve a la interfaz con
  `Dispatcher.BeginInvoke` (la ventana no se congela).
- Mensajes claros: credenciales incorrectas, cuenta bloqueada, servidor apagado.
  Enlace "¿Problemas de acceso?".
- **Sesión local** en `%APPDATA%\AgroCore\session.json` (nunca la contraseña ni el token).
  Al cerrar sesión se elimina y se vuelve al login.

### Comunicación (mismo esquema que el ejemplo de clase)
- `ServerConnection`: `TcpClient` + `StreamReader`/`StreamWriter`; **un hilo lector** con
  `ReadLine()` en bucle. Cada mensaje se pasa al hilo de la interfaz y allí un `switch` según `type`.
- `TelemetryReceiver`: `UdpClient` en un puerto libre + hilo receptor. El puerto se comunica al
  servidor con `UDP_SUBSCRIBE`; la telemetría llega cada 200 ms.
- Sin lógica de reconexión: si se pierde la conexión se avisa y se vuelve al login.

### Panel principal personalizado
- KPIs de la explotación del usuario: máquinas, campos, batería media, trabajando, cargando, disponibles.
- **Vista 3D low-poly** hecha con `Viewport3D` (sin librerías ni modelos descargados):
  - Cosechadora (inspirada en el modelo de referencia), sembradora (tractor + sembradora),
    regadora con depósito y barra de riego, y **fumigadoras como drones de 4 rotores**.
  - Animaciones: rotores girando, molinete de la cosechadora, nube de fumigación, agua, polvo.
  - Los cultivos cambian según el crecimiento (zanahoria, maíz con mazorcas, tomateras con
    tomates verdes→rojos); durante la siembra/cosecha solo se ve la parte ya trabajada.
  - Movimiento interpolado entre paquetes de telemetría (sin teletransporte).
  - Cargadores con luz verde/naranja, granero, silo, árboles, caminos.
  - Etiquetas 2D con nombre y batería; cámara orbital; clic para seleccionar una máquina;
    "seguir selección"; ver zonas reservadas (anticolisión).
- Ficha de la máquina seleccionada con START / STOP / RESUME / A LA BASE.
- Alertas (batería baja, esperas, interbloqueos evitados...).
- Órdenes a toda la flota: DETENER TODAS, REANUDAR TODAS, TODAS A LA BASE, PARADA DE EMERGENCIA
  (con confirmación).

### Otras páginas
- **Máquinas**: tabla en tiempo real + órdenes por máquina + ficha técnica (capacidades distintas).
- **Campos (planificador)**: elegir cultivo → el servidor genera las tareas; resiembra automática;
  cancelar plan.
- **Tareas**: estados PENDING/WAITING/ASSIGNED/RUNNING/COMPLETED/CANCELLED, motivo de espera,
  progreso, interrupciones, cancelar.

### Roles
Los botones se ocultan según `RolePermissions` (el observador solo ve), pero **el servidor vuelve a
comprobar siempre** el permiso y que la máquina sea de la explotación del usuario.
El administrador tiene un selector para ver cualquier explotación.

## Cómo probarlo

```
dotnet run --project src/AgroCore.Server
dotnet run --project src/AgroCore.MachineClient -- --fleet --farm 1 --key AGRO-DEMO-1
dotnet run --project src/AgroCore.ControlCenter
```

Entrar como `juan / juan123` (solo ve Finca La Esperanza) o `admin / admin123` (todas).
