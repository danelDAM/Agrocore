using System;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// 3D posizioa ustiategiaren barruan.
    /// X eta Z lurra dira. Y altuera da (droneek 0 baino Y handiagoarekin egiten dute hegan).
    /// </summary>
    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Position()
        {
        }

        public Position(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Bi posizioren arteko distantzia lerro zuzenean.</summary>
        public double DistanceTo(Position other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Lurraren gaineko distantzia (altuera kontuan hartu gabe).</summary>
        public double GroundDistanceTo(Position other)
        {
            double dx = X - other.X;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public Position Clone()
        {
            return new Position(X, Y, Z);
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.0") + ", " + Y.ToString("0.0") + ", " + Z.ToString("0.0") + ")";
        }
    }
}
