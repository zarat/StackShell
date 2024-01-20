using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ScriptStack.Runtime;

namespace ScriptStack
{
    public class TcpServer
    {
        private TcpListener m_tcpListener;
        private List<Socket> m_openSockets;
        private Queue<string> m_messageQueue;
        private object m_queueLock = new object();

        private Action<string> m_scriptMessageHandler;

        public TcpServer(int port, Action<string> scriptMessageHandler)
        {
            m_tcpListener = new TcpListener(IPAddress.Any, port);
            m_openSockets = new List<Socket>();
            m_messageQueue = new Queue<string>();
            m_scriptMessageHandler = scriptMessageHandler;
            Thread messageProcessingThread = new Thread(ProcessMessages);
            messageProcessingThread.IsBackground = true;
            messageProcessingThread.Start();
        }

        public void Start()
        {
            m_tcpListener.Start();
            Thread acceptConnectionsThread = new Thread(AcceptConnections);
            acceptConnectionsThread.IsBackground = true;
            acceptConnectionsThread.Start();
        }

        private void AcceptConnections()
        {
            while (true)
            {
                Socket clientSocket = m_tcpListener.AcceptSocket();
                m_openSockets.Add(clientSocket);
                Thread clientThread = new Thread(() => HandleClient(clientSocket));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }

        private void HandleClient(Socket clientSocket)
        {
            while (true)
            {
                byte[] buffer = new byte[1024];
                int bytesRead = clientSocket.Receive(buffer);
                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                EnqueueMessage(receivedData);

                byte[] sendData = Encoding.UTF8.GetBytes("Server received: " + receivedData);
                clientSocket.Send(sendData);
            }
        }

        private void ProcessMessages()
        {
            while (true)
            {
                string message = DequeueMessage();
                if (message != null)
                {
                    m_scriptMessageHandler(message);
                    Console.WriteLine("Received Message: " + message);
                }

            }
        }

        private void EnqueueMessage(string message)
        {
            lock (m_queueLock)
            {
                m_messageQueue.Enqueue(message);
            }
        }

        private string DequeueMessage()
        {
            lock (m_queueLock)
            {
                if (m_messageQueue.Count > 0)
                {
                    return m_messageQueue.Dequeue();
                }
                return null;
            }
        }

        public void Stop()
        {
            m_tcpListener.Stop();

            foreach (Socket socket in m_openSockets)
            {
                socket.Close();
            }
        }
    }

    public class UdpServer
    {
        private UdpClient m_udpClient;
        private List<IPEndPoint> m_clientEndpoints;
        private Queue<string> m_udpMessageQueue;
        private object m_udpQueueLock = new object();

        private Action<string> m_udpMessageHandler;

        public UdpServer(int port, Action<string> udpMessageHandler)
        {
            m_udpClient = new UdpClient(port);
            m_clientEndpoints = new List<IPEndPoint>();
            m_udpMessageQueue = new Queue<string>();
            m_udpMessageHandler = udpMessageHandler;

            Thread udpMessageProcessingThread = new Thread(ProcessUdpMessages);
            udpMessageProcessingThread.IsBackground = true;
            udpMessageProcessingThread.Start();
        }

        public void Start()
        {
            Thread receiveUdpMessagesThread = new Thread(ReceiveUdpMessages);
            receiveUdpMessagesThread.IsBackground = true;
            receiveUdpMessagesThread.Start();
        }

        private void ReceiveUdpMessages()
        {
            while (true)
            {
                IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] receiveBytes = m_udpClient.Receive(ref clientEndpoint);
                string receivedData = Encoding.UTF8.GetString(receiveBytes);

                EnqueueUdpMessage(receivedData, clientEndpoint);
            }
        }

        private void EnqueueUdpMessage(string message, IPEndPoint clientEndpoint)
        {
            lock (m_udpQueueLock)
            {
                m_clientEndpoints.Add(clientEndpoint);
                m_udpMessageQueue.Enqueue(message);
            }
        }

        private void ProcessUdpMessages()
        {
            while (true)
            {
                string message = DequeueUdpMessage();
                if (message != null)
                {
                    m_udpMessageHandler(message);
                    Console.WriteLine("Received UDP Message: " + message);
                }
            }
        }

        private string DequeueUdpMessage()
        {
            lock (m_udpQueueLock)
            {
                if (m_udpMessageQueue.Count > 0)
                {
                    m_clientEndpoints.RemoveAt(0); // Remove client endpoint associated with the message
                    return m_udpMessageQueue.Dequeue();
                }
                return null;
            }
        }

        public void Stop()
        {
            m_udpClient.Close();
        }
    }

    public class Network : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;
        private List<TcpServer> m_tcpServers;
        private List<string> m_receivedMessages; 
        private int m_serverCounter = 0;

        private List<UdpServer> m_udpServers;

        public Network()
        {
            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();
            routines.Add(new Routine(typeof(int), "startTcpServer", typeof(int), "Starte einen TCP-Server."));
            routines.Add(new Routine(typeof(void), "stopTcpServer", typeof(int), "Stoppe einen TCP-Server."));
            routines.Add(new Routine(typeof(string), "getNextMessage", "Hole die nächste Nachricht aus der Liste der empfangenen Nachrichten."));

            routines.Add(new Routine(typeof(int), "startUdpServer", typeof(int), "Starte einen UDP-Server."));
            routines.Add(new Routine(typeof(void), "stopUdpServer", typeof(int), "Stoppe einen UDP-Server."));
            routines.Add(new Routine(typeof(string), "getNextUdpMessage", "Hole die nächste Nachricht aus der Liste der empfangenen UDP-Nachrichten."));

            exportedRoutines = routines.AsReadOnly();

            m_tcpServers = new List<TcpServer>();
            m_receivedMessages = new List<string>();

            m_udpServers = new List<UdpServer>();
        }

        public object Invoke(string strFunctionName, List<object> listParameters)
        {
            if (strFunctionName == "startTcpServer")
            {
                int port = (int)listParameters[0];

                TcpServer tcpServer = new TcpServer(port, HandleScriptMessage);
                tcpServer.Start();
                m_tcpServers.Add(tcpServer);

                return m_serverCounter++;
            }

            if (strFunctionName == "stopTcpServer")
            {
                int serverId = (int)listParameters[0];
                if (serverId >= 0 && serverId < m_tcpServers.Count)
                {
                    TcpServer tcpServer = m_tcpServers[serverId];
                    tcpServer.Stop();
                    m_tcpServers.RemoveAt(serverId);
                }
                return null;
            }

            if (strFunctionName == "getNextMessage")
            {
                string nextMessage = DequeueNextMessage();
                return nextMessage.Trim();
            }

            if (strFunctionName == "startUdpServer")
            {
                int port = (int)listParameters[0];

                UdpServer udpServer = new UdpServer(port, HandleScriptUdpMessage);
                udpServer.Start();
                m_udpServers.Add(udpServer);

                return m_serverCounter++;
            }

            if (strFunctionName == "stopUdpServer")
            {
                int serverId = (int)listParameters[0];
                if (serverId >= 0 && serverId < m_udpServers.Count)
                {
                    UdpServer udpServer = m_udpServers[serverId];
                    udpServer.Stop();
                    m_udpServers.RemoveAt(serverId);
                }
                return null;
            }

            return null;
        }

        private void HandleScriptMessage(string message)
        {
            EnqueueReceivedMessage(message);
        }

        private void HandleScriptUdpMessage(string message)
        {
            EnqueueReceivedMessage(message);
        }

        private void EnqueueReceivedMessage(string message)
        {
            lock (m_receivedMessages)
            {
                m_receivedMessages.Add(message);
            }
        }

        private string DequeueNextMessage()
        {
            lock (m_receivedMessages)
            {
                if (m_receivedMessages.Count > 0)
                {
                    string nextMessage = m_receivedMessages[0];
                    m_receivedMessages.RemoveAt(0);
                    return nextMessage;
                }
                return null;
            }
        }

        public ReadOnlyCollection<Routine> Routines
        {
            get { return exportedRoutines; }
        }

    }
}
