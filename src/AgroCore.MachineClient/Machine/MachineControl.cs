using System.Threading;

namespace AgroCore.MachineClient.Machine
{
    /// <summary>
    /// Órdenes recibidas del servidor (STOP, RESUME, RETURN_TO_BASE, EMERGENCY_STOP, TASK_CANCELLED).
    ///
    /// Las escribe el hilo lector de red y las lee el hilo principal que mueve la máquina.
    /// Cuando la máquina está detenida, el hilo principal espera con Monitor.Wait (sin gastar CPU)
    /// y la orden RESUME lo despierta con Monitor.PulseAll.
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

        /// <summary>RESUME o START: deja de estar detenida y, si estaba aparcada, vuelve al trabajo.</summary>
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

        /// <summary>Devuelve true si había una orden de volver (y la borra).</summary>
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

        /// <summary>Si está detenida, el hilo se queda aquí dormido hasta RESUME.</summary>
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

        /// <summary>Aparcada tras RETURN_TO_BASE: espera aquí hasta START/RESUME.</summary>
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
