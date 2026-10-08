using System;

namespace Bow.Net
{
    /// <summary>
    /// 봇 대전용 전송 계층. 항상 호스트이며 상대(봇)는 MatchController 내부의 BotController가 담당한다.
    /// Send는 아무 것도 하지 않는다 (판정도 전부 로컬).
    /// </summary>
    public sealed class LoopbackTransport : IMatchTransport
    {
        public bool IsHost { get { return true; } }
        public bool IsConnected { get { return true; } }
        public string Status { get { return "봇 대전"; } }

#pragma warning disable 67
        public event Action<NetMessage> OnMessage;
        public event Action OnPeerConnected;
        public event Action<string> OnDisconnected;
#pragma warning restore 67

        public void Send(NetMessage msg) { }
        public void Poll() { }
        public void Close() { }
        public void Dispose() { }
    }
}
