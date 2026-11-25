using System;
using System.Collections.Generic;
using System.Threading;
using AgroCore.MachineClient.Battery;
using AgroCore.MachineClient.Networking;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;

namespace AgroCore.MachineClient.Machine
{
    /// <summary>La conexión con el servidor se ha perdido.</summary>
    public class LinkLostException : Exception
    {
        public LinkLostException() : base("Conexión con el servidor perdida")
        {
        }
    }

    /// <summary>
    /// Mueve la máquina por el mapa de forma VISIBLE (paso a paso, nunca teletransporta).
    ///
    /// Máquinas de tierra:
    ///   - Solo circulan por los caminos y el carril de la base.
    ///   - Antes de entrar en cada celda del camino piden la ZONA al servidor (ZONE_REQUEST)
    ///     y no avanzan hasta que se la conceden. Al salir de una celda la liberan (ZONE_RELEASE).
    ///   - Si el servidor deniega la zona para evitar un interbloqueo, la máquina se aparta
    ///     al arcén, libera su celda y cede el paso.
    ///
    /// Drones (fumigadoras): vuelan en línea recta. Cada dron tiene su propia altitud de crucero
    /// (corredores aéreos), así nunca coinciden dos drones a la misma altura.
    /// </summary>
    public class Navigator
    {
        private const double TickSeconds = 0.05; // 20 pasos de simulación por segundo
        private const double WorkAltitude = 2.5;

        private enum Place { Slot, Road, Field }

        private readonly string machineId;
        private readonly MachineType type;
        private readonly MachineProfile profile;
        private readonly MachineState state;
        private readonly MachineControl control;
        private readonly BatteryManager battery;
        private readonly ServerLink link;
        private readonly FarmLayout layout;
        private readonly double cruiseAltitude;

        private Place place = Place.Slot;
        private int roadCol;
        private int roadRow;
        private int fieldIndex = -1;
        private readonly List<string> heldZones = new List<string>();

        public Navigator(string machineId, MachineType type, MachineProfile profile, MachineState state,
                         MachineControl control, BatteryManager battery, ServerLink link, FarmLayout layout, int droneIndex)
        {
            this.machineId = machineId;
            this.type = type;
            this.profile = profile;
            this.state = state;
            this.control = control;
            this.battery = battery;
            this.link = link;
            this.layout = layout;
            cruiseAltitude = 7 + droneIndex * 1.5;
        }

        public bool IsDrone
        {
            get { return type == MachineType.Sprayer; }
        }

        public double FieldWorkAltitude
        {
            get { return IsDrone ? WorkAltitude : 0; }
        }

        /// <summary>¿Está en una plaza de la base (aparcamiento o cargador)?</summary>
        public bool IsAtBase
        {
            get { return place == Place.Slot; }
        }

        public void PlaceAtSlot(Position slot)
        {
            state.SetPosition(slot, 90, 0);
            place = Place.Slot;
        }

        // ==================================================================
        // DESTINOS
        // ==================================================================

        /// <summary>
        /// Lleva la máquina hasta dentro de un campo. shouldAbort se comprueba en cada celda:
        /// si devuelve true, la máquina se queda donde está (en el camino) y devuelve false.
        /// </summary>
        public bool GoToField(int index, Func<bool> shouldAbort)
        {
            if (place == Place.Field && fieldIndex == index)
            {
                return true;
            }

            if (IsDrone)
            {
                Position start = new Position(layout.FieldMinX(index) + 1, WorkAltitude, layout.FieldMinZ(index) + 1.5);
                FlyTo(start, WorkAltitude);
                place = Place.Field;
                fieldIndex = index;
                return true;
            }

            LeaveCurrentPlace();
            int entranceCol = layout.FieldEntranceCol(index);
            int entranceRow = layout.FieldEntranceRow(index);
            if (!FollowPathTo(entranceCol, entranceRow, shouldAbort))
            {
                return false;
            }

            // Entrar en el campo y liberar la celda del camino
            Position inside = new Position(layout.FieldMinX(index) + 1.0, 0, layout.CellCenter(entranceCol, entranceRow).Z);
            MoveTo(inside, false);
            ReleaseZone(roadCol, roadRow);
            place = Place.Field;
            fieldIndex = index;
            return true;
        }

        /// <summary>Lleva la máquina a una plaza de la base (su aparcamiento o un cargador).</summary>
        public void GoToSlot(Position slot)
        {
            Position current = state.GetPosition();
            if (place == Place.Slot && current.GroundDistanceTo(slot) < 0.05 && current.Y < 0.05)
            {
                return;
            }

            if (IsDrone)
            {
                FlyTo(slot, 0);
                place = Place.Slot;
                fieldIndex = -1;
                return;
            }

            LeaveCurrentPlace();
            int laneRow = layout.RowOf(slot.Z);
            FollowPathTo(FarmLayout.BaseLaneCol, laneRow, null);

            // Por el ramal hasta la plaza (cada plaza tiene una Z distinta: no se cruza con nadie)
            MoveTo(new Position(layout.BaseLaneX, 0, slot.Z), false);
            MoveTo(slot, false);
            ReleaseZone(FarmLayout.BaseLaneCol, laneRow);
            place = Place.Slot;
            fieldIndex = -1;
        }

        /// <summary>Sale de la plaza o del campo y se pone en una celda de camino (reservada).</summary>
        private void LeaveCurrentPlace()
        {
            Position position = state.GetPosition();

            if (place == Place.Slot)
            {
                int laneRow = layout.RowOf(position.Z);
                AcquireZone(FarmLayout.BaseLaneCol, laneRow);
                MoveTo(new Position(layout.BaseLaneX, 0, position.Z), false);
                MoveTo(layout.CellCenter(FarmLayout.BaseLaneCol, laneRow), false);
                place = Place.Road;
                roadCol = FarmLayout.BaseLaneCol;
                roadRow = laneRow;
            }
            else if (place == Place.Field)
            {
                int col = layout.FieldEntranceCol(fieldIndex);
                int row = layout.FieldEntranceRow(fieldIndex);
                Position exitPoint = new Position(layout.FieldMinX(fieldIndex) + 1.0, 0, layout.CellCenter(col, row).Z);
                MoveTo(exitPoint, false);
                AcquireZone(col, row);
                MoveTo(layout.CellCenter(col, row), false);
                place = Place.Road;
                roadCol = col;
                roadRow = row;
                fieldIndex = -1;
            }
        }

        /// <summary>Recorre el camino celda a celda reservando cada zona antes de entrar.</summary>
        private bool FollowPathTo(int targetCol, int targetRow, Func<bool> shouldAbort)
        {
            List<int[]> path = layout.FindPath(roadCol, roadRow, targetCol, targetRow);
            for (int i = 1; i < path.Count; i++)
            {
                if (shouldAbort != null && shouldAbort())
                {
                    return false;
                }

                int nextCol = path[i][0];
                int nextRow = path[i][1];

                AcquireZone(nextCol, nextRow);                       // 1. reservar la siguiente celda
                MoveTo(layout.CellCenter(nextCol, nextRow), false);  // 2. moverse hasta ella
                ReleaseZone(roadCol, roadRow);                       // 3. liberar la anterior
                roadCol = nextCol;
                roadRow = nextRow;
            }
            return true;
        }

        // ==================================================================
        // ZONAS (anticolisión)
        // ==================================================================

        private void AcquireZone(int col, int row)
        {
            string zoneId = FarmLayout.ZoneId(col, row);
            if (heldZones.Contains(zoneId))
            {
                return;
            }

            while (true)
            {
                PauseIfHalted();

                MachineStatus previous = state.Status;
                string previousNote = state.Note;
                state.Set(MachineStatus.WaitingForZone, "esperando zona " + zoneId);
                state.SetSpeed(0);

                ZoneRequest request = new ZoneRequest();
                request.MachineId = machineId;
                request.ZoneId = zoneId;
                ZoneResponse response = link.Request<ZoneResponse>(request, MessageTypes.ZoneResponse);

                state.Set(previous, previousNote);
                if (response == null)
                {
                    throw new LinkLostException();
                }

                if (response.Outcome == ZoneOutcome.Granted)
                {
                    heldZones.Add(zoneId);
                    return;
                }
                if (response.Outcome == ZoneOutcome.DeniedDeadlock)
                {
                    YieldWay();
                    continue; // volver a pedirla
                }
                // Cancelled: normalmente por STOP o emergencia. PauseIfHalted() esperará.
                Thread.Sleep(200);
            }
        }

        private void ReleaseZone(int col, int row)
        {
            string zoneId = FarmLayout.ZoneId(col, row);
            if (!heldZones.Contains(zoneId))
            {
                return;
            }
            heldZones.Remove(zoneId);
            ZoneRelease release = new ZoneRelease();
            release.MachineId = machineId;
            release.ZoneId = zoneId;
            link.Send(release);
        }

        /// <summary>
        /// Ceder el paso para romper un interbloqueo: apartarse al arcén, liberar la celda
        /// en la que está para que la otra máquina pueda pasar, y esperar un momento.
        /// </summary>
        private void YieldWay()
        {
            if (place != Place.Road)
            {
                Thread.Sleep(500);
                return;
            }

            MachineStatus previous = state.Status;
            string previousNote = state.Note;
            state.Set(MachineStatus.Yielding, "cede el paso (interbloqueo evitado)");

            // Apartarse 2,2 unidades en perpendicular a la dirección de marcha
            Position position = state.GetPosition();
            Position center = layout.CellCenter(roadCol, roadRow);
            bool verticalRoad = (roadCol - 3) % 4 == 0 || roadCol == FarmLayout.BaseLaneCol;
            Position shoulder;
            if (verticalRoad)
            {
                shoulder = new Position(center.X + 2.2, 0, position.Z);
            }
            else
            {
                shoulder = new Position(position.X, 0, center.Z + 2.2);
            }
            MoveTo(shoulder, false);
            ReleaseZone(roadCol, roadRow);

            Thread.Sleep(1500);
            state.Set(previous, previousNote);
        }

        // ==================================================================
        // VUELO (drones)
        // ==================================================================

        private void FlyTo(Position target, double finalAltitude)
        {
            Position position = state.GetPosition();
            if (position.Y < cruiseAltitude - 0.05)
            {
                MoveTo(new Position(position.X, cruiseAltitude, position.Z), false); // despegar
            }
            MoveTo(new Position(target.X, cruiseAltitude, target.Z), false);         // volar
            MoveTo(new Position(target.X, finalAltitude, target.Z), false);          // descender
        }

        // ==================================================================
        // MOVIMIENTO BÁSICO
        // ==================================================================

        /// <summary>
        /// Avanza en línea recta hasta el destino, poco a poco (20 veces por segundo),
        /// gastando batería. Si la máquina recibe STOP, se queda parada aquí hasta RESUME.
        /// </summary>
        public void MoveTo(Position target, bool working)
        {
            double speed = working ? MachineMath.WorkSpeed(profile) : profile.Speed;

            while (true)
            {
                if (!link.IsConnected)
                {
                    throw new LinkLostException();
                }
                PauseIfHalted();

                Position position = state.GetPosition();
                double distance = position.DistanceTo(target);
                if (distance < 0.01)
                {
                    state.SetSpeed(0);
                    return;
                }

                double step = speed * TickSeconds;
                Position next;
                if (step >= distance)
                {
                    next = target.Clone();
                }
                else
                {
                    double t = step / distance;
                    next = new Position(
                        position.X + (target.X - position.X) * t,
                        position.Y + (target.Y - position.Y) * t,
                        position.Z + (target.Z - position.Z) * t);
                }

                // Orientación: 0 grados = hacia +Z, 90 grados = hacia +X
                double dx = target.X - position.X;
                double dz = target.Z - position.Z;
                double heading = GetHeading(dx, dz);

                state.SetPosition(next, heading, speed);
                if (working)
                {
                    battery.ConsumeWorking(TickSeconds);
                }
                else
                {
                    battery.ConsumeMoving(TickSeconds);
                }
                state.UpdateTemperature(working, true, TickSeconds);

                Thread.Sleep((int)(TickSeconds * 1000));
            }
        }

        private double lastHeading = 90;

        private double GetHeading(double dx, double dz)
        {
            if (Math.Abs(dx) < 0.001 && Math.Abs(dz) < 0.001)
            {
                return lastHeading; // movimiento vertical (dron): mantiene la orientación
            }
            lastHeading = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            return lastHeading;
        }

        /// <summary>Si hay un STOP o una emergencia, la máquina se detiene aquí hasta que llegue RESUME.</summary>
        public void PauseIfHalted()
        {
            if (!control.IsHalted)
            {
                return;
            }
            MachineStatus previous = state.Status;
            string previousNote = state.Note;
            if (control.IsEmergency)
            {
                state.Set(MachineStatus.Emergency, "PARADA DE EMERGENCIA");
            }
            else
            {
                state.Set(MachineStatus.Stopped, "detenida por el operador");
            }
            state.SetSpeed(0);

            control.WaitWhileHalted(); // Monitor.Wait hasta RESUME

            state.Set(previous, previousNote);
        }
    }
}
