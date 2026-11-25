using System;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Posición 3D dentro de la explotación.
    /// X y Z son el suelo. Y es la altura (los drones vuelan con Y mayor que 0).
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

        /// <summary>Distancia en línea recta entre dos posiciones.</summary>
        public double DistanceTo(Position other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Distancia sobre el suelo (sin tener en cuenta la altura).</summary>
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
