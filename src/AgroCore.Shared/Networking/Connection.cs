using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AgroCore.Shared.Networking
{
    /// <summary>
    /// TCP konexio sinplea: testu-lerroak bidali eta jasotzen ditu (JSON mezu bat lerroko).
    /// Zerbitzariak, makinek eta Kontrol Zentroak erabiltzen dute.
    ///
    /// Bidaltzea lock-ekin babestuta dago, hainbat harik socket berean idatz dezaketelako
    /// aldi berean (adibidez, eskaerei erantzuten dien haria eta egoera segundoro bidaltzen duena).
    /// Babestuko ez balitz, bi mezu lerro berean nahas litezke.
    /// </summary>
    public class Connection
    {
        private readonly TcpClient socket;
        private readonly NetworkStream network;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;
        private readonly object writeLock = new object();
        private bool closed = false;

        public Connection(TcpClient socket)
        {
            this.socket = socket;
            this.socket.NoDelay = true; // berehala bidali, pakete txikiak taldekatu gabe
            network = socket.GetStream();
            reader = new StreamReader(network, new UTF8Encoding(false));
            writer = new StreamWriter(network, new UTF8Encoding(false));
            writer.NewLine = "\n";

            IPEndPoint remote = socket.Client.RemoteEndPoint as IPEndPoint;
            if (remote != null)
            {
                RemoteAddress = remote.Address;
                RemoteText = remote.ToString();
            }
            else
            {
                RemoteText = "ezezaguna";
            }
        }

        /// <summary>Beste muturraren IPa.</summary>
        public IPAddress RemoteAddress { get; private set; }

        public string RemoteText { get; private set; }

        public bool IsClosed
        {
            get { return closed; }
        }

        /// <summary>Zerbitzari batera konektatzen da (bezeroek erabiltzen dute).</summary>
        public static Connection Connect(string host, int port)
        {
            TcpClient client = new TcpClient();
            client.Connect(host, port); // blokeatzailea
            return new Connection(client);
        }

        /// <summary>Mezu bat bidaltzen du (JSON bihurtzen da eta lerro gisa idazten da).</summary>
        public void Send(object message)
        {
            string json = MessageSerializer.ToJson(message);
            lock (writeLock)
            {
                writer.WriteLine(json);
                writer.Flush();
            }
        }

        /// <summary>Send bezala, baina salbuespenik jaurti gabe konexioa itxita badago.</summary>
        public bool TrySend(object message)
        {
            try
            {
                Send(message);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Itxaron (blokeatuz) lerro bat jaso arte.
        /// null itzultzen du beste muturrak konexioa ixten duenean.
        /// </summary>
        public string ReadLine()
        {
            try
            {
                string line = reader.ReadLine();
                while (line != null && line.Length == 0)
                {
                    line = reader.ReadLine(); // lerro hutsak ez ikusi
                }
                return line;
            }
            catch (IOException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        public void Close()
        {
            if (closed)
            {
                return;
            }
            closed = true;
            try
            {
                socket.Close();
            }
            catch (Exception)
            {
                // itxita zegoen jada
            }
        }
    }
}
