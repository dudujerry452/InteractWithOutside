using System;
using System.IO;
using System.Threading;
using ClientPlugin.GUI;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using VRage.FileSystem;
using VRage.Game;
using VRage.Plugins;

using Sandbox;
using EmptyKeys.UserInterface;
using VRage;

using Sandbox.Game.Entities.Blocks;
using Sandbox.Engine.Utils;

using VRage.Scripting;
using System.Security.Policy;
using System.Reflection;
using System.Collections.Concurrent;

using System.Collections.Generic;
using System.Threading.Tasks;

using Shared.MyLogging;
using System.Collections;

using System.Text;
using System.Linq;
using VRage.Game.Components;
using Microsoft.CodeAnalysis;
using static System.Net.Mime.MediaTypeNames;


using VRageMath;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using System.Data.SqlClient;
using System.Net.Sockets;
using System.Net;
using VRage.Library.Collections;

namespace InteractUtil {

    enum NetworkErr : int
    {
        NET_SUC = 100,
        CON_ERR = 101,
        SEN_ERR = 102,
        REV_ERR = 103,
        PWD_ERR = 104,
        UNK_ERR = 105,
        INT_ERR = 106,
    }

    public class SocketServer
    {

        private Logger m_logger;

        private Dictionary<string, List<string> > m_tagHash = new Dictionary<string, List<string> >(); // storage remote info

        // Manage communication object, all method is async
        private class SocketObject
        {

            private SocketServer server;
            public List<string> tags = new List<string>();

            public TcpClient client;
            public string remote_code = null;

            private ConcurrentQueue<string> recv_queue = new ConcurrentQueue<string>();
            private bool[] active = new bool[2];
            private readonly object locker = new object();
            private int err_counter = 0;
            private bool launched = false;

            private ManualResetEvent data_received = new ManualResetEvent(false);


            public NetworkErr status;



            Logger logger = new Logger("appnet.log");

            public void Send(string send_data)
            {
                if (status == NetworkErr.NET_SUC)
                {
                    logger.Log("Send : " +  send_data);
                    ThreadPool.QueueUserWorkItem(Process2, send_data);
                }
            }
            public string Recv_Peek(int mode) // mode 0 = block, mode 1 = flush
            {
                if (!launched) return null;
                string ret;
                if (recv_queue.TryPeek(out ret)) return ret;
                else if(mode == 0) { data_received.WaitOne(); return Recv_Peek(0); }
                else { return null; }  
            }
            public string Recv(int mode) // mode 0 = block, mode 1 = flush
            {
                if (!launched) return null;
                string ret;
                if (recv_queue.TryDequeue(out ret)) return ret;
                else if (mode == 0) { data_received.WaitOne(); return Recv(0); }
                else { return null; }
            }

            // Waiting for Data
            private void Process(object state)
            {
                lock (locker)
                {
                    if (!CheckConnected()) return;
                    WakeUp();

                    active[0] = true;
                    NetworkStream stream = client.GetStream();


                    // network data
                    byte[] buffer = new byte[1024];
                    int bytesRead;

                    try
                    {
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            logger.Log("Received: " + message);
                            logger.Log("Now Object number: " + server.m_sockets.Count);
                            recv_queue.Enqueue(message);
                            data_received.Set();
                        }

                        HandleError(0, NetworkErr.REV_ERR);
                        return;
                    }
                    catch (IOException e)
                    {
                        logger.Log(e.ToString());
                        HandleError(0, NetworkErr.REV_ERR);
                        return;
                    }
                }
            }

            private void Process2(object content)
            {
                lock (locker)
                {
                    if (!CheckConnected()) return; 
                    active[1] = true;
                    NetworkStream stream = client.GetStream();

                    byte[] message = Encoding.UTF8.GetBytes((string)content);
                    try { stream.Write(message, 0, message.Length); }
                    catch { HandleError(0, NetworkErr.SEN_ERR); }
                    active[1] = false;
                    return;
                }
            }

            public SocketObject(TcpClient client, SocketServer server)
            {
                this.client = client;
                this.status = NetworkErr.INT_ERR;
                this.server = server;
                this.launched = true;

                ThreadPool.QueueUserWorkItem(Process);

            }

            public void AddTag(string tag)
            {
                if (remote_code == null) return;
                tags.Add(tag);
                try
                {
                    if (!server.m_tagHash[tag].Contains(remote_code))
                        server.m_tagHash[tag].Add(remote_code);
                }
                catch (KeyNotFoundException)
                {
                    server.m_tagHash.Add(tag, new List<string> { remote_code });
                }

            }



            private void HandleError(int process, NetworkErr err) {
                active[process] = false;
                status = err;
                if (++err_counter > 3) { // 3 : Max times to try
                    Shutdown();
                }
                else
                {
                    if(process == 0) ThreadPool.QueueUserWorkItem(Process);
                    else ThreadPool.QueueUserWorkItem(Process2);
                }
            }

            

            private bool CheckConnected()
            {
                if(client == null)
                {
                    err_counter = 999; // it is dead
                    HandleError(0, NetworkErr.CON_ERR);
                    return false;
                }
                if (client.Client == null)
                {
                    err_counter = 999; // it is dead
                    HandleError(0, NetworkErr.CON_ERR);
                    return false;
                }
                if (client.Connected) { WakeUp();  return true; }
                try
                {
                    NetworkStream stream = client.GetStream();
                    stream.Close();
                }
                catch { }
                int n = 0;
                IPEndPoint iPEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                
                while (!client.Client.Connected && ++n <= 3)
                {
                    client = new TcpClient();
                    try{client.Connect(iPEndPoint);}
                    catch { }
                    System.Threading.Thread.Sleep(1000); // wait 1 sec
                    logger.Log("Try for reconnect");
                }
                if(client.Connected) { WakeUp(); return true; }
                err_counter = 999;
                HandleError(0, NetworkErr.CON_ERR);
                return false;
            }

            private void WakeUp()
            {
                status = NetworkErr.NET_SUC;
                err_counter = 0;
                launched = true;
            }

            public bool TryRelaunch(TcpClient client)
            {
                this.client = client;
                return CheckConnected();
            }

            public bool TryRelaunch()
            {
                return CheckConnected();
            }

            public void Shutdown()
            {
                launched = false;
                data_received.Set();
                if (status == NetworkErr.NET_SUC || status == NetworkErr.INT_ERR) status = NetworkErr.UNK_ERR;
                if (client != null)
                {
                    try
                    {
                        if (client.Connected)
                        {
                            NetworkStream stream = client.GetStream();
                            if (stream.CanWrite)
                            {
                                stream.Close();
                            }
                            client.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Log(ex.ToString());
                    }
                    finally
                    {
                        // Make sure client is close
                        if (client.Client != null)
                        {
                            try
                            {
                                client.Client.Shutdown(SocketShutdown.Both);
                            }
                            catch { }
                        }
                        logger.Log("Shutdown");
                    }
                }
                
            }

            ~SocketObject()
            {
                Shutdown(); 
            }

            public bool Useable { get { if (status != NetworkErr.NET_SUC) return false; return true; } }

            public bool Active { get { if (launched) return true; return false; } }




        }


        private TcpListener m_tcpListener;
        private bool m_isRunning;
        private int m_port;

        private Dictionary<string, SocketObject> m_sockets = new Dictionary<string, SocketObject>();


        public SocketServer(int port)
        {
            m_logger = new Logger("appnet.log");

            m_port = port;
            // TODO: read password from .cfg
            m_tcpListener = new TcpListener(IPAddress.Any, m_port);

            ThreadPool.QueueUserWorkItem(Start); // Start the service
        }

        ~SocketServer()
        {
            Stop();
        }

        private void Start(object state)
        {
            m_tcpListener.Start();
            m_isRunning = true;

            m_logger.Log("Server started. Waiting for connections...");

            while (m_isRunning)
            {
                try
                {
                    // Waiting for Connect
                    TcpClient client = m_tcpListener.AcceptTcpClient();
                    m_logger.Log("Connected!");

                    ThreadPool.QueueUserWorkItem(AddObject, client);
                }
                catch (SocketException e)
                {
                    m_logger.Log("SocketException: " + e);
                }
            }
            Stop();
        }

        private void AddObject(object client)
        {
            SocketObject obj = new SocketObject((TcpClient)client, this);
            obj.Send("Ask for identity code");
            string res = obj.Recv(0);
            if (res == null || !res.StartsWith("IDCODE") || res.Length <= 6) return;
            string rem = res.Substring(6);
            obj.Send("Ask for pb code");
            res = obj.Recv(0);
            if (res == null || !res.StartsWith("PBCODE") || res.Length <= 6) return;
            res = res.Substring(6);
            obj.remote_code = rem;
            obj.AddTag(res);
            m_sockets.Add(rem, obj);
            return;
        }

        

        public void Stop()
        {
            m_isRunning = false;
            m_tcpListener.Stop();

            foreach(var socket in m_sockets)
            {
                socket.Value.Shutdown();
            }
        }


        
    }


}