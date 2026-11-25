using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Models;

namespace AgroCore.ControlCenter.Scene
{
    /// <summary>
    /// Soro bat 3Dn: ildoak dituen lurra eta landareak.
    /// Landareak laborearen eta hazkuntzaren arabera aldatzen dira (tamaina, kolorea, fruituak).
    /// Ereiten edo uzta biltzen ari den bitartean, landutako zatia bakarrik marrazten da.
    ///
    /// Sarea segundoko 60 aldiz berriro ez kalkulatzeko, ikusten den zerbait aldatzen denean
    /// bakarrik berreraikitzen da (egoera, laborea, hazkuntza %5eko jauzietan, aurrerapena %10eko jauzietan).
    /// </summary>
    public class FieldVisual
    {
        private const int PlantsPerSide = 6;

        public ModelVisual3D Visual { get; private set; }

        private readonly FarmLayout layout;
        private readonly int index;
        private string lastKey = "";

        public FieldVisual(FarmLayout layout, int index)
        {
            this.layout = layout;
            this.index = index;
            Visual = new ModelVisual3D();
        }

        /// <summary>Soroa eguneratzen du. workProgress = bertan lanean ari den makinaren aurrerapena (0-100), edo -1.</summary>
        public void Update(Field field, double workProgress)
        {
            int growthStep = (int)(field.Growth / 5);
            int progressStep = workProgress < 0 ? -1 : (int)(workProgress / 10);
            string key = field.Status + "|" + field.Crop + "|" + growthStep + "|" + progressStep;
            if (key == lastKey)
            {
                return;
            }
            lastKey = key;
            Rebuild(field, workProgress);
        }

        private void Rebuild(Field field, double workProgress)
        {
            Model3DGroup group = new Model3DGroup();
            double minX = layout.FieldMinX(index);
            double minZ = layout.FieldMinZ(index);
            double size = layout.FieldSize;
            double cx = minX + size / 2;
            double cz = minZ + size / 2;

            // ---- Lurra eta ildoak ----
            Color soil = Materials.Rgb(150, 112, 74);          // lur lehorra (soro hutsa)
            Color furrow = Materials.Rgb(124, 90, 58);
            if (field.Status != FieldStatus.Empty && field.Status != FieldStatus.Harvested)
            {
                soil = Materials.Rgb(112, 78, 50);              // landutako lurra
                furrow = Materials.Rgb(88, 60, 38);
            }

            MeshBuilder ground = new MeshBuilder();
            ground.AddBlock(cx, 0, cz, size, 0.12, size);
            group.Children.Add(ground.ToModel(soil));

            MeshBuilder furrows = new MeshBuilder();
            for (int i = 0; i < PlantsPerSide; i++)
            {
                double fx = minX + (i + 0.5) * size / PlantsPerSide;
                furrows.AddBlock(fx, 0.12, cz, 0.9, 0.08, size - 0.8);
            }
            group.Children.Add(furrows.ToModel(furrow));

            // ---- Landareak ----
            if (field.Crop.HasValue && field.Status != FieldStatus.Empty)
            {
                AddPlants(group, field, workProgress, minX, minZ, size);
            }

            Visual.Content = group;
        }

        private void AddPlants(Model3DGroup group, Field field, double workProgress, double minX, double minZ, double size)
        {
            CropType crop = field.Crop.Value;
            bool ready = field.Status == FieldStatus.ReadyToHarvest || field.Status == FieldStatus.Harvesting;
            bool harvested = field.Status == FieldStatus.Harvested;
            double s = Math.Max(0.1, Math.Min(1.0, field.Growth / 100.0));

            // Landareen zein zutabe marraztu behar diren (ereitea/uzta-bilketa martxan)
            int columnsDone = PlantsPerSide;
            if (workProgress >= 0 && (field.Status == FieldStatus.Sowing || field.Status == FieldStatus.Harvesting))
            {
                columnsDone = (int)Math.Round(PlantsPerSide * workProgress / 100.0);
            }
            if (field.Status == FieldStatus.Sowing && workProgress < 0)
            {
                columnsDone = 0;
            }

            MeshBuilder leaves = new MeshBuilder();
            MeshBuilder stems = new MeshBuilder();
            MeshBuilder fruits = new MeshBuilder();
            MeshBuilder stubble = new MeshBuilder();
            Random random = new Random(index * 100 + 7); // beti "nahaste" bera

            for (int i = 0; i < PlantsPerSide; i++)
            {
                for (int j = 0; j < PlantsPerSide; j++)
                {
                    double px = minX + (i + 0.5) * size / PlantsPerSide + (random.NextDouble() - 0.5) * 0.5;
                    double pz = minZ + (j + 0.5) * size / PlantsPerSide + (random.NextDouble() - 0.5) * 0.5;

                    if (harvested)
                    {
                        stubble.AddBlock(px, 0.12, pz, 0.35, 0.12, 0.35);
                        continue;
                    }

                    bool columnWorked = i < columnsDone;
                    if (field.Status == FieldStatus.Harvesting && columnWorked)
                    {
                        stubble.AddBlock(px, 0.12, pz, 0.35, 0.12, 0.35); // uzta bilduta jada
                        continue;
                    }
                    if (field.Status == FieldStatus.Sowing)
                    {
                        if (columnWorked)
                        {
                            leaves.AddBlock(px, 0.12, pz, 0.15, 0.12, 0.15); // erein berri den kimua
                        }
                        continue;
                    }

                    switch (crop)
                    {
                        case CropType.Carrot: AddCarrot(leaves, fruits, px, pz, s, ready); break;
                        case CropType.Corn: AddCorn(leaves, stems, fruits, px, pz, s, ready); break;
                        case CropType.Tomato: AddTomato(leaves, fruits, px, pz, s, ready, field.Growth); break;
                    }
                }
            }

            Color leafColor = Materials.Rgb(84, 168, 62);
            Color stemColor = Materials.Rgb(110, 160, 60);
            Color fruitColor = Materials.Rgb(236, 120, 32);
            if (crop == CropType.Corn)
            {
                leafColor = ready ? Materials.Rgb(170, 170, 72) : Materials.Rgb(96, 160, 56);
                stemColor = ready ? Materials.Rgb(186, 176, 84) : Materials.Rgb(110, 160, 60);
                fruitColor = Materials.Rgb(244, 204, 64);
            }
            if (crop == CropType.Tomato)
            {
                leafColor = Materials.Rgb(58, 138, 52);
                fruitColor = ready ? Materials.Rgb(222, 52, 40) : Materials.Rgb(168, 186, 64);
            }

            if (!leaves.IsEmpty) group.Children.Add(leaves.ToModel(leafColor));
            if (!stems.IsEmpty) group.Children.Add(stems.ToModel(stemColor));
            if (!fruits.IsEmpty) group.Children.Add(fruits.ToModel(fruitColor));
            if (!stubble.IsEmpty) group.Children.Add(stubble.ToModel(Materials.Rgb(196, 170, 104)));
        }

        // ---------------- Low-poly landareak ----------------

        private static void AddCarrot(MeshBuilder leaves, MeshBuilder fruits, double px, double pz, double s, bool ready)
        {
            double h = 0.2 + 1.0 * s;
            leaves.AddCone(new Point3D(px, 0.12, pz), new Point3D(px, 0.12 + h, pz), 0.14, 5);
            leaves.AddCone(new Point3D(px + 0.12, 0.12, pz), new Point3D(px + 0.35, 0.12 + h * 0.8, pz + 0.1), 0.1, 5);
            leaves.AddCone(new Point3D(px - 0.12, 0.12, pz), new Point3D(px - 0.3, 0.12 + h * 0.75, pz - 0.15), 0.1, 5);
            if (ready)
            {
                fruits.AddCone(new Point3D(px, 0.28, pz), new Point3D(px, 0.02, pz), 0.2, 6); // azenarioa agerian
            }
        }

        private static void AddCorn(MeshBuilder leaves, MeshBuilder stems, MeshBuilder fruits, double px, double pz, double s, bool ready)
        {
            double h = 0.3 + 2.7 * s;
            stems.AddBlock(px, 0.12, pz, 0.13, h, 0.13);
            leaves.AddCone(new Point3D(px, 0.12 + h * 0.45, pz), new Point3D(px + 0.65 * s + 0.1, 0.12 + h * 0.7, pz), 0.07, 4);
            leaves.AddCone(new Point3D(px, 0.12 + h * 0.6, pz), new Point3D(px - 0.6 * s - 0.1, 0.12 + h * 0.85, pz + 0.1), 0.07, 4);
            leaves.AddCone(new Point3D(px, 0.12 + h * 0.3, pz), new Point3D(px, 0.12 + h * 0.5, pz + 0.55 * s + 0.1), 0.07, 4);
            if (ready || s > 0.75)
            {
                fruits.AddBox(px + 0.16, 0.12 + h * 0.55, pz, 0.15, 0.5, 0.15); // artaburua
            }
        }

        private static void AddTomato(MeshBuilder leaves, MeshBuilder fruits, double px, double pz, double s, bool ready, double growth)
        {
            double r = 0.18 + 0.55 * s;
            leaves.AddSphere(new Point3D(px, 0.12 + r * 0.9, pz), r, 6, 4);
            if (ready || growth > 65)
            {
                fruits.AddSphere(new Point3D(px + r * 0.8, 0.12 + r * 0.8, pz), 0.14, 5, 3);
                fruits.AddSphere(new Point3D(px - r * 0.7, 0.12 + r * 1.1, pz + 0.2), 0.14, 5, 3);
                fruits.AddSphere(new Point3D(px, 0.12 + r * 0.6, pz - r * 0.85), 0.14, 5, 3);
                fruits.AddSphere(new Point3D(px + 0.1, 0.12 + r * 1.2, pz + r * 0.75), 0.14, 5, 3);
            }
        }
    }
}
