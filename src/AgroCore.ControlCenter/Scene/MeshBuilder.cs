using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace AgroCore.ControlCenter.Scene
{
    /// <summary>
    /// Construye mallas 3D "low-poly" a base de piezas simples (cajas, cilindros, conos...).
    ///
    /// Cada triángulo tiene sus PROPIOS vértices y su normal: así cada cara se ilumina con un
    /// solo color (sombreado plano), que es lo que da el aspecto "low-poly".
    /// Todas las piezas que se añaden al mismo MeshBuilder acaban en UNA sola malla
    /// (pocas mallas = mejor rendimiento).
    /// </summary>
    public class MeshBuilder
    {
        private readonly MeshGeometry3D mesh = new MeshGeometry3D();

        public bool IsEmpty
        {
            get { return mesh.Positions.Count == 0; }
        }

        /// <summary>Triángulo en sentido antihorario visto desde fuera.</summary>
        public void AddTriangle(Point3D a, Point3D b, Point3D c)
        {
            Vector3D normal = Vector3D.CrossProduct(b - a, c - a);
            if (normal.Length < 1e-9)
            {
                return;
            }
            normal.Normalize();

            int index = mesh.Positions.Count;
            mesh.Positions.Add(a);
            mesh.Positions.Add(b);
            mesh.Positions.Add(c);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.TriangleIndices.Add(index);
            mesh.TriangleIndices.Add(index + 1);
            mesh.TriangleIndices.Add(index + 2);
        }

        public void AddQuad(Point3D a, Point3D b, Point3D c, Point3D d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        /// <summary>Caja centrada en (cx, cy, cz) con tamaño sx (ancho X), sy (alto Y), sz (largo Z).</summary>
        public void AddBox(double cx, double cy, double cz, double sx, double sy, double sz)
        {
            double x0 = cx - sx / 2, x1 = cx + sx / 2;
            double y0 = cy - sy / 2, y1 = cy + sy / 2;
            double z0 = cz - sz / 2, z1 = cz + sz / 2;

            // arriba, abajo, delante (+Z), detrás (-Z), derecha (+X), izquierda (-X)
            AddQuad(new Point3D(x0, y1, z1), new Point3D(x1, y1, z1), new Point3D(x1, y1, z0), new Point3D(x0, y1, z0));
            AddQuad(new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y0, z1), new Point3D(x0, y0, z1));
            AddQuad(new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), new Point3D(x0, y1, z1));
            AddQuad(new Point3D(x1, y0, z0), new Point3D(x0, y0, z0), new Point3D(x0, y1, z0), new Point3D(x1, y1, z0));
            AddQuad(new Point3D(x1, y0, z1), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), new Point3D(x1, y1, z1));
            AddQuad(new Point3D(x0, y0, z0), new Point3D(x0, y0, z1), new Point3D(x0, y1, z1), new Point3D(x0, y1, z0));
        }

        /// <summary>Caja apoyada en el suelo: (x, z) es el centro y y0 la altura de la base.</summary>
        public void AddBlock(double x, double y0, double z, double sx, double sy, double sz)
        {
            AddBox(x, y0 + sy / 2, z, sx, sy, sz);
        }

        /// <summary>Cilindro entre dos puntos (sirve para ruedas, tubos, depósitos...).</summary>
        public void AddCylinder(Point3D start, Point3D end, double radius, int segments)
        {
            Vector3D u, v;
            Basis(end - start, out u, out v);

            Point3D[] bottom = new Point3D[segments];
            Point3D[] top = new Point3D[segments];
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                Vector3D offset = u * (radius * Math.Cos(angle)) + v * (radius * Math.Sin(angle));
                bottom[i] = start + offset;
                top[i] = end + offset;
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                AddQuad(bottom[i], bottom[next], top[next], top[i]); // lateral
                AddTriangle(end, top[i], top[next]);                 // tapa final
                AddTriangle(start, bottom[next], bottom[i]);         // tapa inicial
            }
        }

        /// <summary>Cono desde una base circular hasta una punta.</summary>
        public void AddCone(Point3D baseCenter, Point3D tip, double radius, int segments)
        {
            Vector3D u, v;
            Basis(tip - baseCenter, out u, out v);

            Point3D[] ring = new Point3D[segments];
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                ring[i] = baseCenter + u * (radius * Math.Cos(angle)) + v * (radius * Math.Sin(angle));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                AddTriangle(ring[i], ring[next], tip);
                AddTriangle(baseCenter, ring[next], ring[i]);
            }
        }

        /// <summary>Esfera de pocas caras (para arbustos, tomates, copas de árboles).</summary>
        public void AddSphere(Point3D center, double radius, int slices, int stacks)
        {
            for (int i = 0; i < stacks; i++)
            {
                double phi1 = Math.PI * i / stacks;
                double phi2 = Math.PI * (i + 1) / stacks;
                for (int j = 0; j < slices; j++)
                {
                    double theta1 = 2 * Math.PI * j / slices;
                    double theta2 = 2 * Math.PI * (j + 1) / slices;
                    Point3D a = SpherePoint(center, radius, phi1, theta1);
                    Point3D b = SpherePoint(center, radius, phi2, theta1);
                    Point3D c = SpherePoint(center, radius, phi2, theta2);
                    Point3D d = SpherePoint(center, radius, phi1, theta2);
                    AddQuad(a, d, c, b);
                }
            }
        }

        /// <summary>Prisma triangular a lo largo de Z (tejados a dos aguas).</summary>
        public void AddRoof(double cx, double y0, double cz, double width, double height, double length)
        {
            double x0 = cx - width / 2, x1 = cx + width / 2;
            double z0 = cz - length / 2, z1 = cz + length / 2;
            double yTop = y0 + height;
            Point3D a0 = new Point3D(x0, y0, z0), b0 = new Point3D(x1, y0, z0), t0 = new Point3D(cx, yTop, z0);
            Point3D a1 = new Point3D(x0, y0, z1), b1 = new Point3D(x1, y0, z1), t1 = new Point3D(cx, yTop, z1);
            AddTriangle(a1, b1, t1);           // frontal (+Z)
            AddTriangle(b0, a0, t0);           // trasero (-Z)
            AddQuad(b1, b0, t0, t1);           // faldón derecho
            AddQuad(a0, a1, t1, t0);           // faldón izquierdo
        }

        public MeshGeometry3D ToMesh()
        {
            mesh.Freeze();
            return mesh;
        }

        /// <summary>Crea el modelo 3D con un color (material mate).</summary>
        public GeometryModel3D ToModel(Color color)
        {
            return new GeometryModel3D(ToMesh(), Materials.Solid(color));
        }

        // ------------------------------------------------------------------

        private static Point3D SpherePoint(Point3D c, double r, double phi, double theta)
        {
            return new Point3D(c.X + r * Math.Sin(phi) * Math.Cos(theta),
                               c.Y + r * Math.Cos(phi),
                               c.Z + r * Math.Sin(phi) * Math.Sin(theta));
        }

        /// <summary>Calcula dos vectores perpendiculares al eje (u x v = eje).</summary>
        private static void Basis(Vector3D axis, out Vector3D u, out Vector3D v)
        {
            axis.Normalize();
            Vector3D helper = Math.Abs(axis.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
            u = Vector3D.CrossProduct(axis, helper);
            u.Normalize();
            v = Vector3D.CrossProduct(axis, u);
            v.Normalize();
        }
    }

    /// <summary>Materiales reutilizables (congelados para que sean más rápidos).</summary>
    public static class Materials
    {
        public static Material Solid(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            DiffuseMaterial material = new DiffuseMaterial(brush);
            material.Freeze();
            return material;
        }

        /// <summary>Material con brillo propio (luces de estado de las máquinas y cargadores).</summary>
        public static Material Glow(SolidColorBrush brush)
        {
            MaterialGroup group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(brush));
            group.Children.Add(new EmissiveMaterial(brush));
            return group;
        }

        public static Color Rgb(byte r, byte g, byte b)
        {
            return Color.FromRgb(r, g, b);
        }

        public static Color Argb(byte a, byte r, byte g, byte b)
        {
            return Color.FromArgb(a, r, g, b);
        }
    }
}
