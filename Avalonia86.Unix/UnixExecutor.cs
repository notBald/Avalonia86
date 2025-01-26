using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using _86BoxManager.API;
using _86BoxManager.Common;
using System.Text;

namespace _86BoxManager.Unix
{
    public sealed class UnixExecutor : CommonExecutor, IDisposable
    {
        private readonly string _tempDir;
        private readonly IDictionary<string, SocketInfo> _runningVm;

        public IMessageReceiver CallBack { get; set; }

        public UnixExecutor(string tempDir)
        {
            _tempDir = tempDir;
            _runningVm = new Dictionary<string, SocketInfo>();
        }

        public void Dispose()
        {
            foreach (var info in _runningVm.Values)
                info.Dispose();
            _runningVm.Clear();
        }

        ~UnixExecutor()
        {
            Dispose();
        }

        private string GetName(IVm vm) => vm.Title.Replace('/', '_')+vm.UID;

        public override ProcessStartInfo BuildStartInfo(IExecVars args)
        {
            var name = GetName(args.Vm);
            var info = base.BuildStartInfo(args);

            var socketName = name + Environment.ProcessId;

            var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var socketPath = Path.Combine(_tempDir, socketName);
            server.Bind(new UnixDomainSocketEndPoint(socketPath));
            server.Listen();

            _runningVm[name] = new SocketInfo { Server = server };
            args.Vm.OnExit = OnVmExit;

            var opEnv = info.Environment;
            opEnv["86BOX_MANAGER_SOCKET"] = socketName;

            if (server.IsBound)
                server.BeginAccept(OnSocketConnect, (server, (name, args.Vm.UID)));

            return info;
        }

        private void OnSocketConnect(IAsyncResult result)
        {
            var (server, (name, uid)) = (ValueTuple<Socket, ValueTuple<string, long>>)result.AsyncState!;
            try
            {
                var client = server.EndAccept(result);
                _runningVm[name].Client = client;

                // Start reading data from the client
                BeginReceive(client, name, uid);
            }
            catch
            {
                // Simply ignore!
            }
        }

        private void BeginReceive(Socket client, string name, long uid)
        {
            var state = new StateObject { ClientSocket = client, Name = name, UID = uid };
            client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(OnDataReceived), state);
        }

        private class StateObject
        {
            public Socket ClientSocket { get; set; }
            public string Name { get; set; }
            public long UID { get; set; }
            public const int BufferSize = 1024;
            public byte[] Buffer = new byte[BufferSize];
        }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="result"></param>
    /// <remarks>
    /// This function in 86Box sends back communication accross:
    /// 
    /// bool UnixManagerSocket::eventFilter(QObject* obj, QEvent*event)
    /// {
    ///     if (state() == QLocalSocket::ConnectedState)
    ///     {
    ///         if (event->type() == QEvent::WindowBlocked) {
    ///            write(QByteArray { "1" });
    ///         } else if (event->type() == QEvent::WindowUnblocked) {
    ///            write(QByteArray { "0" });
    ///         }
    ///    }

    ///    return QObject::eventFilter(obj, event);
    /// }
    /// 
    /// All it sends back is 1 and 0. 1 is for "waiting" and 0 is for normal.
    /// 
    /// Found in file: 86Box/src/qt/qt_unixmanagerfilter.cpp
    /// </remarks>
    private void OnDataReceived(IAsyncResult result)
        {
            var state = (StateObject)result.AsyncState!;
            var client = state.ClientSocket;

            try
            {
                int bytesRead = client.EndReceive(result);

                if (bytesRead > 0)
                {
                    // Process the data received
                    string data = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);
                    //Console.WriteLine($"Received data from {state.Name}: {data} UID: {state.UID}");

                    if (CallBack != null)
                    {
                        if (data == "0")
                            CallBack.OnDialogClosed(state.UID);
                        else if (data == "1")
                            CallBack.OnDialogOpened(state.UID);
                    }

                    // Continue receiving data
                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(OnDataReceived), state);
                }
                else
                {
                    // Connection closed
                    client.Close();
                    //Console.WriteLine($"Connection closed by {state.Name}");
                }
            }
            catch //(Exception ex)
            {
                //Console.WriteLine($"Error receiving data: {ex.Message}");
                client.Close();
            }
        }

        private void OnVmExit(IVm vm)
        {
            var name = GetName(vm);
            if (!_runningVm.TryGetValue(name, out var info))
                return;
            info.Dispose();
            _runningVm.Remove(name);
        }

        private sealed class SocketInfo : IDisposable
        {
            public Socket Server { get; set; }
            public Socket Client { get; set; }

            public void Dispose()
            {
                Server?.Dispose();
                Client?.Dispose();
            }
        }

        internal Socket GetClient(IVm vm)
        {
            return _runningVm.TryGetValue(GetName(vm), out var info) ? info.Client : null;
        }
    }
}