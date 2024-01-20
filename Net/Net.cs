using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
using ScriptStack.Runtime;
using System.Collections.ObjectModel;

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

            // Starte einen Hintergrundthread für die Nachrichtenverarbeitung
            Thread messageProcessingThread = new Thread(ProcessMessages);
            messageProcessingThread.IsBackground = true;
            messageProcessingThread.Start();
        }

        public void Start()
        {
            m_tcpListener.Start();

            // Starte einen Hintergrundthread für das Akzeptieren von Verbindungen
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

                // Starte einen Hintergrundthread für die Kommunikation mit dem Client
                Thread clientThread = new Thread(() => HandleClient(clientSocket));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }

        private void HandleClient(Socket clientSocket)
        {
            // Hier implementieren Sie die Logik für die Kommunikation mit dem Client,
            // z.B., Lesen und Schreiben von Daten über den Socket

            // Beispiel: Lesen und Schreiben von Daten in einer Endlosschleife
            while (true)
            {
                // Implementieren Sie hier die Logik zum Lesen von Daten vom Client
                byte[] buffer = new byte[1024];
                int bytesRead = clientSocket.Receive(buffer);
                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Fügen Sie die empfangene Nachricht zur Verarbeitung in die Warteschlange ein
                EnqueueMessage(receivedData);

                // Implementieren Sie hier die Logik zum Senden von Daten an den Client
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
                    // Übergebe die empfangene Nachricht an das Skript
                    m_scriptMessageHandler(message);

                    // Hier können Sie die empfangenen Nachrichten weiterverarbeiten,
                    // zum Beispiel an die Anwendung weitergeben oder in einer Log-Datei speichern.
                    Console.WriteLine("Received Message: " + message);
                }

                // Fügen Sie hier ggf. eine Verzögerung ein, um die CPU nicht zu stark zu belasten
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

    public class Network : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;
        private List<TcpServer> m_tcpServers;
        private List<string> m_receivedMessages; // Liste zur Aufbewahrung der empfangenen Nachrichten
        private int m_serverCounter = 0; // Zähler für die Server-IDs

        public Network()
        {
            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();
            routines.Add(new Routine(typeof(int), "startTcpServer", typeof(int), "Starte einen TCP-Server."));
            routines.Add(new Routine(typeof(void), "stopTcpServer", typeof(int), "Stoppe einen TCP-Server."));
            routines.Add(new Routine(typeof(string), "getNextMessage", "Hole die nächste Nachricht aus der Liste der empfangenen Nachrichten."));

            exportedRoutines = routines.AsReadOnly();
            m_tcpServers = new List<TcpServer>();
            m_receivedMessages = new List<string>();
        }

        public object Invoke(string strFunctionName, List<object> listParameters)
        {
            if (strFunctionName == "startTcpServer")
            {
                int port = (int)listParameters[0];

                TcpServer tcpServer = new TcpServer(port, HandleScriptMessage);
                tcpServer.Start();
                m_tcpServers.Add(tcpServer);

                // Rückgabewert ist die ID des Servers
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

            return null;
        }

        private void HandleScriptMessage(string message)
        {
            // Hier können Sie die empfangenen Nachrichten im Skript verarbeiten
            // print("Received message: " + message);
            // Weitere Verarbeitung ...
            // Fügen Sie die empfangene Nachricht zur Liste der empfangenen Nachrichten hinzu
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
