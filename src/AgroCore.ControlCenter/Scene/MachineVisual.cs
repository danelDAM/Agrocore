using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.Enums;

namespace AgroCore.ControlCenter.Scene
{
    /// <summary>
    /// Makina bat 3D eszenan: bere low-poly eredua, bere posizioa eta bere animazioak.
    ///
    /// Ereduak +Z aldera begira eraikitzen dira (0 norabidea) eta gurpilak y = 0 puntuan dituztela.
    /// Telemetriaz iristen den posizioa (200 ms-ro) "helburua" da; fotograma bakoitzean
    /// marraztutako posizioa poliki hurbiltzen zaio helburu horri (interpolazioa), horrela
    /// mugimendua jarraitua eta jauzirik gabea ikusten da.
    /// </summary>
    public class MachineVisual
    {
        // Koloreak
        private static readonly Color Black = Materials.Rgb(38, 38, 40);
        private static readonly Color Glass = Materials.Rgb(150, 205, 232);
        private static readonly Color Metal = Materials.Rgb(150, 152, 156);
        private static readonly Color DarkMetal = Materials.Rgb(70, 74, 80);

        public string MachineId { get; private set; }
        public MachineType Type { get; private set; }

        /// <summary>Viewport3D-ri gehitzen zaion objektua.</summary>
        public ModelVisual3D Visual { get; private set; }

        /// <summary>Itzala lurrean (droneak bakarrik, hegan daudenean non dauden ikusteko).</summary>
        public ModelVisual3D Shadow { get; private set; }

        /// <summary>Makinaren gaineko puntua, bere 2D etiketa jartzen den lekua.</summary>
        public Point3D LabelPoint
        {
            get { return new Point3D(x, y + (Type == MachineType.Sprayer ? 2.2 : 4.0), z); }
        }

        public MachineStatus Status { get; private set; }
        public bool Online { get; private set; }
        public double Battery { get; set; }

        private readonly Model3DGroup group = new Model3DGroup();
        private readonly TranslateTransform3D translate = new TranslateTransform3D();
        private readonly AxisAngleRotation3D rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        private readonly TranslateTransform3D shadowTranslate = new TranslateTransform3D();
        private readonly SolidColorBrush lightBrush = new SolidColorBrush(Colors.White);
        private readonly List<AxisAngleRotation3D> spinners = new List<AxisAngleRotation3D>();
        private GeometryModel3D workEffect;
        private GeometryModel3D selectionRing;
        private bool effectVisible = false;
        private bool selected = false;

        // Marraztutako posizioa eta helburuko posizioa
        private double x, y, z, heading;
        private double targetX, targetY, targetZ, targetHeading;
        private bool hasPosition = false;
        private double time = 0;

        public MachineVisual(string machineId, MachineType type)
        {
            MachineId = machineId;
            Type = type;

            switch (type)
            {
                case MachineType.Harvester: BuildHarvester(); break;
                case MachineType.Seeder: BuildSeeder(); break;
                case MachineType.Irrigator: BuildIrrigator(); break;
                case MachineType.Sprayer: BuildDrone(); break;
            }

            // Hautapen-eraztuna (ezkutuan makina hautatu arte)
            MeshBuilder ring = new MeshBuilder();
            ring.AddCylinder(new Point3D(0, 0.06, 0), new Point3D(0, 0.1, 0), Type == MachineType.Sprayer ? 1.8 : 2.6, 24);
            selectionRing = ring.ToModel(Materials.Argb(110, 255, 214, 64));

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(rotation));
            transform.Children.Add(translate);
            group.Transform = transform;

            Visual = new ModelVisual3D();
            Visual.Content = group;

            if (type == MachineType.Sprayer)
            {
                MeshBuilder shadow = new MeshBuilder();
                shadow.AddCylinder(new Point3D(0, 0.04, 0), new Point3D(0, 0.05, 0), 1.1, 16);
                GeometryModel3D shadowModel = shadow.ToModel(Materials.Argb(90, 0, 0, 0));
                shadowModel.Transform = shadowTranslate;
                Shadow = new ModelVisual3D();
                Shadow.Content = shadowModel;
            }
        }

        // ==================================================================
        // ZERBITZARITIK IRISTEN DIREN DATUAK
        // ==================================================================

        public void SetTarget(double newX, double newY, double newZ, double newHeading)
        {
            targetX = newX;
            targetY = newY;
            targetZ = newZ;
            targetHeading = newHeading;

            // Lehen aldian (edo oso urrun badago) zuzenean kokatzen da
            double distance = Math.Sqrt((x - newX) * (x - newX) + (z - newZ) * (z - newZ));
            if (!hasPosition || distance > 25)
            {
                x = newX;
                y = newY;
                z = newZ;
                heading = newHeading;
                hasPosition = true;
                ApplyTransform();
            }
        }

        public void SetStatus(MachineStatus status, bool online)
        {
            Status = status;
            Online = online;
            lightBrush.Color = Texts.StatusColor(online ? status : MachineStatus.Offline);

            bool showEffect = online && status == MachineStatus.Working && workEffect != null;
            if (showEffect != effectVisible)
            {
                if (showEffect)
                {
                    group.Children.Add(workEffect);
                }
                else
                {
                    group.Children.Remove(workEffect);
                }
                effectVisible = showEffect;
            }
        }

        public void SetSelected(bool value)
        {
            if (value == selected)
            {
                return;
            }
            selected = value;
            if (selected)
            {
                group.Children.Add(selectionRing);
            }
            else
            {
                group.Children.Remove(selectionRing);
            }
        }

        public Point3D CurrentPosition
        {
            get { return new Point3D(x, y, z); }
        }

        // ==================================================================
        // ANIMAZIOA (fotograma bakoitzean deitzen da)
        // ==================================================================

        public void Animate(double dt)
        {
            time += dt;

            // Interpolazio leuna helburuko posiziorantz
            double k = 1 - Math.Exp(-dt * 9);
            x += (targetX - x) * k;
            y += (targetY - y) * k;
            z += (targetZ - z) * k;

            double delta = targetHeading - heading;
            while (delta > 180) delta -= 360;
            while (delta < -180) delta += 360;
            heading += delta * k;

            ApplyTransform();

            // Biratzen diren piezak (droneen errotoreak, uzta-biltzailearen errota)
            bool flying = Type == MachineType.Sprayer && y > 0.3;
            for (int i = 0; i < spinners.Count; i++)
            {
                double speed = 0;
                if (Type == MachineType.Sprayer)
                {
                    speed = flying ? 1400 : (Online && Status != MachineStatus.Charging && Status != MachineStatus.Parked ? 200 : 0);
                    if (i % 2 == 1)
                    {
                        speed = -speed; // errotoreek noranzko txandakatuetan biratzen dute
                    }
                }
                else if (Status == MachineStatus.Working)
                {
                    speed = 220;
                }
                spinners[i].Angle = (spinners[i].Angle + speed * dt) % 360;
            }
        }

        private void ApplyTransform()
        {
            translate.OffsetX = x;
            translate.OffsetY = y;
            translate.OffsetZ = z;
            rotation.Angle = heading;

            if (Shadow != null)
            {
                shadowTranslate.OffsetX = x;
                shadowTranslate.OffsetZ = z;
            }
        }

        // ==================================================================
        // LOW-POLY EREDUAK
        // ==================================================================

        private void AddPart(MeshBuilder builder, Color color)
        {
            if (!builder.IsEmpty)
            {
                group.Children.Add(builder.ToModel(color));
            }
        }

        private void AddStatusLight(double lx, double ly, double lz)
        {
            MeshBuilder light = new MeshBuilder();
            light.AddBox(lx, ly, lz, 0.32, 0.2, 0.32);
            group.Children.Add(new GeometryModel3D(light.ToMesh(), Materials.Glow(lightBrush)));
        }

        /// <summary>Gurpila: X ardatzean zehar dagoen zilindroa.</summary>
        private static void AddWheel(MeshBuilder wheels, MeshBuilder hubs, double wx, double wz, double radius, double width)
        {
            double side = wx > 0 ? 1 : -1;
            Point3D inner = new Point3D(wx - side * width / 2, radius, wz);
            Point3D outer = new Point3D(wx + side * width / 2, radius, wz);
            wheels.AddCylinder(inner, outer, radius, 10);
            hubs.AddCylinder(outer, new Point3D(outer.X + side * 0.04, radius, wz), radius * 0.45, 8);
        }

        /// <summary>Uzta-biltzailea (erreferentziako low-poly ereduan oinarritua).</summary>
        private void BuildHarvester()
        {
            MeshBuilder body = new MeshBuilder();
            MeshBuilder bodyLight = new MeshBuilder();
            MeshBuilder header = new MeshBuilder();
            MeshBuilder glass = new MeshBuilder();
            MeshBuilder metal = new MeshBuilder();
            MeshBuilder dark = new MeshBuilder();
            MeshBuilder wheels = new MeshBuilder();
            MeshBuilder hubs = new MeshBuilder();

            body.AddBlock(0, 0.55, -0.3, 2.1, 1.5, 3.3);           // gorputz nagusia
            bodyLight.AddBlock(0, 2.05, -0.8, 1.9, 0.7, 1.7);      // ale-tolba
            body.AddBlock(0, 2.05, 0.85, 1.55, 0.25, 1.1);         // kabinaren oinarria
            glass.AddBlock(0, 2.3, 0.85, 1.4, 0.95, 1.0);          // kabina beiraztatua
            body.AddBlock(0, 3.25, 0.85, 1.6, 0.12, 1.2);          // teilatua
            dark.AddBlock(0, 0.55, 1.55, 1.3, 0.75, 0.7);          // elikagailua
            header.AddBlock(0, 0.2, 2.2, 3.4, 0.55, 0.9);          // ebaketa-burua
            header.AddBlock(-1.65, 0.2, 2.2, 0.12, 0.9, 0.95);     // buruaren alboak
            header.AddBlock(1.65, 0.2, 2.2, 0.12, 0.9, 0.95);
            for (int i = -3; i <= 3; i++)                           // hortzak
            {
                dark.AddBlock(i * 0.42, 0.12, 2.75, 0.08, 0.1, 0.35);
            }
            metal.AddCylinder(new Point3D(-0.95, 2.45, -1.3), new Point3D(-2.2, 2.65, 0.3), 0.13, 8); // deskarga-hodia
            dark.AddCylinder(new Point3D(0.65, 2.7, -1.5), new Point3D(0.65, 3.4, -1.5), 0.08, 6);    // ihes-hodia

            AddWheel(wheels, hubs, 1.25, 0.55, 0.75, 0.5);
            AddWheel(wheels, hubs, -1.25, 0.55, 0.75, 0.5);
            AddWheel(wheels, hubs, 1.15, -1.45, 0.48, 0.35);
            AddWheel(wheels, hubs, -1.15, -1.45, 0.48, 0.35);

            AddPart(body, Materials.Rgb(204, 64, 46));
            AddPart(bodyLight, Materials.Rgb(226, 98, 70));
            AddPart(header, Materials.Rgb(236, 186, 48));
            AddPart(glass, Glass);
            AddPart(metal, Metal);
            AddPart(dark, DarkMetal);
            AddPart(wheels, Black);
            AddPart(hubs, Materials.Rgb(236, 186, 48));

            // Buruaren errota: uzta biltzen ari den bitartean biratzen du
            MeshBuilder reel = new MeshBuilder();
            reel.AddCylinder(new Point3D(-1.55, 0, 0), new Point3D(1.55, 0, 0), 0.08, 6);
            for (int i = 0; i < 4; i++)
            {
                double a = i * Math.PI / 2;
                reel.AddBox(0, Math.Sin(a) * 0.35, Math.Cos(a) * 0.35, 3.0, 0.06, 0.06);
            }
            GeometryModel3D reelModel = reel.ToModel(Materials.Rgb(250, 210, 80));
            AxisAngleRotation3D reelRotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
            Transform3DGroup reelTransform = new Transform3DGroup();
            reelTransform.Children.Add(new RotateTransform3D(reelRotation));
            reelTransform.Children.Add(new TranslateTransform3D(0, 1.0, 2.35));
            reelModel.Transform = reelTransform;
            group.Children.Add(reelModel);
            spinners.Add(reelRotation);

            // Hautsa/lastoa atzean lanean ari den bitartean
            MeshBuilder dust = new MeshBuilder();
            dust.AddSphere(new Point3D(0, 0.9, -2.6), 0.9, 6, 4);
            dust.AddSphere(new Point3D(0.4, 0.7, -3.3), 0.6, 6, 4);
            workEffect = dust.ToModel(Materials.Argb(90, 222, 196, 130));

            AddStatusLight(0, 3.42, 0.85);
        }

        /// <summary>Ereilea: traktore urdina, tolbadun ereile bati tiraka.</summary>
        private void BuildSeeder()
        {
            MeshBuilder body = new MeshBuilder();
            MeshBuilder glass = new MeshBuilder();
            MeshBuilder implement = new MeshBuilder();
            MeshBuilder hoppers = new MeshBuilder();
            MeshBuilder dark = new MeshBuilder();
            MeshBuilder wheels = new MeshBuilder();
            MeshBuilder hubs = new MeshBuilder();

            body.AddBlock(0, 0.55, 0.9, 1.0, 0.85, 1.7);            // kapota
            dark.AddBlock(0, 0.55, 1.78, 0.9, 0.6, 0.08);           // sareta
            body.AddBlock(0, 0.45, -0.35, 1.3, 0.6, 1.2);           // txasisa kabinaren azpian
            glass.AddBlock(0, 1.05, -0.35, 1.25, 1.15, 1.1);        // kabina
            body.AddBlock(0, 2.2, -0.35, 1.4, 0.12, 1.25);          // teilatua
            dark.AddCylinder(new Point3D(0.3, 1.4, 1.4), new Point3D(0.3, 2.0, 1.4), 0.06, 6); // ihes-hodia

            AddWheel(wheels, hubs, 0.95, -0.35, 0.78, 0.45);        // atzeko gurpil handiak
            AddWheel(wheels, hubs, -0.95, -0.35, 0.78, 0.45);
            AddWheel(wheels, hubs, 0.7, 1.25, 0.45, 0.3);           // aurreko gurpil txikiak
            AddWheel(wheels, hubs, -0.7, 1.25, 0.45, 0.3);

            dark.AddBlock(0, 0.45, -1.35, 0.2, 0.18, 0.9);          // lotunea
            implement.AddBlock(0, 0.35, -2.0, 3.4, 0.25, 0.4);      // ereilearen barra
            for (int i = -1; i <= 1; i++)
            {
                hoppers.AddBlock(i * 1.05, 0.6, -2.0, 0.75, 0.6, 0.65); // hazi-tolbak
                dark.AddBlock(i * 1.05, 0.05, -2.2, 0.1, 0.35, 0.1);    // goldeak
            }
            AddWheel(wheels, hubs, 1.8, -2.0, 0.3, 0.18);
            AddWheel(wheels, hubs, -1.8, -2.0, 0.3, 0.18);

            AddPart(body, Materials.Rgb(46, 108, 196));
            AddPart(glass, Glass);
            AddPart(implement, Materials.Rgb(224, 124, 40));
            AddPart(hoppers, Materials.Rgb(242, 180, 60));
            AddPart(dark, DarkMetal);
            AddPart(wheels, Black);
            AddPart(hubs, Materials.Rgb(230, 230, 230));

            // Haziak erortzen lanean ari den bitartean
            MeshBuilder seeds = new MeshBuilder();
            for (int i = -1; i <= 1; i++)
            {
                seeds.AddBox(i * 1.05, 0.12, -2.6, 0.5, 0.06, 0.6);
            }
            workEffect = seeds.ToModel(Materials.Argb(160, 120, 84, 50));

            AddStatusLight(0, 2.36, -0.35);
        }

        /// <summary>Ureztatzailea: ur-depositua eta atzeko ureztatze-barra dituen ibilgailua.</summary>
        private void BuildIrrigator()
        {
            MeshBuilder chassis = new MeshBuilder();
            MeshBuilder body = new MeshBuilder();
            MeshBuilder glass = new MeshBuilder();
            MeshBuilder tank = new MeshBuilder();
            MeshBuilder metal = new MeshBuilder();
            MeshBuilder wheels = new MeshBuilder();
            MeshBuilder hubs = new MeshBuilder();

            chassis.AddBlock(0, 0.55, -0.2, 1.5, 0.35, 3.8);        // txasisa
            body.AddBlock(0, 0.9, 1.25, 1.45, 0.4, 1.0);            // kabinaren oinarria
            glass.AddBlock(0, 1.3, 1.25, 1.35, 0.95, 0.95);         // kabina
            body.AddBlock(0, 2.25, 1.25, 1.5, 0.12, 1.1);           // teilatua
            tank.AddCylinder(new Point3D(0, 1.6, -1.8), new Point3D(0, 1.6, 0.55), 0.78, 12); // depositua
            body.AddCylinder(new Point3D(0, 1.6, -1.2), new Point3D(0, 1.6, -1.05), 0.82, 12); // uztaiak
            body.AddCylinder(new Point3D(0, 1.6, 0.0), new Point3D(0, 1.6, 0.15), 0.82, 12);
            metal.AddCylinder(new Point3D(0, 2.35, -0.5), new Point3D(0, 2.5, -0.5), 0.25, 8); // estalkia

            // Ureztatze-barra
            metal.AddCylinder(new Point3D(-2.6, 1.0, -2.25), new Point3D(2.6, 1.0, -2.25), 0.08, 6);
            metal.AddBlock(0, 0.7, -2.1, 0.25, 0.5, 0.3);
            for (int i = -5; i <= 5; i++)
            {
                metal.AddBlock(i * 0.5, 0.82, -2.25, 0.06, 0.18, 0.06); // pitak
            }

            AddWheel(wheels, hubs, 0.95, 1.2, 0.62, 0.35);
            AddWheel(wheels, hubs, -0.95, 1.2, 0.62, 0.35);
            AddWheel(wheels, hubs, 0.95, -1.3, 0.62, 0.35);
            AddWheel(wheels, hubs, -0.95, -1.3, 0.62, 0.35);

            AddPart(chassis, DarkMetal);
            AddPart(body, Materials.Rgb(32, 150, 150));
            AddPart(glass, Glass);
            AddPart(tank, Materials.Rgb(214, 232, 242));
            AddPart(metal, Metal);
            AddPart(wheels, Black);
            AddPart(hubs, Materials.Rgb(32, 150, 150));

            // Ur-gortina ureztatzen ari den bitartean
            MeshBuilder water = new MeshBuilder();
            water.AddBlock(0, 0.05, -2.3, 5.0, 0.75, 0.5);
            workEffect = water.ToModel(Materials.Argb(80, 90, 170, 255));

            AddStatusLight(0, 2.41, 1.25);
        }

        /// <summary>4 errotoreko fumigazio-dronea.</summary>
        private void BuildDrone()
        {
            const double lift = 0.75; // eredua igotzen da patinak y = 0 puntuan gera daitezen

            MeshBuilder shell = new MeshBuilder();
            MeshBuilder dark = new MeshBuilder();
            MeshBuilder tank = new MeshBuilder();
            MeshBuilder accent = new MeshBuilder();

            shell.AddBox(0, lift, 0, 0.9, 0.35, 1.15);                 // gorputza
            dark.AddBox(0, lift + 0.23, 0, 0.6, 0.12, 0.75);           // goiko estalkia
            accent.AddBox(0, lift, 0.6, 0.5, 0.18, 0.06);              // aurrealdea (nora begira dagoen ikusteko)
            tank.AddCylinder(new Point3D(0, lift - 0.55, 0), new Point3D(0, lift - 0.17, 0), 0.27, 8); // depositua
            dark.AddCylinder(new Point3D(-0.95, lift - 0.6, 0), new Point3D(0.95, lift - 0.6, 0), 0.04, 6); // barra
            // hankak eta patinak
            dark.AddBox(0.35, lift - 0.45, 0, 0.06, 0.6, 0.06);
            dark.AddBox(-0.35, lift - 0.45, 0, 0.06, 0.6, 0.06);
            dark.AddCylinder(new Point3D(0.35, 0.04, -0.5), new Point3D(0.35, 0.04, 0.5), 0.04, 6);
            dark.AddCylinder(new Point3D(-0.35, 0.04, -0.5), new Point3D(-0.35, 0.04, 0.5), 0.04, 6);

            double[] armX = { 1.0, -1.0, -1.0, 1.0 };
            double[] armZ = { 1.0, 1.0, -1.0, -1.0 };
            for (int i = 0; i < 4; i++)
            {
                Point3D end = new Point3D(armX[i], lift + 0.1, armZ[i]);
                dark.AddCylinder(new Point3D(0, lift + 0.05, 0), end, 0.06, 6);                       // besoa
                dark.AddCylinder(new Point3D(armX[i], lift, armZ[i]), new Point3D(armX[i], lift + 0.3, armZ[i]), 0.13, 8); // motorra

                // Errotorea: eredu propioa, biratu ahal izateko
                MeshBuilder rotor = new MeshBuilder();
                rotor.AddBox(0, 0, 0, 1.3, 0.03, 0.13);
                rotor.AddBox(0, 0, 0, 0.13, 0.03, 1.3);
                GeometryModel3D rotorModel = rotor.ToModel(Materials.Rgb(30, 30, 32));
                MeshBuilder disc = new MeshBuilder();
                disc.AddCylinder(new Point3D(0, -0.01, 0), new Point3D(0, 0.0, 0), 0.68, 16);
                GeometryModel3D discModel = disc.ToModel(Materials.Argb(45, 200, 200, 200));

                AxisAngleRotation3D spin = new AxisAngleRotation3D(new Vector3D(0, 1, 0), i * 30);
                Transform3DGroup rotorTransform = new Transform3DGroup();
                rotorTransform.Children.Add(new RotateTransform3D(spin));
                rotorTransform.Children.Add(new TranslateTransform3D(armX[i], lift + 0.34, armZ[i]));
                rotorModel.Transform = rotorTransform;
                discModel.Transform = new TranslateTransform3D(armX[i], lift + 0.34, armZ[i]);
                group.Children.Add(rotorModel);
                spinners.Add(spin);
                // disko erdi-gardenak amaieran gehitzen dira (ikus beherago)
                transparentParts.Add(discModel);
            }

            AddPart(shell, Materials.Rgb(238, 240, 242));
            AddPart(dark, Materials.Rgb(52, 56, 62));
            AddPart(tank, Materials.Rgb(92, 170, 96));
            AddPart(accent, Materials.Rgb(235, 90, 60));

            foreach (GeometryModel3D part in transparentParts)
            {
                group.Children.Add(part); // gardena opakoaren ondoren marrazten da
            }

            // Produktu fitosanitarioaren hodeia fumigatzen ari den bitartean
            MeshBuilder spray = new MeshBuilder();
            spray.AddCone(new Point3D(0, lift - 2.4, 0), new Point3D(0, lift - 0.6, 0), 1.2, 10);
            workEffect = spray.ToModel(Materials.Argb(70, 170, 230, 150));

            AddStatusLight(0, lift + 0.33, -0.4);
        }

        private readonly List<GeometryModel3D> transparentParts = new List<GeometryModel3D>();
    }
}
