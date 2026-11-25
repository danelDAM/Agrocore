using System;
using System.Threading;
using AgroCore.Server.Synchronization;

namespace AgroCore.Server.Fields
{
    /// <summary>
    /// Hilo que hace crecer los cultivos de todas las explotaciones.
    /// Cada 500 ms avanza el crecimiento según el tiempo real transcurrido.
    /// Cada 10 s guarda en disco el estado de los campos y las estadísticas.
    /// </summary>
    public class GrowthService
    {
        private readonly FarmManager farms;
        private Thread thread;
        private volatile bool running;

        public GrowthService(FarmManager farms)
        {
            this.farms = farms;
        }

        public void Start()
        {
            running = true;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "Growth";
            thread.Start();
        }

        public void Stop()
        {
            running = false;
        }

        private void Loop()
        {
            DateTime last = DateTime.Now;
            DateTime lastSave = DateTime.Now;

            while (running)
            {
                Thread.Sleep(500);
                DateTime now = DateTime.Now;
                double seconds = (now - last).TotalSeconds;
                last = now;

                foreach (FarmRuntime farm in farms.GetAll())
                {
                    farm.Tasks.Tick(seconds);
                }

                if ((now - lastSave).TotalSeconds >= 10)
                {
                    lastSave = now;
                    try
                    {
                        farms.SaveAll();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("No se pudo guardar el estado: " + ex.Message);
                    }
                }
            }
        }
    }
}
