using System;
using System.Collections.Generic;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Map
{
    /// <summary>
    /// Ustiategi baten planoa. Lursaila gelaxka karratuetan banatzen da (sare bat).
    ///
    ///   zutabea:  0  1  2  3  4  5  6  7  8  9 10 11 ...
    ///             [ BASEA][L][C][ SOROA ][C][ SOROA ][C]
    ///
    ///   - 0 eta 1 zutabeak: basea (kargagailuak eta aparkalekuak).
    ///   - 2. zutabea: "basearen errei" (L), makinak basearen barruan mugitzen diren bidea.
    ///   - 3, 7, 11, 15 zutabeak: bide bertikalak (C).
    ///   - 0, 4, 8 errenkadak: bide horizontalak.
    ///   - Bideen artean 3x3 gelaxkako soroak daude.
    ///   - Basea ATETIK bakarrik konektatzen da bideekin (GateRow errenkada).
    ///
    /// Bideko edo erreiko gelaxka bakoitza GUNE bat da. Gune batean sartzeko, makinak
    /// zerbitzarian erreserbatu behar du (talken aurkako sistema). Soroetako gelaxkak ez dira guneak:
    /// soro batean makina bakar batek egiten du lan aldi berean (zereginen koordinatzaileak bermatzen du).
    ///
    /// Klase hau Shared-en dago zerbitzariak, makinek eta Kontrol Zentroak
    /// plano bera erabili behar dutelako.
    /// </summary>
    public class FarmLayout
    {
        /// <summary>Gelaxka bakoitzaren tamaina, 3D munduko unitateetan.</summary>
        public const double CellSize = 6.0;

        /// <summary>Basearen erreiaren zutabea.</summary>
        public const int BaseLaneCol = 2;

        public int FieldCount { get; private set; }
        public int ChargerCount { get; private set; }
        public int MachineSlotCount { get; private set; }
        public int FieldCols { get; private set; }
        public int FieldRows { get; private set; }
        public int Cols { get; private set; }
        public int Rows { get; private set; }

        /// <summary>Basetik bidera irteteko errenkada.</summary>
        public int GateRow { get; private set; }

        public FarmLayout(int fieldCount, int chargerCount, int machineSlotCount)
        {
            FieldCount = Math.Max(1, fieldCount);
            ChargerCount = Math.Max(1, chargerCount);
            MachineSlotCount = Math.Max(1, machineSlotCount);

            FieldCols = Math.Min(3, FieldCount);                 // gehienez 3 soro errenkadako
            FieldRows = (FieldCount + FieldCols - 1) / FieldCols; // behar diren soro-errenkadak
            Cols = 4 + 4 * FieldCols;
            Rows = 1 + 4 * FieldRows;
            GateRow = 4 * (FieldRows / 2);                        // bide horizontal bat erdian
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
        // Gelaxkak eta guneak
        // ------------------------------------------------------------------

        public bool IsInside(int col, int row)
        {
            return col >= 0 && col < Cols && row >= 0 && row < Rows;
        }

        /// <summary>Bideko gelaxka bat da?</summary>
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

        /// <summary>Basearen barneko erreiko gelaxka bat da?</summary>
        public bool IsBaseLane(int col, int row)
        {
            return col == BaseLaneCol && row >= 0 && row < Rows;
        }

        /// <summary>Lurreko makina batek gelaxka honetatik zirkula dezake?</summary>
        public bool IsDrivable(int col, int row)
        {
            return IsRoad(col, row) || IsBaseLane(col, row);
        }

        /// <summary>Gelaxka batetik ondokora zuzenean pasa daiteke?</summary>
        public bool CanConnect(int col1, int row1, int col2, int row2)
        {
            if (!IsDrivable(col1, row1) || !IsDrivable(col2, row2))
            {
                return false;
            }
            // Basearen erreiaren eta bidearen artean atetik bakarrik pasatzen da.
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

        /// <summary>"Z07-04" 7. zutabe eta 4. errenkada bihurtzen du.</summary>
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

        /// <summary>Basearen erreiaren erdiko X koordenatua.</summary>
        public double BaseLaneX
        {
            get { return (BaseLaneCol + 0.5) * CellSize; }
        }

        // ------------------------------------------------------------------
        // Soroak
        // ------------------------------------------------------------------

        public static string FieldId(int index)
        {
            char letter = (char)('A' + index);
            return "FIELD-" + letter;
        }

        public static string FieldName(int index)
        {
            char letter = (char)('A' + index);
            return letter + " soroa";
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

        /// <summary>Soro batek 3x3 gelaxka neurtzen ditu.</summary>
        public double FieldSize
        {
            get { return 3 * CellSize; }
        }

        /// <summary>Sorora sartzeko bideko gelaxka (ezkerrean, erdian).</summary>
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
        // Basea: kargagailuak eta aparkalekuak.
        // Kargagailu eta aparkaleku bakoitzak Z desberdina du; horrela, erreitik
        // aparkaleku bakoitzera doazen "adarrak" ez dira inoiz elkarren artean gurutzatzen.
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
        // Bide-bilaketa (BFS = zabalerako bilaketa) gelaxka zirkulagarrietan zehar.
        // ------------------------------------------------------------------

        /// <summary>
        /// {col, row} gelaxken zerrenda itzultzen du hasieratik helmugaraino (biak barne),
        /// edo zerrenda huts bat bidea ez badago.
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

            // Bidea berreraiki helmugatik atzera eginez.
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
