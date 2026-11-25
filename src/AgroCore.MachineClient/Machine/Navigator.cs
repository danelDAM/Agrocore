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
    /// <summary>Zerbitzariarekiko konexioa galdu da.</summary>
    public class LinkLostException : Exception
    {
        public LinkLostException() : base("Zerbitzariarekiko konexioa galduta")
        {
        }
    }

    /// <summary>
    /// Makina mapan zehar mugitzen du modu IKUSGARRIAN (urratsez urrats, inoiz ez du teleportatzen).
    ///
    /// Lurreko makinak:
    ///   - Bideetatik eta basearen errailetik bakarrik ibiltzen dira.
    ///   - Bideko gelaxka bakoitzean sartu aurretik, GUNEA eskatzen diote zerbitzariari (ZONE_REQUEST)
    ///     eta ez dute aurrera egiten ematen ez dioten arte. Gelaxka batetik irtetean askatu egiten dute (ZONE_RELEASE).
    ///   - Zerbitzariak gunea ukatzen badu elkarblokeo bat saihesteko, makina bazterbidera
    ///     aldentzen da, bere gelaxka askatzen du eta bidea uzten du.
    ///
    /// Droneak (fumigazio-droneak): lerro zuzenean hegan egiten dute. Drone bakoitzak bere hegaldi-altitudea du
    /// (aire-korridoreak); horrela, bi drone ez dira inoiz altuera berean egongo.
    /// </summary>
    public class Navigator
    {
        private const double TickSeconds = 0.05; // 20 simulazio-urrats segundoko
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

        /// <summary>Basearen plaza batean dago (aparkalekua edo kargagailua)?</summary>
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
        // HELMUGAK
        // ==================================================================

        /// <summary>
        /// Makina soro baten barruraino eramaten du. shouldAbort gelaxka bakoitzean egiaztatzen da:
        /// true itzultzen badu, makina dagoen tokian geratzen da (bidean) eta false itzultzen du.
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

            // Soroan sartu eta bideko gelaxka askatu
            Position inside = new Position(layout.FieldMinX(index) + 1.0, 0, layout.CellCenter(entranceCol, entranceRow).Z);
            MoveTo(inside, false);
            ReleaseZone(roadCol, roadRow);
            place = Place.Field;
            fieldIndex = index;
            return true;
        }

        /// <summary>Makina basearen plaza batera eramaten du (bere aparkalekura edo kargagailu batera).</summary>
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

            // Adarretik plazaraino (plaza bakoitzak Z desberdina du: ez da inorekin gurutzatzen)
            MoveTo(new Position(layout.BaseLaneX, 0, slot.Z), false);
            MoveTo(slot, false);
            ReleaseZone(FarmLayout.BaseLaneCol, laneRow);
            place = Place.Slot;
            fieldIndex = -1;
        }

        /// <summary>Plazatik edo sorotik irten eta bideko gelaxka batean jartzen da (erreserbatuta).</summary>
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

        /// <summary>Bidea gelaxkaz gelaxka egiten du, gune bakoitza sartu aurretik erreserbatuz.</summary>
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

                AcquireZone(nextCol, nextRow);                       // 1. hurrengo gelaxka erreserbatu
                MoveTo(layout.CellCenter(nextCol, nextRow), false);  // 2. bertaraino mugitu
                ReleaseZone(roadCol, roadRow);                       // 3. aurrekoa askatu
                roadCol = nextCol;
                roadRow = nextRow;
            }
            return true;
        }

        // ==================================================================
        // GUNEAK (talkaren aurkakoak)
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
                state.Set(MachineStatus.WaitingForZone, zoneId + " gunearen zain");
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
                    continue; // berriro eskatu
                }
                // Cancelled: normalean STOP edo larrialdiagatik. PauseIfHalted()-ek itxarongo du.
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
        /// Bidea utzi elkarblokeo bat hausteko: bazterbidera aldendu, dagoen gelaxka
        /// askatu beste makinak pasatu ahal izateko, eta une batez itxaron.
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
            state.Set(MachineStatus.Yielding, "bidea uzten (elkarblokeoa saihestuta)");

            // 2,2 unitate aldendu, martxaren norabidearekiko perpendikularrean
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
        // HEGALDIA (droneak)
        // ==================================================================

        private void FlyTo(Position target, double finalAltitude)
        {
            Position position = state.GetPosition();
            if (position.Y < cruiseAltitude - 0.05)
            {
                MoveTo(new Position(position.X, cruiseAltitude, position.Z), false); // aireratu
            }
            MoveTo(new Position(target.X, cruiseAltitude, target.Z), false);         // hegan egin
            MoveTo(new Position(target.X, finalAltitude, target.Z), false);          // jaitsi
        }

        // ==================================================================
        // OINARRIZKO MUGIMENDUA
        // ==================================================================

        /// <summary>
        /// Lerro zuzenean aurrera egiten du helmugaraino, pixkanaka (segundoko 20 aldiz),
        /// bateria gastatuz. Makinak STOP jasotzen badu, hemen geldituta geratzen da RESUME arte.
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

                // Simulazioaren abiadura: x10 bada, urrats honetan 10 aldiz denbora gehiago simulatzen da
                double simSeconds = SimulationClock.Scale(TickSeconds);
                double step = speed * simSeconds;
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

                // Orientazioa: 0 gradu = +Z aldera, 90 gradu = +X aldera
                double dx = target.X - position.X;
                double dz = target.Z - position.Z;
                double heading = GetHeading(dx, dz);

                state.SetPosition(next, heading, speed);
                if (working)
                {
                    battery.ConsumeWorking(simSeconds);
                }
                else
                {
                    battery.ConsumeMoving(simSeconds);
                }
                state.UpdateTemperature(working, true, simSeconds);

                Thread.Sleep((int)(TickSeconds * 1000));
            }
        }

        private double lastHeading = 90;

        private double GetHeading(double dx, double dz)
        {
            if (Math.Abs(dx) < 0.001 && Math.Abs(dz) < 0.001)
            {
                return lastHeading; // mugimendu bertikala (dronea): orientazioa mantentzen du
            }
            lastHeading = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            return lastHeading;
        }

        /// <summary>STOP edo larrialdi bat badago, makina hemen gelditzen da RESUME iritsi arte.</summary>
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
                state.Set(MachineStatus.Emergency, "LARRIALDIKO GELDIALDIA");
            }
            else
            {
                state.Set(MachineStatus.Stopped, "operadoreak geldituta");
            }
            state.SetSpeed(0);

            control.WaitWhileHalted(); // Monitor.Wait RESUME arte

            state.Set(previous, previousNote);
        }
    }
}
