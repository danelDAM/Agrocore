using System;
using System.Threading;
using AgroCore.Server.Synchronization;

namespace AgroCore.Server.Fields
{
    /// <summary>
    /// Ustiategi guztietako laboreak hazarazten dituen haria.
    /// 500 ms-ro hazkuntza aurreratzen du, igarotako denbora errealaren arabera.
    /// 10 s-ro soroen egoera eta estatistikak diskoan gordetzen ditu.
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
                        Console.WriteLine("Ezin izan da egoera gorde: " + ex.Message);
                    }
                }
            }
        }
    }
}
