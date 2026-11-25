using System;
using System.Collections.Generic;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Map
{
    /// <summary>
    /// Plano de una explotación. El terreno se divide en celdas cuadradas (una cuadrícula).
    ///
    ///   columna:  0  1  2  3  4  5  6  7  8  9 10 11 ...
    ///             [ BASE ][L][C][ CAMPO ][C][ CAMPO ][C]
    ///
    ///   - Columnas 0 y 1: la base (cargadores y aparcamientos).
    ///   - Columna 2: el "carril de la base" (L), por donde se mueven las máquinas dentro de la base.
    ///   - Columnas 3, 7, 11, 15: caminos verticales (C).
    ///   - Filas 0, 4, 8: caminos horizontales.
    ///   - Entre los caminos hay campos de 3x3 celdas.
    ///   - La base solo se conecta con los caminos por la PUERTA (fila GateRow).
    ///
    /// Cada celda de camino o de carril es una ZONA. Para entrar en una zona, la máquina debe
    /// reservarla en el servidor (sistema anticolisión). Las celdas de los campos no son zonas:
    /// en un campo solo trabaja una máquina a la vez (lo garantiza el coordinador de tareas).
    ///
    /// Esta clase está en Shared porque el servidor, las máquinas y el centro de control
    /// tienen que usar exactamente el mismo plano.
    /// </summary>
    public class FarmLayout
    {
        /// <summary>Tamaño de cada celda en unidades del mundo 3D.</summary>
        public const double CellSize = 6.0;

        /// <summary>Columna del carril de la base.</summary>
        public const int BaseLaneCol = 2;

        public int FieldCount { get; private set; }
        public int ChargerCount { get; private set; }
        public int MachineSlotCount { get; private set; }
        public int FieldCols { get; private set; }
        public int FieldRows { get; private set; }
        public int Cols { get; private set; }
        public int Rows { get; private set; }

        /// <summary>Fila por la que se sale de la base al camino.</summary>
        public int GateRow { get; private set; }

        public FarmLayout(int fieldCount, int chargerCount, int machineSlotCount)
        {
            FieldCount = Math.Max(1, fieldCount);
            ChargerCount = Math.Max(1, chargerCount);
            MachineSlotCount = Math.Max(1, machineSlotCount);

            FieldCols = Math.Min(3, FieldCount);                 // como mucho 3 campos por fila
            FieldRows = (FieldCount + FieldCols - 1) / FieldCols; // filas de campos necesarias
            Cols = 4 + 4 * FieldCols;
            Rows = 1 + 4 * FieldRows;
            GateRow = 4 * (FieldRows / 2);                        // un camino horizontal en el centro
        }

        public double Width
        {
            get { return Cols * CellSize; }
        }

        public double Depth
        {
            get { return Rows * CellSize; }
        }

        // ------------------------------------------------------------------
        // Celdas y zonas
        // ------------------------------------------------------------------

        public bool IsInside(int col, int row)
        {
            return col >= 0 && col < Cols && row >= 0 && row < Rows;
        }

        /// <summary>¿Es una celda de camino?</summary>
        public bool IsRoad(int col, int row)
        {
            if (!IsInside(col, row) || col < 3)
            {
                return false;
            }
            bool verticalRoad = (col - 3) % 4 == 0;
            bool horizontalRoad = row % 4 == 0;
            return verticalRoad || horizontalRoad;
        }

        /// <summary>¿Es una celda del carril interior de la base?</summary>
        public bool IsBaseLane(int col, int row)
        {
            return col == BaseLaneCol && row >= 0 && row < Rows;
        }

        /// <summary>¿Puede circular una máquina de tierra por esta celda?</summary>
        public bool IsDrivable(int col, int row)
        {
            return IsRoad(col, row) || IsBaseLane(col, row);
        }

        /// <summary>¿Se puede pasar directamente de una celda a otra vecina?</summary>
        public bool CanConnect(int col1, int row1, int col2, int row2)
        {
            if (!IsDrivable(col1, row1) || !IsDrivable(col2, row2))
            {
                return false;
            }
            // Entre el carril de la base y el camino solo se pasa por la puerta.
            bool oneIsLane = col1 == BaseLaneCol || col2 == BaseLaneCol;
            bool oneIsRoad = col1 == 3 || col2 == 3;
            if (oneIsLane && oneIsRoad)
            {
                return row1 == GateRow && row2 == GateRow;
            }
            return true;
        }

        public static string ZoneId(int col, int row)
        {
            return "Z" + col.ToString("00") + "-" + row.ToString("00");
        }

        /// <summary>Convierte "Z07-04" en columna 7, fila 4.</summary>
        public static bool TryParseZone(string zoneId, out int col, out int row)
        {
            col = 0;
            row = 0;
            if (zoneId == null || zoneId.Length != 6 || zoneId[0] != 'Z')
            {
                return false;
            }
            return int.TryParse(zoneId.Substring(1, 2), out col) && int.TryParse(zoneId.Substring(4, 2), out row);
        }

        public Position CellCenter(int col, int row)
        {
            return new Position((col + 0.5) * CellSize, 0, (row + 0.5) * CellSize);
        }

        public int ColOf(double x)
        {
            return (int)Math.Floor(x / CellSize);
        }

        public int RowOf(double z)
        {
            return (int)Math.Floor(z / CellSize);
        }

        /// <summary>Coordenada X del centro del carril de la base.</summary>
        public double BaseLaneX
        {
            get { return (BaseLaneCol + 0.5) * CellSize; }
        }

        // ------------------------------------------------------------------
        // Campos
        // ------------------------------------------------------------------

        public static string FieldId(int index)
        {
            char letter = (char)('A' + index);
            return "FIELD-" + letter;
        }

        public static string FieldName(int index)
        {
            char letter = (char)('A' + index);
            return "Campo " + letter;
        }

        public int FieldMinCol(int index)
        {
            return 4 + 4 * (index % FieldCols);
        }

        public int FieldMinRow(int index)
        {
            return 1 + 4 * (index / FieldCols);
        }

        public double FieldMinX(int index)
        {
            return FieldMinCol(index) * CellSize;
        }

        public double FieldMinZ(int index)
        {
            return FieldMinRow(index) * CellSize;
        }

        /// <summary>Un campo mide 3x3 celdas.</summary>
        public double FieldSize
        {
            get { return 3 * CellSize; }
        }

        /// <summary>Celda de camino desde la que se entra al campo (a su izquierda, en el centro).</summary>
        public int FieldEntranceCol(int index)
        {
            return FieldMinCol(index) - 1;
        }

        public int FieldEntranceRow(int index)
        {
            return FieldMinRow(index) + 1;
        }

        public Position FieldCenter(int index)
        {
            return new Position(FieldMinX(index) + FieldSize / 2, 0, FieldMinZ(index) + FieldSize / 2);
        }

        // ------------------------------------------------------------------
        // Base: cargadores y aparcamientos.
        // Cada cargador y cada plaza tiene una Z distinta, así los "ramales" que van del carril
        // a cada plaza nunca se cruzan entre sí.
        // ------------------------------------------------------------------

        private double SlotZ(int slot)
        {
            int total = ChargerCount + MachineSlotCount;
            double spacing = (Depth - 2) / total;
            return 1 + spacing * (slot + 0.5);
        }

        public Position ChargerPosition(int chargerIndex)
        {
            return new Position(0.55 * CellSize, 0, SlotZ(chargerIndex));
        }

        public Position ParkingPosition(int slotIndex)
        {
            return new Position(1.35 * CellSize, 0, SlotZ(ChargerCount + slotIndex));
        }

        // ------------------------------------------------------------------
        // Búsqueda de caminos (BFS = búsqueda en anchura) por las celdas circulables.
        // ------------------------------------------------------------------

        /// <summary>
        /// Devuelve la lista de celdas {col, row} desde el inicio hasta el destino (ambas incluidas),
        /// o una lista vacía si no hay camino.
        /// </summary>
        public List<int[]> FindPath(int startCol, int startRow, int endCol, int endRow)
        {
            List<int[]> path = new List<int[]>();
            if (!IsDrivable(startCol, startRow) || !IsDrivable(endCol, endRow))
            {
                return path;
            }

            bool[,] visited = new bool[Cols, Rows];
            int[,] parentCol = new int[Cols, Rows];
            int[,] parentRow = new int[Cols, Rows];

            Queue<int[]> queue = new Queue<int[]>();
            queue.Enqueue(new int[] { startCol, startRow });
            visited[startCol, startRow] = true;
            parentCol[startCol, startRow] = -1;

            int[] dCol = { 1, -1, 0, 0 };
            int[] dRow = { 0, 0, 1, -1 };
            bool found = false;

            while (queue.Count > 0)
            {
                int[] cell = queue.Dequeue();
                if (cell[0] == endCol && cell[1] == endRow)
                {
                    found = true;
                    break;
                }

                for (int i = 0; i < 4; i++)
                {
                    int nCol = cell[0] + dCol[i];
                    int nRow = cell[1] + dRow[i];
                    if (!IsInside(nCol, nRow) || visited[nCol, nRow])
                    {
                        continue;
                    }
                    if (!CanConnect(cell[0], cell[1], nCol, nRow))
                    {
                        continue;
                    }
                    visited[nCol, nRow] = true;
                    parentCol[nCol, nRow] = cell[0];
                    parentRow[nCol, nRow] = cell[1];
                    queue.Enqueue(new int[] { nCol, nRow });
                }
            }

            if (!found)
            {
                return path;
            }

            // Reconstruir el camino yendo hacia atrás desde el destino.
            int c = endCol;
            int r = endRow;
            while (c != -1)
            {
                path.Insert(0, new int[] { c, r });
                int pc = parentCol[c, r];
                int pr = parentRow[c, r];
                if (c == startCol && r == startRow)
                {
                    break;
                }
                c = pc;
                r = pr;
            }
            return path;
        }
    }
}
