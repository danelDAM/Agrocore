using System;
using System.Threading;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Services
{
    /// <summary>
    /// Kontrol Zentroaren TCP konexioa zerbitzariarekin.
    ///
    /// Klaseko adibidearen berdin funtzionatzen du:
    ///  - Connect(): socketa irekitzen du (TcpClient + StreamReader/StreamWriter Connection barruan).
    ///  - Login(): LOGIN_REQUEST bidaltzen du eta erantzuna ReadLine-rekin irakurtzen du (oraindik harigabe).
    ///  - StartReading(): HARI BAT abiarazten du, ReadLine begizta batean egiten duena eta MessageReceived gertaerarekin abisatzen duena.
    ///
    /// GARRANTZITSUA: gertaera hari irakurletik jaurtitzen da, EZ interfazearen haritik.
    /// Jasotzen duenak Dispatcher.BeginInvoke erabili behar du WPF kontrolak ukitzeko.
    /// </summary>
    public class ServerConnection
    {
        private Connection connection;
        private Thread readerThread;
        private volatile bool closing = false;

        public string Host { get; private set; }
        public int Port { get; private set; }

        /// <summary>Zerbitzariaren mezu bat iristen da: (mota, json osoa).</summary>
        public event Action<string, string> MessageReceived;

        /// <summary>Konexioa galdu da (zerbitzaria itxi da edo sarea erori da).</summary>
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

        /// <summary>TCP konexioa irekitzen du. Salbuespena jaurtitzen du zerbitzariak erantzuten ez badu.</summary>
        public void Connect()
        {
            connection = Connection.Connect(Host, Port);
        }

        /// <summary>
        /// Erabiltzailea eta pasahitza bidaltzen ditu eta erantzunaren zain geratzen da (hari irakurlea abiarazi AURRETIK deitzen da).
        /// null itzultzen du zerbitzariak konexioa ixten badu.
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

        /// <summary>Zerbitzariaren mezuak jasotzen dituen haria abiarazten du.</summary>
        public void StartReading()
        {
            readerThread = new Thread(ReadLoop);
            readerThread.IsBackground = true;
            readerThread.Name = "TCP irakurlea";
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
                        break; // zerbitzariak konexioa itxi du
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
                // sare-errorea: itxiera baten berdin tratatzen da
            }

            if (!closing && ConnectionLost != null)
            {
                ConnectionLost();
            }
        }

        /// <summary>Mezu bat bidaltzen dio zerbitzariari. false itzultzen du ezin izan bada.</summary>
        public bool Send(object message)
        {
            if (connection == null)
            {
                return false;
            }
            return connection.TrySend(message);
        }

        /// <summary>Konexioa ixten du (saioa ixtean). Ez du ConnectionLost bidez abisatzen.</summary>
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
