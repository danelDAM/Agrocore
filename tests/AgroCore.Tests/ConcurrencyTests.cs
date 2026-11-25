using System.Collections.Generic;
using System.Threading;
using AgroCore.Server;
using AgroCore.Server.Charging;
using AgroCore.Server.Collision;
using AgroCore.Server.Machines;
using AgroCore.Server.Tasks;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using Xunit;

namespace AgroCore.Tests
{
    /// <summary>lock / Monitor.Wait / Monitor.PulseAll bidezko sinkronizazioaren probak.</summary>
    public class ConcurrencyTests
    {
        private static MachineRuntime NewMachine(string name, MachineType type, double battery)
        {
            MachineRegistration registration = new MachineRegistration();
            registration.Id = "F1-" + name;
            registration.Name = name;
            registration.FarmId = 1;
            registration.Type = type;
            MachineRuntime machine = new MachineRuntime(registration, new Position(10, 0, 10));
            machine.MarkConnected(null, "token", new MachineProfile(), battery, new Position(10, 0, 10));
            return machine;
        }

        [Fact]
        public void Zona_Ocupada_EsperaHastaQueSeLibera()
        {
            ServerContext context = TestHelpers.CreateContext();
            ZoneManager zones = new ZoneManager(1, context.Logger);
            MachineRuntime seeder = NewMachine("Seeder-01", MachineType.Seeder, 100);
            MachineRuntime irrigator = NewMachine("Irrigator-01", MachineType.Irrigator, 100);

            Assert.Equal(ZoneOutcome.Granted, zones.Acquire(seeder, "Z03-04"));

            ZoneOutcome irrigatorResult = ZoneOutcome.Cancelled;
            Thread other = new Thread(() => irrigatorResult = zones.Acquire(irrigator, "Z03-04"));
            other.Start();

            // Ureztatzailea blokeatuta dago zain (Monitor.Wait)
            Assert.False(other.Join(500));

            // Ereilea gunetik irteten da -> PulseAll -> ureztatzailea sartzen da
            zones.Release(seeder, "Z03-04");
            Assert.True(other.Join(2000));
            Assert.Equal(ZoneOutcome.Granted, irrigatorResult);
            Assert.Equal(1, zones.ConflictsAvoided);
        }

        [Fact]
        public void Zona_InterbloqueoDetectado()
        {
            ServerContext context = TestHelpers.CreateContext();
            ZoneManager zones = new ZoneManager(1, context.Logger);
            MachineRuntime a = NewMachine("Seeder-01", MachineType.Seeder, 100);
            MachineRuntime b = NewMachine("Harvester-01", MachineType.Harvester, 100);

            zones.Acquire(a, "Z03-01");
            zones.Acquire(b, "Z03-02");

            // A-k B-ren gunea nahi du: zain geratzen da
            Thread waiting = new Thread(() => zones.Acquire(a, "Z03-02"));
            waiting.Start();
            Thread.Sleep(300);

            // B-k A-ren gunea nahi du: ziklo bat litzateke -> ukatu egiten da
            Assert.Equal(ZoneOutcome.DeniedDeadlock, zones.Acquire(b, "Z03-01"));
            Assert.Equal(1, zones.DeadlocksAvoided);

            // B-k bidea uzten du bere gunea askatuz -> A-k jarraitu dezake
            zones.Release(b, "Z03-02");
            Assert.True(waiting.Join(2000));
        }

        [Fact]
        public void Zona_StopCancelaLaEspera()
        {
            ServerContext context = TestHelpers.CreateContext();
            ZoneManager zones = new ZoneManager(1, context.Logger);
            MachineRuntime a = NewMachine("Seeder-01", MachineType.Seeder, 100);
            MachineRuntime b = NewMachine("Seeder-02", MachineType.Seeder, 100);
            zones.Acquire(a, "Z03-01");

            ZoneOutcome result = ZoneOutcome.Granted;
            Thread waiting = new Thread(() => result = zones.Acquire(b, "Z03-01"));
            waiting.Start();
            Thread.Sleep(200);

            b.Halted = true;  // STOP
            zones.WakeAll();  // PulseAll
            Assert.True(waiting.Join(2000));
            Assert.Equal(ZoneOutcome.Cancelled, result);
        }

        [Fact]
        public void Cargadores_ColaFifo_TresCargandoDosEsperando()
        {
            ServerContext context = TestHelpers.CreateContext();
            ChargingStationManager chargers = new ChargingStationManager(1, new FarmLayout(6, 3, 11), context.Logger);

            List<MachineRuntime> machines = new List<MachineRuntime>();
            List<Thread> threads = new List<Thread>();
            List<string> order = new List<string>();
            object orderLock = new object();

            for (int i = 1; i <= 5; i++)
            {
                MachineRuntime machine = NewMachine("Harvester-0" + i, MachineType.Harvester, 10);
                machines.Add(machine);
                Thread t = new Thread(() =>
                {
                    Charger charger;
                    if (chargers.Acquire(machine, out charger) == ChargerOutcome.Granted)
                    {
                        lock (orderLock) { order.Add(machine.Name); }
                    }
                });
                threads.Add(t);
                t.Start();
                Thread.Sleep(100); // ordenan iristen dira
            }

            Thread.Sleep(300);
            lock (orderLock)
            {
                Assert.Equal(3, order.Count); // 3 kargatzen
            }
            Assert.Equal(2, chargers.QueueLength); // 2 zain

            // Kargagailu bat askatzen da: ilarako hurrengoa sartzen da (4.a)
            chargers.Release(machines[0], null);
            Thread.Sleep(300);
            lock (orderLock)
            {
                Assert.Equal(4, order.Count);
                Assert.Equal("Harvester-04", order[3]);
            }

            chargers.Release(machines[1], null);
            foreach (Thread t in threads)
            {
                Assert.True(t.Join(2000));
            }
            Assert.Equal("Harvester-05", order[4]);
        }

        private static TaskCoordinator NewCoordinator(ServerContext context, CropType crop)
        {
            FarmLayout layout = new FarmLayout(1, 1, 4);
            Field field = new Field();
            field.Id = "FIELD-A";
            field.FarmId = 1;
            field.Index = 0;
            field.PlannedCrop = crop;
            List<Field> fields = new List<Field>();
            fields.Add(field);
            return new TaskCoordinator(1, layout, fields, () => context.Config.Current, context.Logger);
        }

        [Fact]
        public void Tareas_RecolectoraEsperaHastaQueElCultivoMadura()
        {
            ServerContext context = TestHelpers.CreateContext();
            TaskCoordinator coordinator = NewCoordinator(context, CropType.Carrot);
            MachineRuntime seeder = NewMachine("Seeder-01", MachineType.Seeder, 100);
            MachineRuntime irrigator = NewMachine("Irrigator-01", MachineType.Irrigator, 100);
            MachineRuntime harvester = NewMachine("Harvester-01", MachineType.Harvester, 100);

            // Uzta-biltzaileak lana eskatzen du: ez dago ezer heldurik -> Monitor.Wait-en blokeatzen da
            TaskAssignment harvestResult = null;
            Thread harvesterThread = new Thread(() => harvestResult = coordinator.WaitForTask(harvester, 0, () => false));
            harvesterThread.Start();
            Assert.False(harvesterThread.Join(300));

            // Erein
            TaskAssignment sow = coordinator.WaitForTask(seeder, 1000, () => false);
            Assert.Equal(AssignmentOutcome.Assigned, sow.Outcome);
            Assert.Equal(TaskType.Sow, sow.TaskType);
            coordinator.StartTask(seeder, sow.TaskId);
            coordinator.CompleteTask(seeder, sow.TaskId);

            // Ureztatzaileak oraindik ezin du (hazkuntza azenarioak behar duen % 35a baino txikiagoa da)
            Assert.Equal(AssignmentOutcome.NoTask, coordinator.WaitForTask(irrigator, 200, () => false).Outcome);

            // Denboraren igarotzea simulatu: hazkuntza % 35ean gelditzen da ureztatzearen zain
            coordinator.Tick(5 * 60);
            TaskAssignment irrigate = coordinator.WaitForTask(irrigator, 1000, () => false);
            Assert.Equal(TaskType.Irrigate, irrigate.TaskType);
            Assert.False(harvesterThread.Join(200)); // oraindik ez dago heldu

            coordinator.StartTask(irrigator, irrigate.TaskId);
            coordinator.CompleteTask(irrigator, irrigate.TaskId);

            // Hazten amaitzen du -> PulseAll -> uzta-biltzailea esnatu eta zeregina jasotzen du
            coordinator.Tick(5 * 60);
            Assert.True(harvesterThread.Join(3000));
            Assert.Equal(AssignmentOutcome.Assigned, harvestResult.Outcome);
            Assert.Equal(TaskType.Harvest, harvestResult.TaskType);
        }

        [Fact]
        public void Tareas_SeEligeLaMaquinaMasAdecuada()
        {
            ServerContext context = TestHelpers.CreateContext();
            // Planik gabeko soroa: hasieran ez dago zereginik
            FarmLayout layout = new FarmLayout(1, 1, 4);
            Field field = new Field();
            field.Id = "FIELD-A";
            field.FarmId = 1;
            List<Field> fields = new List<Field>();
            fields.Add(field);
            TaskCoordinator coordinator = new TaskCoordinator(1, layout, fields, () => context.Config.Current, context.Logger);

            MachineRuntime slow = NewMachine("Seeder-01", MachineType.Seeder, 100);
            MachineRuntime fast = NewMachine("Seeder-02", MachineType.Seeder, 100);
            MachineProfile fastProfile = new MachineProfile();
            fastProfile.Speed = 8;
            fast.Profile = fastProfile;

            // Biak zain geratzen dira (Monitor.Wait)
            TaskAssignment slowResult = null;
            TaskAssignment fastResult = null;
            Thread t1 = new Thread(() => slowResult = coordinator.WaitForTask(slow, 1500, () => false));
            Thread t2 = new Thread(() => fastResult = coordinator.WaitForTask(fast, 1500, () => false));
            t1.Start();
            t2.Start();
            Thread.Sleep(300);

            // Plana sortzen da -> PulseAll -> biek alderatzen dute eta azkarrenak bakarrik hartzen du ereitea
            Assert.Null(coordinator.CreateCropPlan("FIELD-A", CropType.Corn, false, "test"));
            t1.Join();
            t2.Join();

            Assert.Equal(AssignmentOutcome.Assigned, fastResult.Outcome);
            Assert.Equal(AssignmentOutcome.NoTask, slowResult.Outcome);
        }

        [Fact]
        public void Tareas_SinBateriaSuficiente_DebeCargar()
        {
            ServerContext context = TestHelpers.CreateContext();
            TaskCoordinator coordinator = NewCoordinator(context, CropType.Tomato);
            MachineRuntime seeder = NewMachine("Seeder-01", MachineType.Seeder, 5);

            TaskAssignment result = coordinator.WaitForTask(seeder, 500, () => false);

            Assert.Equal(AssignmentOutcome.NeedsCharge, result.Outcome);
        }
    }
}
