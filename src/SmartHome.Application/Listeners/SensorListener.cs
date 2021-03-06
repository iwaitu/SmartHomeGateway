﻿
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartHome.Application;
using Microsoft.Extensions.Logging;

namespace SmartHome.Application
{
    
  
    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }


    /// <summary>
    /// 这是一个socket server ，端口 8010
    /// 用于接收传感器消息和控制主灯继电器
    /// </summary>
    public class SensorListener : BackgroundService
    {
        // Thread signal.  
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        private static IList<StateObject> clientStateObjects = new List<StateObject>();

        public static SensorHelper _helper ;
        private readonly ILogger<SensorListener> _logger;

        private int port;
        public SensorListener(IConfiguration configuration, SensorHelper helper, ILogger<SensorListener> logger)
        {
            port = configuration.GetValue<int>("socketServer:port");
            
            _helper = helper;
            _logger = logger;
            _helper.SetListener(this);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartListening(port)); 
        }

        public static void StartListening(int port)
        {
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();

                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
            }

            listener.Shutdown(SocketShutdown.Both);
            listener.Close();

        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);

            NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Info("{0} is connected.", handler.RemoteEndPoint.ToString());
            clientStateObjects.Add(state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                var response = new StringBuilder(1024);
                response.Append(BitConverter.ToString(state.buffer, 0, bytesRead)).Replace("-", " ");
                state.sb = response;
                Task.Run(async () => { await _helper.OnReceiveCommand(response.ToString()); });

                //继续监听
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,new AsyncCallback(ReadCallback), state);
            }
        }

        /// <summary>
        /// 广播命令
        /// </summary>
        /// <param name="command"></param>
        public void PublishCommand(string command)
        {
            foreach(var node in clientStateObjects)
            {
                Send(node.workSocket, command);
            }
        }

        private static void Send(Socket handler, String data)
        {
            byte[] byteData = StringToByteArray(data.Replace(" ", ""));

            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesSent = handler.EndSend(ar);
            }
            catch (Exception e)
            {
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

    }
}
