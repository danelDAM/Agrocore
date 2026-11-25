using System;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Map
{
    /// <summary>
    /// Lanaren eta bateriaren kalkuluak. Shared-en daude zerbitzariak (makina aukeratzean)
    /// eta makinak berak (basera itzuli ala ez erabakitzean) formula berberak erabil ditzaten.
    /// </summary>
    public static class MachineMath
    {
        /// <summary>Soroaren gaineko pasada bakoitzaren zabalera.</summary>
        public static double LaneWidth(MachineProfile profile)
        {
            return Math.Max(1.5, 2 * profile.WorkRadius);
        }

        /// <summary>Soro oso bat estaltzeko behar den pasada kopurua.</summary>
        public static int LaneCount(MachineProfile profile, double fieldSize)
        {
            return (int)Math.Ceiling(fieldSize / LaneWidth(profile));
        }

        /// <summary>Lanean ari den bitarteko abiadura (mugitzean baino motelagoa).</summary>
        public static double WorkSpeed(MachineProfile profile)
        {
            return Math.Max(0.5, profile.Speed * profile.WorkSpeedFactor * profile.Efficiency);
        }

        /// <summary>Soro bateko falta den lana amaitzeko beharko dituen segundoak.</summary>
        public static double WorkSeconds(MachineProfile profile, double fieldSize, double progressPercent)
        {
            double totalDistance = LaneCount(profile, fieldSize) * fieldSize;
            double remaining = totalDistance * (1 - progressPercent / 100.0);
            return remaining / WorkSpeed(profile);
        }

        /// <summary>Bideetako gutxi gorabeherako distantzia ("Manhattan" distantzia: horizontala + bertikala soilik).</summary>
        public static double RoadDistance(Position a, Position b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
        }

        /// <summary>Distantzia makina motaren arabera: droneek lerro zuzenean egiten dute hegan.</summary>
        public static double TravelDistance(MachineType type, Position a, Position b)
        {
            if (type == MachineType.Sprayer)
            {
                return a.GroundDistanceTo(b) + 10; // + igo eta jaitsi
            }
            return RoadDistance(a, b) + 6;
        }

        /// <summary>Distantzia bat egiteko bateria-%.</summary>
        public static double MoveCost(MachineProfile profile, BatteryConfig config, double distance)
        {
            double seconds = distance / Math.Max(0.5, profile.Speed);
            return seconds * profile.MoveConsumption * config.MoveMultiplier;
        }

        /// <summary>Segundo batzuk lan egiteko bateria-%.</summary>
        public static double WorkCost(MachineProfile profile, BatteryConfig config, double seconds)
        {
            return seconds * profile.WorkConsumption * config.WorkMultiplier;
        }

        /// <summary>Basearen erreferentzia-puntua (atea), itzulera kalkulatzeko.</summary>
        public static Position BaseGate(FarmLayout layout)
        {
            return layout.CellCenter(FarmLayout.BaseLaneCol, layout.GateRow);
        }

        /// <summary>
        /// Soro batera joateko, falta den lana egiteko eta basera itzultzeko behar den bateria,
        /// segurtasun-tartea utzita.
        /// </summary>
        public static double BatteryNeededForTask(MachineType type, MachineProfile profile, BatteryConfig config,
                                                  FarmLayout layout, Position from, int fieldIndex, double progress)
        {
            Position entrance = layout.CellCenter(layout.FieldEntranceCol(fieldIndex), layout.FieldEntranceRow(fieldIndex));
            Position gate = BaseGate(layout);

            double go = MoveCost(profile, config, TravelDistance(type, from, entrance));
            double work = WorkCost(profile, config, WorkSeconds(profile, layout.FieldSize, progress));
            double back = MoveCost(profile, config, TravelDistance(type, entrance, gate) + 12);
            return go + work + back + config.ReturnReserve;
        }

        /// <summary>Zeregin bakoitza egiten duen makina mota.</summary>
        public static MachineType? MachineFor(TaskType task)
        {
            switch (task)
            {
                case TaskType.Sow: return MachineType.Seeder;
                case TaskType.Irrigate: return MachineType.Irrigator;
                case TaskType.Spray: return MachineType.Sprayer;
                case TaskType.Harvest: return MachineType.Harvester;
                default: return null; // Growth: zerbitzariak egiten du
            }
        }
    }
}
