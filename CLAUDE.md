# HexaPuzzle (MatchMine) - 프로젝트 가이드

> **언어 규칙**: 모든 응답, 질문, 보고서, 코드 주석을 **한국어**로 작성한다.

## 프로젝트 개요

Unity로 제작된 육각형 그리드 기반 매치-3 퍼즐 게임. 플레이어는 인접 3개 블록의 삼각형 클러스터를 회전시켜 색상 매칭을 만든다. 모든 비주얼은 프로시저럴 생성 (UI Canvas 기반, SpriteRenderer/ParticleSystem 미사용). 외부 아트 에셋 없음.

- **렌더링**: UI.Image + RectTransform (Canvas 기반)
- **좌표계**: Axial coordinate (q, r), s = -q - r, flat-top 육각형
- **그리드**: 반지름 5, 약 61블록, hexSize 50px
- **타겟**: 모바일 (한 손 플레이, 터치 입력)

## 네임스페이스 구조

```
JewelsHexaPuzzle.Data/       → BlockData.cs (열거형, 데이터 클래스)
JewelsHexaPuzzle.Core/       → 게임플레이 시스템 전체
JewelsHexaPuzzle.Managers/   → 싱글톤 매니저들
```

## 코어 루프 (실행 순서)

```
InputSystem → RotationSystem → MatchingSystem → BlockRemovalSystem → GameManager → StageManager
   터치         120° 회전         패턴 감지        캐스케이드 루프       상태/턴        미션 판정
```

캐스케이드 루프 (BlockRemovalSystem): 매칭 삭제 → 특수 블록 발동 → 낙하 물리 → 리필 → 재확인 (최대 20회)

## 핵심 데이터 (BlockData.cs)

| 열거형 | 값 |
|--------|-----|
| GemType | None, Red, Blue, Green, Yellow, Purple, Orange + Ruby, Emerald, Sapphire, Amber, Amethyst |
| SpecialBlockType | None, MoveBlock, FixedBlock, TimeBomb, Drill, Bomb, Rainbow, XBlock, Drone |
| BlockTier | Normal(0) → Tier1(1) → Tier2(2) → Tier3(3) → ProcessedGem(4) |
| DrillDirection | Vertical, Slash, BackSlash |
| MissionType | CollectGem, CollectMultiGem, ProcessGem, CreateSpecialGem |
| GameState | Loading, Playing, Processing, Paused, StageClear, GameOver |

## 특수 블록 시스템

| 블록 | 생성 조건 | 효과 | 시스템 클래스 |
|------|----------|------|-------------|
| Drill | 4매칭 직선 | 한 방향 라인 파괴 | DrillBlockSystem |
| Bomb | 5+매칭 | 중심 + 인접 6칸 폭발 | BombBlockSystem |

| Rainbow(Donut) | 7+매칭 또는 링 | 해당 색상 전체 파괴 | DonutBlockSystem |
| XBlock | 링 매칭 | 링 색상 전체 파괴 | XBlockSystem |

## 파일 맵

### Core (Assets/Scripts/Core/)
| 파일 | 역할 |
|------|------|
| HexGrid.cs | 그리드 생성, 좌표계, 블록 스폰, GetNeighbors, GetClusterAtPosition |
| HexCoord.cs | 축 좌표 구조체, 이웃 방향 정의 |
| HexBlock.cs | 블록 비주얼/상태, 티어 진행, 인디케이터, 프로시저럴 스프라이트 |
| InputSystem.cs | 터치 감지, 삼각형 클러스터 선택 |
| RotationSystem.cs | 클러스터 120° CW/CCW 회전, 매칭 검증 |
| MatchingSystem.cs | 패턴 감지 (삼각형/링 매칭), 특수 블록 조건 판단 |
| BlockRemovalSystem.cs | 캐스케이드 루프, 블록 삭제 애니메이션, 낙하 바운스 물리, 스폰 애니메이션 |
| DrillBlockSystem.cs | 드릴 투사체, 라인 파괴, 발사 이펙트 |
| BombBlockSystem.cs | 폭발 이펙트, 방사형 파괴 |

| DonutBlockSystem.cs | 무지개 이펙트, 색상별 전체 파괴 |
| XBlockSystem.cs | X패턴 이펙트, 링 색상 파괴 |

### Managers (Assets/Scripts/Managers/)
| 파일 | 역할 |
|------|------|
| GameManager.cs | 싱글톤, 상태 머신, 턴 카운터, 스턱 복구 (8초 타임아웃) |
| StageManager.cs | 스테이지/미션 진행, 스테이지 데이터 로드 |
| ScoreManager.cs | 점수 관리 |
| ScoreCalculator.cs | 티어별 점수 계산 |
| UIManager.cs | UI 팝업, HUD 업데이트 |
| AudioManager.cs | 싱글톤, SFX 풀, ProceduralAudio 연동 |
| ItemManager.cs | 아이템 관리 |
| AchievementManager.cs | 업적 시스템 |

### Utils (Assets/Scripts/Utils/)
| 파일 | 역할 |
|------|------|
| VisualConstants.cs | 통일 비주얼 상수 (이징, 파편, 스파크, 웨이브, 흔들림, 색상 헬퍼) |
| ProceduralAudio.cs | 런타임 사운드 생성 (AudioClip 프로시저럴 합성) |
| GemSpriteProvider.cs | 프로시저럴 젬 스프라이트 |
| GemMaterialManager.cs | 젬 머티리얼 관리 |
| HexagonMeshGenerator.cs | 육각형 메시 생성 |

### UI (Assets/Scripts/UI/)
| 파일 | 역할 |
|------|------|
| ComboDisplay.cs | 콤보 표시 UI |
| ScorePopupManager.cs | 점수 팝업 |

### Items (Assets/Scripts/Items/)
| 파일 | 역할 |
|------|------|
| HammerItem.cs | 망치 아이템 로직 |
| HammerItemSetup.cs | 망치 아이템 설정 |

## 코딩 컨벤션

- **이펙트 생성**: UI.Image GameObject → effectParent에 부모 지정 → raycastTarget = false → Destroy()로 유한 수명
- **이펙트 정리**: CleanupEffects() / CleanupOrphanedEffects()에서 이름으로 찾아 정리
- **애니메이션**: 코루틴 기반, VisualConstants의 이징 함수 사용
- **화면 흔들림**: hexGrid.transform 공유, shakeCount로 중첩 관리, 원래 위치 캡처/복원
- **색상**: GemColors.GetColor() 사용, VisualConstants.Brighten/Darken 헬퍼
- **좌표 검증**: 그리드 접근 전 항상 IsValidCoord() 확인
- **캐스케이드 안전**: 무한 루프 방지 (블록 소모 수렴 필수), 최대 20회 제한
- **상태 머신**: GameManager 상태 밖에서 게임 상태 수정 금지

## 특수 블록 확장 패턴

새 특수 블록 추가 시:
1. `BlockData.cs` → SpecialBlockType enum 값 추가
2. `XxxBlockSystem.cs` → 새 시스템 클래스 생성 (기존 패턴 참조)
3. `MatchingSystem.cs` → 매칭/트리거 조건 정의
4. `HexBlock.cs` → 비주얼 표현 (프로시저럴 스프라이트, 인디케이터)
5. `BlockRemovalSystem.cs` → 발동 이벤트 연결
6. 시스템 클래스에 발동 로직 + 이펙트 + CleanupEffects 구현

## VFX 공통 패턴 (Phase 1 적용 완료)

모든 특수 블록 시스템에 통일 적용된 이펙트 패턴:
- **Pre-Fire Compression**: 발동 전 1.0→0.78→1.18 스케일 애니메이션 (0.12s)
- **Hit Stop**: Time.timeScale 일시 정지 + 슬로모 (쿨다운 관리)
- **Zoom Punch**: hexGrid.transform 스케일 펄스
- **Destroy Flash**: 블록 파괴 시 백색 플래시 오버레이
- **Dual Easing Destroy**: 20% 확장 → 80% 스퀴즈+축소 파괴 애니메이션
- **Bloom Layer**: 센터 플래시 뒤 큰 글로우 레이어
- **Match Pulse**: 매칭 하이라이트 시 스케일 펄스 + 글로우

## 에이전트 팀

| 에이전트 | 모델 | 역할 | 코드 수정 |
|---------|------|------|----------|
| dev-pm | Haiku | 오케스트레이터, 작업 분배 | X |
| programmer | 기본 | 개발 팀장, C# 코드 작성/디버깅 | **O (유일)** |
| art-director | Sonnet | VFX 설계, 비주얼 사양서 | X |
| sound-director | Sonnet | 사운드 설계, 오디오 사양서 | X |
| planning-director | Sonnet | 게임 설계, 밸런스 분석 | X |

**규칙**: 코드(.cs) 수정은 오직 `programmer` 에이전트만 수행. 다른 에이전트는 설계서/사양서를 작성하여 programmer에게 전달.

## 빌드 & 실행

- Unity 프로젝트 (솔루션: `Hexa puzzle project.sln`)
- Unity 에디터에서 Play로 테스트
- **컴파일 검증**: `dotnet build "Assembly-CSharp.csproj"` (Hook이 .cs 수정 시 자동 실행)

## 자동화 (Hooks)

프로젝트에 다음 자동화가 설정되어 있다:

### 컴파일 게이트
- `.cs` 파일 수정(Write/Edit) 후 자동으로 `dotnet build`가 실행된다
- 컴파일 에러 발생 시 `additionalContext`로 피드백 → 즉시 수정
- Claude 응답 완료 전 최종 빌드 검증이 실행된다 (빌드 실패 시 블로킹)

### 코드 수정 권한
- `.cs` 파일 수정은 오직 `개발` 에이전트만 수행한다
- 기획/아트/사운드 에이전트는 설계서/사양서만 작성

### Hook 파일
- `.claude/hooks/post-cs-edit.sh` — PostToolUse: 컴파일 검증
- `.claude/hooks/stop-build-check.sh` — Stop: 최종 빌드 게이트
- `.claude/hooks/dev-agent-done.sh` — SubagentStop: 개발 에이전트 검증

## 슬래시 커맨드

| 커맨드 | 용도 |
|--------|------|
| `/버그수정 [설명]` | 버그 수정 워크플로우 |
| `/기능추가 [설명]` | 기능 추가 워크플로우 |
| `/리팩토링 [대상]` | 리팩토링 워크플로우 |
| `/특수블록 [사양]` | 특수 블록 추가 (확장 패턴) |
| `/빌드` | 컴파일 검증 |
| `/코드리뷰 [범위]` | 코드 리뷰 |
| `/스킬트리 [스킬명] [효과]` | 스킬트리 추가 (4단계 체크리스트) |
| `/아이템 [이름] [효과]` | 아이템 추가/수정 (Item+Gauge 패턴) |
| `/몬스터 [이름] [행동]` | 몬스터(고블린) 추가/수정 (9단계 체크리스트) |
| `/레벨디자인 [범위] [설명]` | 스테이지/레벨 추가/수정 (7단계 체크리스트) |
| `/튜토리얼 [분석\|추가\|검증\|수정]` | 튜토리얼 시퀀스 설계/분석/추가/검증 |
| `/유니티갱신` | Unity Ctrl+R 갱신 + Ctrl+P 재생 사이클 자동 실행 (코드/에셋 변경 후 자동 호출) |

## Unity 자동화 사이클 자동 실행 정책

**코드(.cs) 또는 Resources 에셋(PNG 등) 변경 시 사용자 명시 요청 없이도 응답 마무리 시점에 Unity 갱신+재생 사이클을 자동 실행한다.**

- **실행 방법 (파일 트리거 — PowerShell/키 입력 불필요)**:
  ```bash
  touch ".claude/refresh_trigger"
  ```
  Unity 에디터의 `Assets/Scripts/Editor/UnityAutoRefresh.cs`(파일 감시)가 이 파일을 감지해
  **Play 종료(재생 중이면) → AssetDatabase.Refresh(컴파일) → Play 재시작**을 스스로 수행한다.
- 사이클당 1회만 실행 (같은 응답 내 다회 수정 시 마지막에만)
- 단순 `dotnet build`만 한 경우 / 문서 수정만 한 경우는 스킵
- (구 방식 `Send-UnityKey` PowerShell 키 입력은 권한 차단 + 포커스 불안정으로 폐기됨)
