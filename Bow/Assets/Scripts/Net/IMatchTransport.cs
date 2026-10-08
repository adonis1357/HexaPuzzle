using System;

namespace Bow.Net
{
    /// <summary>
    /// 대전 전송 계층 추상화. MatchController는 이 인터페이스만 알며,
    /// 봇 대전(LoopbackTransport)과 LAN 대전(LanTcpTransport)을 동일하게 다룬다.
    /// 모든 이벤트는 Poll()을 호출한 메인 스레드에서 발생한다.
    /// </summary>
    public interface IMatchTransport : IDisposable
    {
        /// <summary>호스트(권위) 여부. 로컬 플레이어 id = 호스트 0 / 클라이언트 1</summary>
        bool IsHost { get; }
        /// <summary>상대와 연결되어 메시지 교환이 가능한 상태</summary>
        bool IsConnected { get; }
        /// <summary>UI 표시용 상태 문자열</summary>
        string Status { get; }

        event Action<NetMessage> OnMessage;
        event Action OnPeerConnected;
        event Action<string> OnDisconnected;

        void Send(NetMessage msg);
        /// <summary>메인 스레드에서 매 프레임 호출: 수신 큐를 비우며 이벤트 발생</summary>
        void Poll();
        void Close();
    }
}
