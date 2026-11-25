using System;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Map
{
    /// <summary>
    /// Cálculos de trabajo y batería. Están en Shared para que el servidor (al elegir máquina)
    /// y la propia máquina (al decidir si vuelve a la base) usen exactamente las mismas fórmulas.
    /// </summary>
    public static class MachineMath
    {
        /// <summary>Ancho de cada pasada sobre el campo.</summary>
        public static double LaneWidth(MachineProfile profile)
        {
            return Math.Max(1.5, 2 * profile.WorkRadius);
        }

        /// <summary>Número de pasadas para cubrir un campo entero.</summary>
        public static int LaneCount(MachineProfile profile, double fieldSize)
        {
            return (int)Math.Ceiling(fieldSize / LaneWidth(profile));
        }

        /// <summary>Velocidad mientras trabaja (más lenta que al desplazarse).</summary>
        public static double WorkSpeed(MachineProfile profile)
        {
            return Math.Max(0.5, profile.Speed * profile.WorkSpeedFactor * profile.Efficiency);
        }

        /// <summary>Segundos que tardará en terminar el trabajo que falta de un campo.</summary>
        public static double WorkSeconds(MachineProfile profile, double fieldSize, double progressPercent)
        {
            double totalDistance = LaneCount(profile, fieldSize) * fieldSize;
            double remaining = totalDistance * (1 - progressPercent / 100.0);
            return remaining / WorkSpeed(profile);
        }

        /// <summary>Distancia aproximada por caminos (distancia "Manhattan": solo horizontal + vertical).</summary>
        public static double RoadDistance(Position a, Position b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
        }

        /// <summary>Distancia según el tipo de máquina: los drones vuelan en línea recta.</summary>
        public static double TravelDistance(MachineType type, Position a, Position b)
        {
            if (type == MachineType.Sprayer)
            {
                return a.GroundDistanceTo(b) + 10; // + subir y bajar
            }
            return RoadDistance(a, b) + 6;
        }

        /// <summary>% de batería para recorrer una distancia.</summary>
        public static double MoveCost(MachineProfile profile, BatteryConfig config, double distance)
        {
            double seconds = distance / Math.Max(0.5, profile.Speed);
            return seconds * profile.MoveConsumption * config.MoveMultiplier;
        }

        /// <summary>% de batería para trabajar unos segundos.</summary>
        public static double WorkCost(MachineProfile profile, BatteryConfig config, double seconds)
        {
            return seconds * profile.WorkConsumption * config.WorkMultiplier;
        }

        /// <summary>Punto de referencia de la base (la puerta), para calcular el regreso.</summary>
        public static Position BaseGate(FarmLayout layout)
        {
            return layout.CellCenter(FarmLayout.BaseLaneCol, layout.GateRow);
        }

        /// <summary>
        /// Batería necesaria para ir a un campo, hacer el trabajo que falta y volver a la base
        /// dejando el margen de seguridad.
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

        /// <summary>Tipo de máquina que hace cada tarea.</summary>
        public static MachineType? MachineFor(TaskType task)
        {
            switch (task)
            {
                case TaskType.Sow: return MachineType.Seeder;
                case TaskType.Irrigate: return MachineType.Irrigator;
                case TaskType.Spray: return MachineType.Sprayer;
                case TaskType.Harvest: return MachineType.Harvester;
                default: return null; // Growth: la hace el servidor
            }
        }
    }
}
