using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Map;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;

namespace AgroCore.ControlCenter.Scene
{
    /// <summary>
    /// Ustiategiaren 3D ikuspegia (WPFren Viewport3D, kanpoko liburutegirik gabe).
    ///
    ///  - Agertoki finkoa: lurra, bideak, basea, kargagailuak, ukuilua, siloa eta zuhaitzak.
    ///  - Soroak (FieldVisual), laborearekin aldatzen direnak.
    ///  - Makinak (MachineVisual), UDP telemetriarekin mugitzen direnak.
    ///  - 2D etiketak makina eta soro bakoitzaren gainean.
    ///  - Kamera orbitala: ezkerreko botoiarekin arrastatu = biratu, eskuineko botoia = desplazatu,
    ///    gurpila = hurbildu/urrundu, makina baten gainean klik = hautatu.
    /// </summary>
    public class FarmScene
    {
        private readonly Viewport3D viewport;
        private readonly Canvas labels;
        private readonly PerspectiveCamera camera = new PerspectiveCamera();

        private readonly ModelVisual3D world = new ModelVisual3D();
        private readonly ModelVisual3D zonesVisual = new ModelVisual3D();
        private readonly Dictionary<string, MachineVisual> machines = new Dictionary<string, MachineVisual>();
        private readonly Dictionary<string, Border> machineLabels = new Dictionary<string, Border>();
        private readonly List<FieldVisual> fields = new List<FieldVisual>();
        private readonly List<Border> fieldLabels = new List<Border>();
        private readonly List<SolidColorBrush> chargerLights = new List<SolidColorBrush>();

        private FarmLayout layout;
        private int farmId = -1;
        private string layoutKey = "";
        private string selectedId;
        private DateTime lastFrame = DateTime.Now;

        // Kamera orbitala
        private Point3D target = new Point3D(0, 0, 0);
        private double yaw = 35;
        private double pitch = 48;
        private double distance = 90;
        private Point lastMouse;
        private bool rotating = false;
        private bool panning = false;
        private bool moved = false;

        public bool ShowZones { get; set; }
        public bool FollowSelected { get; set; }
        public bool ShowLabels { get; set; } = true;

        /// <summary>Erabiltzaileak makina baten gainean klik egin du (null = klik hutsean).</summary>
        public event Action<string> MachineClicked;

        public FarmScene(Viewport3D viewport, Canvas labels, FrameworkElement mouseArea)
        {
            this.viewport = viewport;
            this.labels = labels;

            camera.FieldOfView = 45;
            camera.NearPlaneDistance = 0.5;
            camera.FarPlaneDistance = 1500;
            viewport.Camera = camera;

            // Argiak: giro-argia + eguzkia + betegarri-argia
            ModelVisual3D lights = new ModelVisual3D();
            Model3DGroup lightGroup = new Model3DGroup();
            lightGroup.Children.Add(new AmbientLight(Color.FromRgb(105, 105, 110)));
            lightGroup.Children.Add(new DirectionalLight(Color.FromRgb(255, 246, 228), new Vector3D(-0.45, -1, -0.3)));
            lightGroup.Children.Add(new DirectionalLight(Color.FromRgb(70, 80, 95), new Vector3D(0.5, -0.4, 0.6)));
            lights.Content = lightGroup;
            viewport.Children.Add(lights);
            viewport.Children.Add(world);

            mouseArea.MouseLeftButtonDown += OnMouseLeftDown;
            mouseArea.MouseLeftButtonUp += OnMouseLeftUp;
            mouseArea.MouseRightButtonDown += OnMouseRightDown;
            mouseArea.MouseRightButtonUp += OnMouseRightUp;
            mouseArea.MouseMove += OnMouseMove;
            mouseArea.MouseWheel += OnMouseWheel;

            CompositionTarget.Rendering += OnFrame;
            UpdateCamera();
        }

        /// <summary>Leihoa ixtean deitu behar da, animatzeari uzteko.</summary>
        public void Dispose()
        {
            CompositionTarget.Rendering -= OnFrame;
        }

        // ==================================================================
        // EGOERA OSOA (FARM_STATE, segundoro)
        // ==================================================================

        public void UpdateState(FarmStateMessage state)
        {
            string key = state.FarmId + "|" + state.FieldCount + "|" + state.ChargerCount + "|" + state.Machines.Count;
            if (key != layoutKey)
            {
                // Beste ustiategi bat (edo bere tamaina aldatu da): dena berriro eraikitzen da
                bool newFarm = state.FarmId != farmId;
                layoutKey = key;
                farmId = state.FarmId;
                layout = new FarmLayout(state.FieldCount, state.ChargerCount, state.Machines.Count);
                BuildWorld();
                if (newFarm)
                {
                    ResetCamera();
                }
            }

            UpdateFields(state);
            UpdateChargers(state.Chargers);
            UpdateMachines(state.Machines);
            UpdateZones(state.Zones);
        }

        private void UpdateFields(FarmStateMessage state)
        {
            foreach (Field field in state.Fields)
            {
                if (field.Index < 0 || field.Index >= fields.Count)
                {
                    continue;
                }

                // Soro honetan makinaren bat lanean ari al da? Bere aurrerapenak zenbat dagoen eginda adierazten du
                double progress = -1;
                foreach (Machine machine in state.Machines)
                {
                    if (machine.CurrentFieldId == field.Id && machine.Status == AgroCore.Shared.Enums.MachineStatus.Working)
                    {
                        progress = machine.TaskProgress;
                    }
                }
                fields[field.Index].Update(field, progress);

                TextBlock text = (TextBlock)fieldLabels[field.Index].Child;
                text.Text = field.Name + " · " + Texts.Crop(field.Crop ?? field.PlannedCrop) + " · " + field.Growth.ToString("0") + "%";
            }
        }

        private void UpdateChargers(List<Charger> chargers)
        {
            foreach (Charger charger in chargers)
            {
                if (charger.Index < chargerLights.Count)
                {
                    chargerLights[charger.Index].Color = charger.OccupiedBy == null
                        ? Color.FromRgb(80, 220, 110)    // librea: berdea
                        : Color.FromRgb(255, 170, 40);   // okupatua: laranja
                }
            }
        }

        private void UpdateMachines(List<Machine> list)
        {
            HashSet<string> present = new HashSet<string>();
            foreach (Machine machine in list)
            {
                present.Add(machine.Id);
                MachineVisual visual;
                if (!machines.TryGetValue(machine.Id, out visual))
                {
                    visual = new MachineVisual(machine.Id, machine.Type);
                    machines[machine.Id] = visual;
                    viewport.Children.Add(visual.Visual);
                    if (visual.Shadow != null)
                    {
                        viewport.Children.Add(visual.Shadow);
                    }
                    visual.SetSelected(machine.Id == selectedId);

                    Border label = CreateLabel(machine.Name, true);
                    machineLabels[machine.Id] = label;
                    labels.Children.Add(label);
                }

                visual.SetTarget(machine.Position.X, machine.Position.Y, machine.Position.Z, machine.Heading);
                visual.SetStatus(machine.Status, machine.Online);
                visual.Battery = machine.Battery;
                UpdateMachineLabel(machine.Id, machine.Name, machine.Battery, machine.Online);
            }

            // Jada existitzen ez diren makinak (adib. ustiategiz aldatu da)
            List<string> removed = new List<string>();
            foreach (string id in machines.Keys)
            {
                if (!present.Contains(id))
                {
                    removed.Add(id);
                }
            }
            foreach (string id in removed)
            {
                viewport.Children.Remove(machines[id].Visual);
                if (machines[id].Shadow != null)
                {
                    viewport.Children.Remove(machines[id].Shadow);
                }
                machines.Remove(id);
                labels.Children.Remove(machineLabels[id]);
                machineLabels.Remove(id);
            }
        }

        private void UpdateZones(List<ZoneReservation> zones)
        {
            if (!ShowZones || layout == null)
            {
                zonesVisual.Content = null;
                return;
            }

            MeshBuilder builder = new MeshBuilder();
            foreach (ZoneReservation zone in zones)
            {
                int col, row;
                if (FarmLayout.TryParseZone(zone.ZoneId, out col, out row))
                {
                    Position center = layout.CellCenter(col, row);
                    builder.AddBlock(center.X, 0.06, center.Z, FarmLayout.CellSize - 0.4, 0.25, FarmLayout.CellSize - 0.4);
                }
            }
            zonesVisual.Content = builder.IsEmpty ? null : builder.ToModel(Materials.Argb(90, 255, 150, 40));
        }

        // ==================================================================
        // TELEMETRIA (UDP, 200 ms-ro)
        // ==================================================================

        public void ApplyTelemetry(Telemetry telemetry)
        {
            MachineVisual visual;
            if (machines.TryGetValue(telemetry.MachineId, out visual))
            {
                visual.SetTarget(telemetry.X, telemetry.Y, telemetry.Z, telemetry.Heading);
                visual.SetStatus(telemetry.Status, true);
                visual.Battery = telemetry.Battery;
            }
        }

        // ==================================================================
        // HAUTAPENA
        // ==================================================================

        public void Select(string machineId)
        {
            selectedId = machineId;
            foreach (MachineVisual visual in machines.Values)
            {
                visual.SetSelected(visual.MachineId == machineId);
            }
            foreach (KeyValuePair<string, Border> pair in machineLabels)
            {
                pair.Value.BorderBrush = pair.Key == machineId ? Brushes.Gold : Brushes.Transparent;
            }
        }

        public void FocusOn(string machineId)
        {
            MachineVisual visual;
            if (machines.TryGetValue(machineId, out visual))
            {
                target = new Point3D(visual.CurrentPosition.X, 0, visual.CurrentPosition.Z);
                distance = Math.Min(distance, 45);
                UpdateCamera();
            }
        }

        // ==================================================================
        // AGERTOKI FINKOA
        // ==================================================================

        private void BuildWorld()
        {
            Model3DGroup group = new Model3DGroup();
            double cs = FarmLayout.CellSize;
            double width = layout.Width;
            double depth = layout.Depth;

            // Belarra inguruan
            MeshBuilder grass = new MeshBuilder();
            grass.AddBlock(width / 2, -0.3, depth / 2 - 6, width + 400, 0.3, depth + 400);
            group.Children.Add(grass.ToModel(Materials.Rgb(104, 158, 70)));

            // Basearen zelaigunea (0. eta 1. zutabeak)
            MeshBuilder pad = new MeshBuilder();
            pad.AddBlock(cs, 0, depth / 2, 2 * cs, 0.04, depth);
            group.Children.Add(pad.ToModel(Materials.Rgb(170, 166, 154)));

            // Bideak
            MeshBuilder roads = new MeshBuilder();
            MeshBuilder lines = new MeshBuilder();
            for (int col = 0; col < layout.Cols; col++)
            {
                for (int row = 0; row < layout.Rows; row++)
                {
                    if (layout.IsRoad(col, row) || layout.IsBaseLane(col, row))
                    {
                        Position c = layout.CellCenter(col, row);
                        roads.AddBlock(c.X, 0, c.Z, cs, 0.06, cs);
                    }
                }
            }
            group.Children.Add(roads.ToModel(Materials.Rgb(84, 86, 90)));

            // Aparkalekuak (lerro zuriak) eta kargagailuak
            for (int slot = 0; slot < layout.MachineSlotCount; slot++)
            {
                Position p = layout.ParkingPosition(slot);
                lines.AddBlock(p.X, 0.04, p.Z - 1.6, 3.4, 0.02, 0.12);
            }
            group.Children.Add(lines.ToModel(Materials.Rgb(235, 235, 230)));

            chargerLights.Clear();
            MeshBuilder chargerBody = new MeshBuilder();
            MeshBuilder chargerPad = new MeshBuilder();
            MeshBuilder chargerScreen = new MeshBuilder();
            for (int i = 0; i < layout.ChargerCount; i++)
            {
                Position p = layout.ChargerPosition(i);
                chargerPad.AddBlock(p.X, 0.04, p.Z, 2.8, 0.04, 2.6);
                chargerBody.AddBlock(0.7, 0.04, p.Z, 0.7, 1.9, 1.0);
                chargerScreen.AddBox(1.06, 1.35, p.Z, 0.02, 0.45, 0.6);

                SolidColorBrush light = new SolidColorBrush(Color.FromRgb(80, 220, 110));
                chargerLights.Add(light);
                MeshBuilder lamp = new MeshBuilder();
                lamp.AddBlock(0.7, 1.94, p.Z, 0.5, 0.18, 0.7);
                group.Children.Add(new GeometryModel3D(lamp.ToMesh(), Materials.Glow(light)));
            }
            group.Children.Add(chargerPad.ToModel(Materials.Rgb(60, 90, 130)));
            group.Children.Add(chargerBody.ToModel(Materials.Rgb(225, 228, 230)));
            group.Children.Add(chargerScreen.ToModel(Materials.Rgb(40, 60, 80)));

            AddFarmBuildings(group);
            AddTrees(group, width, depth);

            world.Content = group;

            // Soroak
            foreach (FieldVisual field in fields)
            {
                viewport.Children.Remove(field.Visual);
            }
            foreach (Border label in fieldLabels)
            {
                labels.Children.Remove(label);
            }
            fields.Clear();
            fieldLabels.Clear();
            for (int i = 0; i < layout.FieldCount; i++)
            {
                FieldVisual field = new FieldVisual(layout, i);
                fields.Add(field);
                viewport.Children.Add(field.Visual);

                Border label = CreateLabel(FarmLayout.FieldName(i), false);
                fieldLabels.Add(label);
                labels.Children.Add(label);
            }

            // Gune erreserbatuak gainerako guztiaren gainean
            viewport.Children.Remove(zonesVisual);
            viewport.Children.Add(zonesVisual);
        }

        private void AddFarmBuildings(Model3DGroup group)
        {
            // Ukuilua (basearen atzean)
            MeshBuilder barn = new MeshBuilder();
            barn.AddBlock(6, 0, -11, 8, 4, 7);
            group.Children.Add(barn.ToModel(Materials.Rgb(176, 52, 42)));

            MeshBuilder roof = new MeshBuilder();
            roof.AddRoof(6, 4, -11, 8.8, 2.8, 7.8);
            group.Children.Add(roof.ToModel(Materials.Rgb(76, 66, 64)));

            MeshBuilder trim = new MeshBuilder();
            trim.AddBlock(6, 0, -7.45, 3.0, 3.0, 0.1);   // atea
            group.Children.Add(trim.ToModel(Materials.Rgb(236, 232, 222)));
            MeshBuilder door = new MeshBuilder();
            door.AddBlock(6, 0.1, -7.38, 2.6, 2.7, 0.1);
            group.Children.Add(door.ToModel(Materials.Rgb(120, 36, 30)));

            // Siloa
            MeshBuilder silo = new MeshBuilder();
            silo.AddCylinder(new Point3D(16, 0, -11), new Point3D(16, 9, -11), 2.4, 14);
            group.Children.Add(silo.ToModel(Materials.Rgb(196, 198, 204)));
            MeshBuilder siloTop = new MeshBuilder();
            siloTop.AddCone(new Point3D(16, 9, -11), new Point3D(16, 11, -11), 2.6, 14);
            group.Children.Add(siloTop.ToModel(Materials.Rgb(150, 154, 160)));
        }

        private void AddTrees(Model3DGroup group, double width, double depth)
        {
            MeshBuilder trunks = new MeshBuilder();
            MeshBuilder crowns = new MeshBuilder();
            MeshBuilder crownsLight = new MeshBuilder();
            Random random = new Random(11);

            for (int i = 0; i < 46; i++)
            {
                double tx, tz;
                int side = i % 4;
                if (side == 0) { tx = -6 - random.NextDouble() * 22; tz = random.NextDouble() * (depth + 20) - 10; }
                else if (side == 1) { tx = width + 5 + random.NextDouble() * 22; tz = random.NextDouble() * (depth + 20) - 10; }
                else if (side == 2) { tx = 24 + random.NextDouble() * (width - 10); tz = -8 - random.NextDouble() * 22; }
                else { tx = random.NextDouble() * width; tz = depth + 5 + random.NextDouble() * 20; }

                double h = 2.5 + random.NextDouble() * 2.5;
                trunks.AddCylinder(new Point3D(tx, 0, tz), new Point3D(tx, h * 0.45, tz), 0.25, 6);
                if (i % 3 == 0)
                {
                    crownsLight.AddSphere(new Point3D(tx, h * 0.45 + h * 0.35, tz), h * 0.42, 7, 4);
                }
                else
                {
                    crowns.AddCone(new Point3D(tx, h * 0.3, tz), new Point3D(tx, h * 1.5, tz), h * 0.38, 7);
                }
            }
            group.Children.Add(trunks.ToModel(Materials.Rgb(110, 76, 48)));
            group.Children.Add(crowns.ToModel(Materials.Rgb(52, 112, 58)));
            group.Children.Add(crownsLight.ToModel(Materials.Rgb(86, 146, 64)));
        }

        // ==================================================================
        // 2D ETIKETAK
        // ==================================================================

        private Border CreateLabel(string text, bool machine)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = machine ? 11 : 12;
            block.FontWeight = machine ? FontWeights.SemiBold : FontWeights.Bold;
            block.Foreground = Brushes.White;

            Border border = new Border();
            border.Child = block;
            border.Padding = new Thickness(6, 2, 6, 2);
            border.CornerRadius = new CornerRadius(4);
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = Brushes.Transparent;
            border.Background = machine
                ? new SolidColorBrush(Color.FromArgb(190, 20, 28, 24))
                : new SolidColorBrush(Color.FromArgb(150, 70, 45, 25));
            border.IsHitTestVisible = false;
            return border;
        }

        private void UpdateMachineLabel(string id, string name, double battery, bool online)
        {
            Border label;
            if (machineLabels.TryGetValue(id, out label))
            {
                ((TextBlock)label.Child).Text = online ? name + "  " + battery.ToString("0") + "%" : name + " (deskonektatuta)";
                label.Opacity = online ? 1.0 : 0.55;
            }
        }

        private void PlaceLabels()
        {
            labels.Visibility = ShowLabels ? Visibility.Visible : Visibility.Collapsed;
            if (!ShowLabels || layout == null)
            {
                return;
            }

            foreach (MachineVisual visual in machines.Values)
            {
                Border label;
                if (machineLabels.TryGetValue(visual.MachineId, out label))
                {
                    PlaceLabel(label, visual.Visual, visual.LabelPoint);
                }
            }
            for (int i = 0; i < fields.Count; i++)
            {
                Position center = layout.FieldCenter(i);
                PlaceLabel(fieldLabels[i], world, new Point3D(center.X, 0.4, layout.FieldMinZ(i) + 1.5));
            }
        }

        private void PlaceLabel(Border label, Visual3D reference, Point3D worldPoint)
        {
            // Munduko 3D puntu bat pantailako 2D puntu bihurtzen du
            GeneralTransform3DTo2D transform = world.TransformToAncestor(viewport);
            Point screen;
            if (transform != null && transform.TryTransform(worldPoint, out screen) && IsInFront(worldPoint))
            {
                label.Visibility = Visibility.Visible;
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, screen.X - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, screen.Y - label.DesiredSize.Height);
            }
            else
            {
                label.Visibility = Visibility.Collapsed;
            }
        }

        private bool IsInFront(Point3D point)
        {
            Vector3D toPoint = point - camera.Position;
            return Vector3D.DotProduct(toPoint, camera.LookDirection) > 0;
        }

        // ==================================================================
        // ANIMAZIOA
        // ==================================================================

        private void OnFrame(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            double dt = (now - lastFrame).TotalSeconds;
            lastFrame = now;
            if (dt > 0.2)
            {
                dt = 0.2;
            }

            foreach (MachineVisual visual in machines.Values)
            {
                visual.Animate(dt);
            }

            if (FollowSelected && selectedId != null && machines.ContainsKey(selectedId))
            {
                Point3D p = machines[selectedId].CurrentPosition;
                target = new Point3D(target.X + (p.X - target.X) * 0.1, 0, target.Z + (p.Z - target.Z) * 0.1);
                UpdateCamera();
            }

            PlaceLabels();
        }

        // ==================================================================
        // KAMERA
        // ==================================================================

        public void ResetCamera()
        {
            if (layout == null)
            {
                return;
            }
            target = new Point3D(layout.Width / 2, 0, layout.Depth / 2);
            yaw = 30;
            pitch = 50;
            distance = Math.Max(layout.Width, layout.Depth) * 1.4;
            UpdateCamera();
        }

        public void TopView()
        {
            if (layout == null)
            {
                return;
            }
            target = new Point3D(layout.Width / 2, 0, layout.Depth / 2);
            yaw = 0;
            pitch = 88;
            distance = Math.Max(layout.Width, layout.Depth) * 1.3;
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            double yawRad = yaw * Math.PI / 180;
            double pitchRad = pitch * Math.PI / 180;
            Vector3D offset = new Vector3D(
                distance * Math.Cos(pitchRad) * Math.Sin(yawRad),
                distance * Math.Sin(pitchRad),
                distance * Math.Cos(pitchRad) * Math.Cos(yawRad));
            camera.Position = target + offset;
            camera.LookDirection = -offset;
            camera.UpDirection = new Vector3D(0, 1, 0);
        }

        private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            rotating = true;
            moved = false;
            lastMouse = e.GetPosition(viewport);
            ((UIElement)sender).CaptureMouse();
        }

        private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            rotating = false;
            ((UIElement)sender).ReleaseMouseCapture();
            if (!moved)
            {
                PickMachine(e.GetPosition(viewport));
            }
        }

        private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            panning = true;
            lastMouse = e.GetPosition(viewport);
            ((UIElement)sender).CaptureMouse();
        }

        private void OnMouseRightUp(object sender, MouseButtonEventArgs e)
        {
            panning = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point position = e.GetPosition(viewport);
            Vector delta = position - lastMouse;
            lastMouse = position;

            if (rotating)
            {
                if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 1)
                {
                    moved = true;
                }
                yaw -= delta.X * 0.4;
                pitch = Math.Max(12, Math.Min(89, pitch + delta.Y * 0.3));
                UpdateCamera();
            }
            else if (panning)
            {
                double yawRad = yaw * Math.PI / 180;
                double scale = distance * 0.0018;
                // kameraren eskuina eta "aurrerantz" lurraren planoan
                Vector3D right = new Vector3D(Math.Cos(yawRad), 0, -Math.Sin(yawRad));
                Vector3D forward = new Vector3D(-Math.Sin(yawRad), 0, -Math.Cos(yawRad));
                target = target - right * (delta.X * scale) + forward * (delta.Y * scale);
                FollowSelected = false;
                UpdateCamera();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            distance = Math.Max(12, Math.Min(260, distance * (e.Delta > 0 ? 0.88 : 1.13)));
            UpdateCamera();
        }

        private void PickMachine(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(viewport, point);
            RayHitTestResult ray = result as RayHitTestResult;
            string clicked = null;
            if (ray != null)
            {
                foreach (MachineVisual visual in machines.Values)
                {
                    if (ray.VisualHit == visual.Visual)
                    {
                        clicked = visual.MachineId;
                    }
                }
            }
            if (MachineClicked != null)
            {
                MachineClicked(clicked);
            }
        }
    }
}
