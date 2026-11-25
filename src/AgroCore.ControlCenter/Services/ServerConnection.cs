using System;
using System.Threading;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Services
{
    /// <summary>
    /// Conexión TCP del Centro de Control con el servidor.
    ///
    /// Funciona igual que el ejemplo de clase:
    ///  - Connect(): abre el socket (TcpClient + StreamReader/StreamWriter dentro de Connection).
    ///  - Login(): envía LOGIN_REQUEST y lee la respuesta con ReadLine (todavía sin hilos).
    ///  - StartReading(): arranca UN HILO que hace ReadLine en bucle y avisa con el evento MessageReceived.
    ///
    /// IMPORTANTE: el evento se lanza desde el hilo lector, NO desde el hilo de la interfaz.
    /// Quien lo reciba tiene que usar Dispatcher.BeginInvoke para tocar controles WPF.
    /// </summary>
    public class ServerConnection
    {
        private Connection connection;
        private Thread readerThread;
        private volatile bool closing = false;

        public string Host { get; private set; }
        public int Port { get; private set; }

        /// <summary>Llega un mensaje del servidor: (tipo, json completo).</summary>
        public event Action<string, string> MessageReceived;

        /// <summary>Se ha perdido la conexión (el servidor se cerró o se cayó la red).</summary>
        public event Action ConnectionLost;

        public ServerConnection(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public bool IsConnected
        {
            get { return connection != null && !connection.IsClosed; }
        }

        /// <summary>Abre la conexión TCP. Lanza excepción si el servidor no responde.</summary>
        public void Connect()
        {
            connection = Connection.Connect(Host, Port);
        }

        /// <summary>
        /// Envía usuario y contraseña y espera la respuesta (se llama ANTES de arrancar el hilo lector).
        /// Devuelve null si el servidor cierra la conexión.
        /// </summary>
        public LoginResponse Login(string username, string password)
        {
            LoginRequest request = new LoginRequest();
            request.Username = username;
            request.Password = password;
            connection.Send(request);

            string line = connection.ReadLine();
            if (line == null)
            {
                return null;
            }
            return MessageSerializer.FromJson<LoginResponse>(line);
        }

        /// <summary>Arranca el hilo que recibe los mensajes del servidor.</summary>
        public void StartReading()
        {
            readerThread = new Thread(ReadLoop);
            readerThread.IsBackground = true;
            readerThread.Name = "Lector TCP";
            readerThread.Start();
        }

        private void ReadLoop()
        {
            try
            {
                while (true)
                {
                    string line = connection.ReadLine();
                    if (line == null)
                    {
                        break; // el servidor ha cerrado la conexión
                    }

                    string type = MessageSerializer.ReadType(line);
                    if (MessageReceived != null)
                    {
                        MessageReceived(type, line);
                    }
                }
            }
            catch (Exception)
            {
                // error de red: se trata igual que un cierre
            }

            if (!closing && ConnectionLost != null)
            {
                ConnectionLost();
            }
        }

        /// <summary>Envía un mensaje al servidor. Devuelve false si no se pudo.</summary>
        public bool Send(object message)
        {
            if (connection == null)
            {
                return false;
            }
            return connection.TrySend(message);
        }

        /// <summary>Cierra la conexión (al cerrar sesión). No avisa con ConnectionLost.</summary>
        public void Close()
        {
            closing = true;
            if (connection != null)
            {
                connection.Close();
            }
        }
    }
}
