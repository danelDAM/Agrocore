using System.Threading;

namespace AgroCore.MachineClient.Machine
{
    /// <summary>
    /// Zerbitzaritik jasotako aginduak (STOP, RESUME, RETURN_TO_BASE, EMERGENCY_STOP, TASK_CANCELLED).
    ///
    /// Sareko irakurle-hariak idazten ditu eta makina mugitzen duen hari nagusiak irakurtzen ditu.
    /// Makina geldituta dagoenean, hari nagusia Monitor.Wait-ekin itxaroten du (CPUrik gastatu gabe)
    /// eta RESUME aginduak Monitor.PulseAll-ekin esnatzen du.
    /// </summary>
    public class MachineControl
    {
        private readonly object controlLock = new object();
        private bool halted;
        private bool emergency;
        private bool returnRequested;
        private bool parkedHold;
        private string cancelledTaskId;

        public bool IsHalted
        {
            get { lock (controlLock) { return halted; } }
        }

        public bool IsEmergency
        {
            get { lock (controlLock) { return emergency; } }
        }

        public bool ReturnRequested
        {
            get { lock (controlLock) { return returnRequested; } }
        }

        public bool IsParkedHold
        {
            get { lock (controlLock) { return parkedHold; } }
        }

        public void Stop(bool isEmergency)
        {
            lock (controlLock)
            {
                halted = true;
                emergency = emergency || isEmergency;
                Monitor.PulseAll(controlLock);
            }
        }

        /// <summary>RESUME edo START: geldituta egoteari uzten dio eta, aparkatuta bazegoen, lanera itzultzen da.</summary>
        public void Resume()
        {
            lock (controlLock)
            {
                halted = false;
                emergency = false;
                parkedHold = false;
                Monitor.PulseAll(controlLock);
            }
        }

        public void RequestReturn()
        {
            lock (controlLock)
            {
                returnRequested = true;
                Monitor.PulseAll(controlLock);
            }
        }

        /// <summary>true itzultzen du itzultzeko agindurik bazegoen (eta ezabatu egiten du).</summary>
        public bool TakeReturnRequest()
        {
            lock (controlLock)
            {
                bool value = returnRequested;
                returnRequested = false;
                return value;
            }
        }

        public void SetParkedHold()
        {
            lock (controlLock)
            {
                parkedHold = true;
            }
        }

        public void CancelTask(string taskId)
        {
            lock (controlLock)
            {
                cancelledTaskId = taskId;
            }
        }

        public bool IsTaskCancelled(string taskId)
        {
            lock (controlLock)
            {
                return taskId != null && taskId == cancelledTaskId;
            }
        }

        /// <summary>Geldituta badago, haria hemen geratzen da lo RESUME iritsi arte.</summary>
        public void WaitWhileHalted()
        {
            lock (controlLock)
            {
                while (halted)
                {
                    Monitor.Wait(controlLock);
                }
            }
        }

        /// <summary>RETURN_TO_BASE ondoren aparkatuta: hemen itxaroten du START/RESUME iritsi arte.</summary>
        public void WaitWhileParked()
        {
            lock (controlLock)
            {
                while (parkedHold)
                {
                    Monitor.Wait(controlLock);
                }
            }
        }
    }
}
