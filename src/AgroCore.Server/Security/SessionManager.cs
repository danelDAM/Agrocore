using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AgroCore.Server.Security
{
    /// <summary>
    /// Kontrol Zentroen saio irekiak.
    /// ConcurrentDictionary bat erabiltzen da hainbat harik (bat bezero bakoitzeko, gehi segundoro
    /// egoera bidaltzen duen haria) saioak aldi berean gehitu, kendu eta zeharkatzen dituztelako.
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

        /// <summary>Saio guztien kopia (arazorik gabe zeharka daiteke, beste hari batek zerrenda aldatu arren).</summary>
        public List<UserSession> GetAll()
        {
            return new List<UserSession>(sessions.Values);
        }

        /// <summary>Erabiltzaile jakin baten saio irekiak (hainbat ordenagailutatik konekta daiteke).</summary>
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
