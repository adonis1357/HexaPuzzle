using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Bow.Net
{
    /// <summary>
    /// 같은 Wi-Fi(LAN) 안에서 1:1 TCP 연결. 호스트는 포트에서 대기, 클라이언트는 IP로 접속한다.
    /// 메시지는 줄 단위 텍스트(NetMessage.Serialize + '\n').
    /// 수신은 백그라운드 스레드 → ConcurrentQueue → Poll()에서 메인 스레드로 전달.
    /// </summary>
    public sealed class LanTcpTransport : IMatchTransport
    {
        public const int DefaultPort = 7777;
        public const string ProtocolVersion = "hwal-proto-1";

        private readonly bool isHost;
        private readonly int port;
        private readonly string hostAddress;

        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;
        private Thread workerThread;
        private volatile bool running;
        private volatile bool connected;
        private string status = "대기 중";

        private readonly ConcurrentQueue<NetMessage> inbox = new ConcurrentQueue<NetMessage>();
        private readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
        private readonly object sendLock = new object();
        private bool peerConnectedRaised;

        public bool IsHost { get { return isHost; } }
        public bool IsConnected { get { return connected; } }
        public string Status { get { return status; } }

        public event Action<NetMessage> OnMessage;
        public event Action OnPeerConnected;
        public event Action<string> OnDisconnected;

        private LanTcpTransport(bool host, string address, int port)
        {
            isHost = host;
            hostAddress = address;
            this.port = port;
        }

        /// <summary>호스트로 대기 시작</summary>
        public static LanTcpTransport Host(int port = DefaultPort)
        {
            LanTcpTransport t = new LanTcpTransport(true, null, port);
            t.StartHost();
            return t;
        }

        /// <summary>클라이언트로 접속 시작</summary>
        public static LanTcpTransport Connect(string address, int port = DefaultPort)
        {
            LanTcpTransport t = new LanTcpTransport(false, address, port);
            t.StartClient();
            return t;
        }

        /// <summary>이 기기의 LAN IPv4 주소 (호스트 화면에 표시)</summary>
        public static string GetLocalIPv4()
        {
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in entry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch (Exception) { }
            return "127.0.0.1";
        }

        private void StartHost()
        {
            running = true;
            status = "참가자 대기 중 (" + GetLocalIPv4() + ":" + port + ")";
            workerThread = new Thread(HostLoop) { IsBackground = true, Name = "HwalHost" };
            workerThread.Start();
        }

        private void StartClient()
        {
            running = true;
            status = hostAddress + " 접속 중...";
            workerThread = new Thread(ClientLoop) { IsBackground = true, Name = "HwalClient" };
            workerThread.Start();
        }

        private void HostLoop()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(1);
                TcpClient c = listener.AcceptTcpClient();
                listener.Stop();
                listener = null;
                AttachClient(c);
                ReadLoop();
            }
            catch (Exception e)
            {
                if (running) errors.Enqueue("호스트 오류: " + e.Message);
            }
            finally { connected = false; }
        }

        private void ClientLoop()
        {
            try
            {
                TcpClient c = new TcpClient();
                c.Connect(hostAddress, port);
                AttachClient(c);
                ReadLoop();
            }
            catch (Exception e)
            {
                if (running) errors.Enqueue("접속 오류: " + e.Message);
            }
            finally { connected = false; }
        }

        private void AttachClient(TcpClient c)
        {
            client = c;
            client.NoDelay = true;
            stream = client.GetStream();
            connected = true;
            status = isHost ? "참가자 연결됨" : "호스트 연결됨";
            Send(NetMessage.Hello(ProtocolVersion));
        }

        private void ReadLoop()
        {
            byte[] buf = new byte[4096];
            StringBuilder acc = new StringBuilder();
            while (running)
            {
                int n = stream.Read(buf, 0, buf.Length);
                if (n <= 0) { errors.Enqueue("상대가 연결을 끊었습니다"); break; }
                acc.Append(Encoding.UTF8.GetString(buf, 0, n));
                while (true)
                {
                    string s = acc.ToString();
                    int nl = s.IndexOf('\n');
                    if (nl < 0) break;
                    string line = s.Substring(0, nl).TrimEnd('\r');
                    acc.Remove(0, nl + 1);
                    NetMessage m = NetMessage.Parse(line);
                    if (m != null) inbox.Enqueue(m);
                }
            }
        }

        public void Send(NetMessage msg)
        {
            if (!connected || stream == null) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(msg.Serialize() + "\n");
                lock (sendLock)
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
            catch (Exception e)
            {
                errors.Enqueue("송신 오류: " + e.Message);
                connected = false;
            }
        }

        public void Poll()
        {
            if (connected && !peerConnectedRaised)
            {
                peerConnectedRaised = true;
                if (OnPeerConnected != null) OnPeerConnected();
            }
            NetMessage m;
            while (inbox.TryDequeue(out m))
            {
                if (m.type == NetMsgType.Hello)
                {
                    if (m.args.Length > 0 && m.args[0] != ProtocolVersion)
                        errors.Enqueue("프로토콜 버전 불일치: " + m.args[0]);
                    continue;
                }
                if (OnMessage != null) OnMessage(m);
            }
            string err;
            while (errors.TryDequeue(out err))
            {
                status = err;
                if (OnDisconnected != null) OnDisconnected(err);
            }
        }

        public void Close()
        {
            running = false;
            connected = false;
            try { if (listener != null) listener.Stop(); } catch (Exception) { }
            try { if (stream != null) stream.Close(); } catch (Exception) { }
            try { if (client != null) client.Close(); } catch (Exception) { }
            listener = null; stream = null; client = null;
        }

        public void Dispose() { Close(); }
    }
}
