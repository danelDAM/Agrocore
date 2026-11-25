using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AgroCore.Server.Security
{
    /// <summary>
    /// Sesiones abiertas de los centros de control.
    /// Se usa un ConcurrentDictionary porque varios hilos (uno por cliente, más el hilo que
    /// envía el estado cada segundo) añaden, quitan y recorren sesiones a la vez.
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, UserSession> sessions = new ConcurrentDictionary<string, UserSession>();

        public void Add(UserSession session)
        {
            sessions[session.Token] = session;
        }

        public void Remove(string token)
        {
            UserSession removed;
            sessions.TryRemove(token, out removed);
        }

        public UserSession Get(string token)
        {
            UserSession session;
            sessions.TryGetValue(token, out session);
            return session;
        }

        /// <summary>Copia de todas las sesiones (se puede recorrer sin problemas aunque otro hilo cambie la lista).</summary>
        public List<UserSession> GetAll()
        {
            return new List<UserSession>(sessions.Values);
        }

        /// <summary>Sesiones abiertas de un usuario concreto (puede estar conectado desde varios equipos).</summary>
        public List<UserSession> FindByUser(int userId)
        {
            List<UserSession> result = new List<UserSession>();
            foreach (UserSession session in sessions.Values)
            {
                if (session.UserId == userId)
                {
                    result.Add(session);
                }
            }
            return result;
        }

        public int Count
        {
            get { return sessions.Count; }
        }
    }
}
