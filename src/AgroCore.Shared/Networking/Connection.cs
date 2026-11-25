using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AgroCore.Shared.Networking
{
    /// <summary>
    /// Conexión TCP sencilla: envía y recibe líneas de texto (un mensaje JSON por línea).
    /// La usan el servidor, las máquinas y el centro de control.
    ///
    /// Enviar está protegido con lock porque varios hilos pueden escribir en el mismo socket
    /// a la vez (por ejemplo, el hilo que responde peticiones y el que envía el estado cada segundo).
    /// Si no se protegiera, dos mensajes se podrían mezclar en la misma línea.
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
            this.socket.NoDelay = true; // enviar enseguida, sin agrupar paquetes pequeños
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
                RemoteText = "desconocido";
            }
        }

        /// <summary>IP del otro extremo.</summary>
        public IPAddress RemoteAddress { get; private set; }

        public string RemoteText { get; private set; }

        public bool IsClosed
        {
            get { return closed; }
        }

        /// <summary>Se conecta a un servidor (lo usan los clientes).</summary>
        public static Connection Connect(string host, int port)
        {
            TcpClient client = new TcpClient();
            client.Connect(host, port); // bloqueante
            return new Connection(client);
        }

        /// <summary>Envía un mensaje (se convierte a JSON y se escribe como una línea).</summary>
        public void Send(object message)
        {
            string json = MessageSerializer.ToJson(message);
            lock (writeLock)
            {
                writer.WriteLine(json);
                writer.Flush();
            }
        }

        /// <summary>Igual que Send pero sin lanzar excepción si la conexión está cerrada.</summary>
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
        /// Espera (bloqueante) hasta recibir una línea.
        /// Devuelve null cuando el otro extremo cierra la conexión.
        /// </summary>
        public string ReadLine()
        {
            try
            {
                string line = reader.ReadLine();
                while (line != null && line.Length == 0)
                {
                    line = reader.ReadLine(); // ignorar líneas vacías
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
                // ya estaba cerrado
            }
        }
    }
}
