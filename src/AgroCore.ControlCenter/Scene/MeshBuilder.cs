using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace AgroCore.ControlCenter.Scene
{
    /// <summary>
    /// "Low-poly" 3D sareak eraikitzen ditu pieza sinpleekin (kutxak, zilindroak, konoak...).
    ///
    /// Triangelu bakoitzak bere erpin PROPIOAK eta bere normala ditu: horrela aurpegi bakoitza
    /// kolore bakarrarekin argiztatzen da (itzaldura laua), eta horrek ematen dio itxura "low-poly".
    /// MeshBuilder berari gehitzen zaizkion pieza guztiak sare BAKAR batean amaitzen dira
    /// (sare gutxi = errendimendu hobea).
    /// </summary>
    public class MeshBuilder
    {
        private readonly MeshGeometry3D mesh = new MeshGeometry3D();

        public bool IsEmpty
        {
            get { return mesh.Positions.Count == 0; }
        }

        /// <summary>Triangelua erlojuaren orratzen aurkako noranzkoan, kanpotik ikusita.</summary>
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

        /// <summary>(cx, cy, cz) puntuan zentratutako kutxa, tamaina hau duena: sx (zabalera X), sy (altuera Y), sz (luzera Z).</summary>
        public void AddBox(double cx, double cy, double cz, double sx, double sy, double sz)
        {
            double x0 = cx - sx / 2, x1 = cx + sx / 2;
            double y0 = cy - sy / 2, y1 = cy + sy / 2;
            double z0 = cz - sz / 2, z1 = cz + sz / 2;

            // goian, behean, aurrean (+Z), atzean (-Z), eskuinean (+X), ezkerrean (-X)
            AddQuad(new Point3D(x0, y1, z1), new Point3D(x1, y1, z1), new Point3D(x1, y1, z0), new Point3D(x0, y1, z0));
            AddQuad(new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y0, z1), new Point3D(x0, y0, z1));
            AddQuad(new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), new Point3D(x0, y1, z1));
            AddQuad(new Point3D(x1, y0, z0), new Point3D(x0, y0, z0), new Point3D(x0, y1, z0), new Point3D(x1, y1, z0));
            AddQuad(new Point3D(x1, y0, z1), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), new Point3D(x1, y1, z1));
            AddQuad(new Point3D(x0, y0, z0), new Point3D(x0, y0, z1), new Point3D(x0, y1, z1), new Point3D(x0, y1, z0));
        }

        /// <summary>Lurrean bermatutako kutxa: (x, z) erdigunea da eta y0 oinarriaren altuera.</summary>
        public void AddBlock(double x, double y0, double z, double sx, double sy, double sz)
        {
            AddBox(x, y0 + sy / 2, z, sx, sy, sz);
        }

        /// <summary>Bi punturen arteko zilindroa (gurpiletarako, hodietarako, depositoetarako... balio du).</summary>
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
                AddQuad(bottom[i], bottom[next], top[next], top[i]); // alboa
                AddTriangle(end, top[i], top[next]);                 // amaierako estalkia
                AddTriangle(start, bottom[next], bottom[i]);         // hasierako estalkia
            }
        }

        /// <summary>Konoa, oinarri zirkular batetik punta bateraino.</summary>
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

        /// <summary>Aurpegi gutxiko esfera (zuhaixketarako, tomateetarako, zuhaitz-adaburuetarako).</summary>
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

        /// <summary>Prisma triangeluarra Z ardatzean zehar (bi isurialdeko teilatuak).</summary>
        public void AddRoof(double cx, double y0, double cz, double width, double height, double length)
        {
            double x0 = cx - width / 2, x1 = cx + width / 2;
            double z0 = cz - length / 2, z1 = cz + length / 2;
            double yTop = y0 + height;
            Point3D a0 = new Point3D(x0, y0, z0), b0 = new Point3D(x1, y0, z0), t0 = new Point3D(cx, yTop, z0);
            Point3D a1 = new Point3D(x0, y0, z1), b1 = new Point3D(x1, y0, z1), t1 = new Point3D(cx, yTop, z1);
            AddTriangle(a1, b1, t1);           // aurrealdea (+Z)
            AddTriangle(b0, a0, t0);           // atzealdea (-Z)
            AddQuad(b1, b0, t0, t1);           // eskuineko isurialdea
            AddQuad(a0, a1, t1, t0);           // ezkerreko isurialdea
        }

        public MeshGeometry3D ToMesh()
        {
            mesh.Freeze();
            return mesh;
        }

        /// <summary>3D eredua sortzen du kolore batekin (material matea).</summary>
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

        /// <summary>Ardatzarekiko perpendikularrak diren bi bektore kalkulatzen ditu (u x v = ardatza).</summary>
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

    /// <summary>Material berrerabilgarriak (izoztuta, azkarragoak izan daitezen).</summary>
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

        /// <summary>Distira propioa duen materiala (makinen eta kargagailuen egoera-argiak).</summary>
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
