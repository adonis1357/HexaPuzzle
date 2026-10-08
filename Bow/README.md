# 「활」(Hwal) — 실시간 1:1 궁수 대전 프로토타입

> Unity 2022.3.62f2 · 모바일 세로(1080×1920) · 외부 아트 에셋 없음(전부 프로시저럴) · BGM은 Suno AI

바람이 강하게 부는 들판에서 두 궁수가 마주 서서 **각도·힘·정확도(추 타이밍)** 를 맞춰 상대를 먼저 쓰러뜨리는 실시간 대전 게임입니다.
사격 후 **장전 딜레이**가 있고, 장전이 끝난 뒤 **얼마나 오래 호흡을 골랐는지(초과 대기 h)** 에 따라 다음 딜레이가 줄어듭니다.
효율 `E(h) = (h/10)·e^(1−h/10)` 은 **10초에서 정점**, 그 뒤로는 효율이 떨어집니다.

## 열기 / 실행
1. Unity Hub → **Add project** → 이 폴더(`Bow/`)를 선택해 엽니다 (HexaPuzzle과는 별도 프로젝트입니다).
2. `Assets/Scenes/BowDuel.unity` 를 열고 **Play**.
3. Game 뷰 비율을 **9:16 (Portrait)** 로 두면 의도한 레이아웃으로 보입니다. (가로 비율에서도 동작은 합니다.)

씬에는 `Main Camera` 와 `BowBootstrap` 두 오브젝트만 있고, 로비/월드/HUD/사운드는 전부 런타임에 생성됩니다.

## 조작 (한 손)
| 단계 | 조작 | 설명 |
|------|------|------|
| 조준 | 화면 아무 곳이나 **드래그** | 당기는 반대 방향으로 조준 (앵그리버드식). 각도 = 드래그 각도, 힘 = 드래그 길이(300px = 100%) |
| 정확도 | **손을 떼면** 추(錘)가 좌↔우 왕복 | 중앙에 가까울수록 데미지 배율↑ (1 + (1−|p|)²×0.4). 금색 존(|p|<0.12)은 **Perfect, 추가 +0.1 → 최대 ×1.5**. 3초 지나면 자동 발사(Perfect 불가) |
| 발사 | **탭** | 추 위치 p → 각도 오차 `p × 6° × (1 − 0.3·E)` |
| 호흡 | 장전 완료 후 **기다리기** | 하단 금색 링이 10초까지 차오름. 그 순간 쏘면 다음 장전 **1.5초**(기본 5초). 20초 넘게 기다리면 손해 |

HUD: 상단 HP바(나=주사/상대=쪽빛), 타이머(120초), 바람 화살표+풍속, 하단 호흡 링(남은 장전/효율%/예상 다음 장전), 추 미터.

## 대전 모드
- **봇 대전**: 쉬움/보통/어려움. 봇도 똑같은 호흡 시스템을 씁니다 (난이도별 각도 오차·바람 반영률·대기 시간).
- **LAN 호스트 / LAN 참가**: 같은 Wi-Fi 안에서 TCP 7777 포트. 호스트 화면에 표시된 IP를 참가자가 입력. 호스트가 판정 권위(HP)를 가지며, 바람과 탄도는 seed 기반 결정론이라 양쪽이 같은 궤적을 봅니다.
  - 전송 계층은 `IMatchTransport` 로 추상화되어 있어 Netcode/Photon 등으로 교체 가능합니다.

## 사운드 (Suno AI)
- BGM 파일을 `Assets/Resources/Audio/BGM/` 에 넣으면 자동 재생됩니다: `bgm_lobby`, `bgm_duel`, `bgm_duel_tense`, `bgm_victory`, `bgm_defeat` (.ogg 권장).
- Suno 프롬프트/후처리/루프 가이드: `Docs/03_사운드_사양서_Suno.md`
- SFX는 `Assets/Resources/Audio/SFX/<id>.wav` 가 있으면 그 파일을, 없으면 **프로시저럴 합성 폴백**(`BowSfxSynth`)을 씁니다. 파일 없이도 모든 효과음이 납니다.

## 폴더 구조
```
Bow/
├─ Assets/Scenes/BowDuel.unity         # 카메라 + BowBootstrap
├─ Assets/Scripts/
│  ├─ Core/   (UnityEngine 비의존 순수 C#)  DuelConfig, WindModel, BreathSystem, AccuracyMeter,
│  │                                       ArrowSimulator, MatchSetup, DuelState, BotBrain, DeterministicRandom
│  ├─ Net/    IMatchTransport, LoopbackTransport(봇), LanTcpTransport, NetMessage
│  ├─ Game/   BowBootstrap(진입점), MatchController, ArcherView, ArrowView, AimInput, BotController
│  ├─ Art/    Palette, Ease/ArtConstants, ProceduralSprites, VfxSystem, ParticleMote, WindFieldView
│  ├─ UI/     UiFactory, HudView, LobbyView, ResultView
│  └─ Audio/  BowAudio(매니저), BowSfxSynth(프로시저럴 SFX)
├─ Assets/Resources/Audio/{BGM,SFX}/   # Suno BGM / 교체용 SFX 파일 투입 위치
└─ Docs/  00_프로젝트_브리프 · 01_기획서_GDD · 02_아트_비주얼사양서 · 03_사운드_사양서_Suno
```

## 핵심 수치 (DuelConfig.cs)
| 항목 | 값 |
|------|----|
| HP / 몸통 / 머리 데미지 | 100 / 25 / 40 × 정확도 배율 (1 + (1−|p|)²×0.4, Perfect +0.1 → 최대 ×1.5 = 38 / 60) |
| 화살 초속 | 9 + 13×힘 (9~22 m/s), 중력 9.81 |
| 바람 | 구간형: ±0.5~7 m/s 목표값을 8초 유지, 변화 시 2.5초 smoothstep 전환. HUD에 다음 바람·남은 초 표시. 화살 가속도 = 0.6×w |
| 장전 | 기본 5.0s, 정점 10s, 최대 감소 70% (최소 1.5s), 경기 시작 시 3s |
| 추 미터 | 주기 1.1s × (1+0.3E), Perfect |p|<0.12, 자동 발사 3s |
| 판정 | 머리 원 r=0.30m(y 1.72), 몸통 ±0.30m × 0~1.48m |
| 경기 | 120초, 사거리 16~24m, 높이차 ±1.5m |
| 장애물 | 두 궁수 사이 먹 기둥 (중앙 ±2m, 폭 1m, 높이 3~5.5m, seed 결정). 직선 탄도는 막히고 넘겨 쏴야 함. 봇도 회피 |

## 알려진 제한 (프로토타입)
- 이 세션에서는 Unity 컴파일러를 돌릴 수 없어 **에디터에서 첫 컴파일 확인이 필요**합니다. 오류가 나면 콘솔 메시지를 알려주세요.
- LAN 시계 동기화는 핑 기반 근사(±수십 ms)입니다. 인터넷 매치메이킹은 미구현(전송 계층 교체 포인트만 마련).
- 화살통, 원경 흘러감, 달 등 아트 P2 항목은 미구현.
