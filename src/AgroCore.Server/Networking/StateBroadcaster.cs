using System;
using System.Collections.Generic;
using System.Threading;
using AgroCore.Server.Security;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.Messages;

namespace AgroCore.Server.Networking
{
    /// <summary>
    /// Hilo que cada segundo envía por TCP a cada centro de control el estado completo
    /// (FARM_STATE) de la explotación que está viendo. Cada usuario solo recibe SU explotación.
    /// </summary>
    public class StateBroadcaster
    {
        private readonly ServerContext context;
        private volatile bool running;

        public StateBroadcaster(ServerContext context)
        {
            this.context = context;
        }

        public void Start()
        {
            running = true;
            Thread thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "StateBroadcaster";
            thread.Start();
        }

        public void Stop()
        {
            running = false;
        }

        private void Loop()
        {
            while (running)
            {
                Thread.Sleep(context.Config.Current.StateBroadcastIntervalMs);

                // Un estado por explotación aunque la vean varios usuarios
                Dictionary<int, FarmStateMessage> cache = new Dictionary<int, FarmStateMessage>();
                foreach (UserSession session in context.Sessions.GetAll())
                {
                    try
                    {
                        SendStateTo(session, cache);
                    }
                    catch (Exception)
                    {
                        // si falla un cliente, se sigue con los demás
                    }
                }
            }
        }

        public void SendStateTo(UserSession session, Dictionary<int, FarmStateMessage> cache)
        {
            FarmStateMessage state;
            if (!cache.TryGetValue(session.ViewFarmId, out state))
            {
                FarmRuntime farm = context.Farms.Get(session.ViewFarmId);
                if (farm == null)
                {
                    return;
                }
                state = context.StateBuilder.Build(farm);
                cache[session.ViewFarmId] = state;
            }
            session.Connection.TrySend(state);
        }
    }
}
