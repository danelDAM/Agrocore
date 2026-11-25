using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.Enums;

namespace AgroCore.ControlCenter.Scene
{
    /// <summary>
    /// Una máquina en la escena 3D: su modelo low-poly, su posición y sus animaciones.
    ///
    /// Los modelos se construyen mirando hacia +Z (rumbo 0) y con las ruedas en y = 0.
    /// La posición que llega por telemetría (cada 200 ms) es el "objetivo"; en cada fotograma
    /// la posición dibujada se acerca suavemente a ese objetivo (interpolación), así el
    /// movimiento se ve continuo y sin saltos.
    /// </summary>
    public class MachineVisual
    {
        // Colores
        private static readonly Color Black = Materials.Rgb(38, 38, 40);
        private static readonly Color Glass = Materials.Rgb(150, 205, 232);
        private static readonly Color Metal = Materials.Rgb(150, 152, 156);
        private static readonly Color DarkMetal = Materials.Rgb(70, 74, 80);

        public string MachineId { get; private set; }
        public MachineType Type { get; private set; }

        /// <summary>Objeto que se añade al Viewport3D.</summary>
        public ModelVisual3D Visual { get; private set; }

        /// <summary>Sombra en el suelo (solo drones, para ver dónde están cuando vuelan).</summary>
        public ModelVisual3D Shadow { get; private set; }

        /// <summary>Punto sobre la máquina donde se coloca su etiqueta 2D.</summary>
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

        // Posición dibujada y posición objetivo
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

            // Anillo de selección (oculto hasta que se selecciona la máquina)
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
        // DATOS QUE LLEGAN DEL SERVIDOR
        // ==================================================================

        public void SetTarget(double newX, double newY, double newZ, double newHeading)
        {
            targetX = newX;
            targetY = newY;
            targetZ = newZ;
            targetHeading = newHeading;

            // La primera vez (o si está muy lejos) se coloca directamente
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
        // ANIMACIÓN (se llama en cada fotograma)
        // ==================================================================

        public void Animate(double dt)
        {
            time += dt;

            // Interpolación suave hacia la posición objetivo
            double k = 1 - Math.Exp(-dt * 9);
            x += (targetX - x) * k;
            y += (targetY - y) * k;
            z += (targetZ - z) * k;

            double delta = targetHeading - heading;
            while (delta > 180) delta -= 360;
            while (delta < -180) delta += 360;
            heading += delta * k;

            ApplyTransform();

            // Piezas que giran (rotores de los drones, molinete de la cosechadora)
            bool flying = Type == MachineType.Sprayer && y > 0.3;
            for (int i = 0; i < spinners.Count; i++)
            {
                double speed = 0;
                if (Type == MachineType.Sprayer)
                {
                    speed = flying ? 1400 : (Online && Status != MachineStatus.Charging && Status != MachineStatus.Parked ? 200 : 0);
                    if (i % 2 == 1)
                    {
                        speed = -speed; // los rotores giran en sentidos alternos
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
        // MODELOS LOW-POLY
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

        /// <summary>Rueda: cilindro a lo largo del eje X.</summary>
        private static void AddWheel(MeshBuilder wheels, MeshBuilder hubs, double wx, double wz, double radius, double width)
        {
            double side = wx > 0 ? 1 : -1;
            Point3D inner = new Point3D(wx - side * width / 2, radius, wz);
            Point3D outer = new Point3D(wx + side * width / 2, radius, wz);
            wheels.AddCylinder(inner, outer, radius, 10);
            hubs.AddCylinder(outer, new Point3D(outer.X + side * 0.04, radius, wz), radius * 0.45, 8);
        }

        /// <summary>Cosechadora (inspirada en la del modelo low-poly de referencia).</summary>
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

            body.AddBlock(0, 0.55, -0.3, 2.1, 1.5, 3.3);           // cuerpo principal
            bodyLight.AddBlock(0, 2.05, -0.8, 1.9, 0.7, 1.7);      // tolva de grano
            body.AddBlock(0, 2.05, 0.85, 1.55, 0.25, 1.1);         // base de la cabina
            glass.AddBlock(0, 2.3, 0.85, 1.4, 0.95, 1.0);          // cabina acristalada
            body.AddBlock(0, 3.25, 0.85, 1.6, 0.12, 1.2);          // techo
            dark.AddBlock(0, 0.55, 1.55, 1.3, 0.75, 0.7);          // alimentador
            header.AddBlock(0, 0.2, 2.2, 3.4, 0.55, 0.9);          // cabezal de corte
            header.AddBlock(-1.65, 0.2, 2.2, 0.12, 0.9, 0.95);     // laterales del cabezal
            header.AddBlock(1.65, 0.2, 2.2, 0.12, 0.9, 0.95);
            for (int i = -3; i <= 3; i++)                           // dientes
            {
                dark.AddBlock(i * 0.42, 0.12, 2.75, 0.08, 0.1, 0.35);
            }
            metal.AddCylinder(new Point3D(-0.95, 2.45, -1.3), new Point3D(-2.2, 2.65, 0.3), 0.13, 8); // tubo de descarga
            dark.AddCylinder(new Point3D(0.65, 2.7, -1.5), new Point3D(0.65, 3.4, -1.5), 0.08, 6);    // escape

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

            // Molinete del cabezal: gira mientras cosecha
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

            // Polvo/paja detrás mientras trabaja
            MeshBuilder dust = new MeshBuilder();
            dust.AddSphere(new Point3D(0, 0.9, -2.6), 0.9, 6, 4);
            dust.AddSphere(new Point3D(0.4, 0.7, -3.3), 0.6, 6, 4);
            workEffect = dust.ToModel(Materials.Argb(90, 222, 196, 130));

            AddStatusLight(0, 3.42, 0.85);
        }

        /// <summary>Sembradora: tractor azul tirando de una sembradora con tolvas.</summary>
        private void BuildSeeder()
        {
            MeshBuilder body = new MeshBuilder();
            MeshBuilder glass = new MeshBuilder();
            MeshBuilder implement = new MeshBuilder();
            MeshBuilder hoppers = new MeshBuilder();
            MeshBuilder dark = new MeshBuilder();
            MeshBuilder wheels = new MeshBuilder();
            MeshBuilder hubs = new MeshBuilder();

            body.AddBlock(0, 0.55, 0.9, 1.0, 0.85, 1.7);            // capó
            dark.AddBlock(0, 0.55, 1.78, 0.9, 0.6, 0.08);           // rejilla
            body.AddBlock(0, 0.45, -0.35, 1.3, 0.6, 1.2);           // chasis bajo la cabina
            glass.AddBlock(0, 1.05, -0.35, 1.25, 1.15, 1.1);        // cabina
            body.AddBlock(0, 2.2, -0.35, 1.4, 0.12, 1.25);          // techo
            dark.AddCylinder(new Point3D(0.3, 1.4, 1.4), new Point3D(0.3, 2.0, 1.4), 0.06, 6); // escape

            AddWheel(wheels, hubs, 0.95, -0.35, 0.78, 0.45);        // ruedas traseras grandes
            AddWheel(wheels, hubs, -0.95, -0.35, 0.78, 0.45);
            AddWheel(wheels, hubs, 0.7, 1.25, 0.45, 0.3);           // delanteras pequeñas
            AddWheel(wheels, hubs, -0.7, 1.25, 0.45, 0.3);

            dark.AddBlock(0, 0.45, -1.35, 0.2, 0.18, 0.9);          // enganche
            implement.AddBlock(0, 0.35, -2.0, 3.4, 0.25, 0.4);      // barra de la sembradora
            for (int i = -1; i <= 1; i++)
            {
                hoppers.AddBlock(i * 1.05, 0.6, -2.0, 0.75, 0.6, 0.65); // tolvas de semilla
                dark.AddBlock(i * 1.05, 0.05, -2.2, 0.1, 0.35, 0.1);    // rejas
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

            // Semillas cayendo mientras trabaja
            MeshBuilder seeds = new MeshBuilder();
            for (int i = -1; i <= 1; i++)
            {
                seeds.AddBox(i * 1.05, 0.12, -2.6, 0.5, 0.06, 0.6);
            }
            workEffect = seeds.ToModel(Materials.Argb(160, 120, 84, 50));

            AddStatusLight(0, 2.36, -0.35);
        }

        /// <summary>Regadora: vehículo con depósito de agua y barra de riego trasera.</summary>
        private void BuildIrrigator()
        {
            MeshBuilder chassis = new MeshBuilder();
            MeshBuilder body = new MeshBuilder();
            MeshBuilder glass = new MeshBuilder();
            MeshBuilder tank = new MeshBuilder();
            MeshBuilder metal = new MeshBuilder();
            MeshBuilder wheels = new MeshBuilder();
            MeshBuilder hubs = new MeshBuilder();

            chassis.AddBlock(0, 0.55, -0.2, 1.5, 0.35, 3.8);        // chasis
            body.AddBlock(0, 0.9, 1.25, 1.45, 0.4, 1.0);            // base de la cabina
            glass.AddBlock(0, 1.3, 1.25, 1.35, 0.95, 0.95);         // cabina
            body.AddBlock(0, 2.25, 1.25, 1.5, 0.12, 1.1);           // techo
            tank.AddCylinder(new Point3D(0, 1.6, -1.8), new Point3D(0, 1.6, 0.55), 0.78, 12); // depósito
            body.AddCylinder(new Point3D(0, 1.6, -1.2), new Point3D(0, 1.6, -1.05), 0.82, 12); // aros
            body.AddCylinder(new Point3D(0, 1.6, 0.0), new Point3D(0, 1.6, 0.15), 0.82, 12);
            metal.AddCylinder(new Point3D(0, 2.35, -0.5), new Point3D(0, 2.5, -0.5), 0.25, 8); // tapa

            // Barra de riego
            metal.AddCylinder(new Point3D(-2.6, 1.0, -2.25), new Point3D(2.6, 1.0, -2.25), 0.08, 6);
            metal.AddBlock(0, 0.7, -2.1, 0.25, 0.5, 0.3);
            for (int i = -5; i <= 5; i++)
            {
                metal.AddBlock(i * 0.5, 0.82, -2.25, 0.06, 0.18, 0.06); // boquillas
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

            // Cortina de agua mientras riega
            MeshBuilder water = new MeshBuilder();
            water.AddBlock(0, 0.05, -2.3, 5.0, 0.75, 0.5);
            workEffect = water.ToModel(Materials.Argb(80, 90, 170, 255));

            AddStatusLight(0, 2.41, 1.25);
        }

        /// <summary>Dron fumigador de 4 rotores.</summary>
        private void BuildDrone()
        {
            const double lift = 0.75; // el modelo se sube para que los patines queden en y = 0

            MeshBuilder shell = new MeshBuilder();
            MeshBuilder dark = new MeshBuilder();
            MeshBuilder tank = new MeshBuilder();
            MeshBuilder accent = new MeshBuilder();

            shell.AddBox(0, lift, 0, 0.9, 0.35, 1.15);                 // cuerpo
            dark.AddBox(0, lift + 0.23, 0, 0.6, 0.12, 0.75);           // tapa superior
            accent.AddBox(0, lift, 0.6, 0.5, 0.18, 0.06);              // frontal (para ver hacia dónde mira)
            tank.AddCylinder(new Point3D(0, lift - 0.55, 0), new Point3D(0, lift - 0.17, 0), 0.27, 8); // depósito
            dark.AddCylinder(new Point3D(-0.95, lift - 0.6, 0), new Point3D(0.95, lift - 0.6, 0), 0.04, 6); // barra
            // patas y patines
            dark.AddBox(0.35, lift - 0.45, 0, 0.06, 0.6, 0.06);
            dark.AddBox(-0.35, lift - 0.45, 0, 0.06, 0.6, 0.06);
            dark.AddCylinder(new Point3D(0.35, 0.04, -0.5), new Point3D(0.35, 0.04, 0.5), 0.04, 6);
            dark.AddCylinder(new Point3D(-0.35, 0.04, -0.5), new Point3D(-0.35, 0.04, 0.5), 0.04, 6);

            double[] armX = { 1.0, -1.0, -1.0, 1.0 };
            double[] armZ = { 1.0, 1.0, -1.0, -1.0 };
            for (int i = 0; i < 4; i++)
            {
                Point3D end = new Point3D(armX[i], lift + 0.1, armZ[i]);
                dark.AddCylinder(new Point3D(0, lift + 0.05, 0), end, 0.06, 6);                       // brazo
                dark.AddCylinder(new Point3D(armX[i], lift, armZ[i]), new Point3D(armX[i], lift + 0.3, armZ[i]), 0.13, 8); // motor

                // Rotor: modelo propio para poder girarlo
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
                // los discos semitransparentes se añaden al final (ver más abajo)
                transparentParts.Add(discModel);
            }

            AddPart(shell, Materials.Rgb(238, 240, 242));
            AddPart(dark, Materials.Rgb(52, 56, 62));
            AddPart(tank, Materials.Rgb(92, 170, 96));
            AddPart(accent, Materials.Rgb(235, 90, 60));

            foreach (GeometryModel3D part in transparentParts)
            {
                group.Children.Add(part); // lo transparente se dibuja después de lo opaco
            }

            // Nube de producto fitosanitario mientras fumiga
            MeshBuilder spray = new MeshBuilder();
            spray.AddCone(new Point3D(0, lift - 2.4, 0), new Point3D(0, lift - 0.6, 0), 1.2, 10);
            workEffect = spray.ToModel(Materials.Argb(70, 170, 230, 150));

            AddStatusLight(0, lift + 0.33, -0.4);
        }

        private readonly List<GeometryModel3D> transparentParts = new List<GeometryModel3D>();
    }
}
