# HexaPuzzle UI/UX 시스템 분석 보고서

**작성일:** 2026-02-14
**프로젝트:** JewelsHexaPuzzle
**분석자:** 기획 디렉터

---

## 목차
1. [현재 UI 구성 현황](#1-현재-ui-구성-현황)
2. [문제점 및 개선 기회](#2-문제점-및-개선-기회)
3. [구체적인 개선 제안](#3-구체적인-개선-제안)
4. [모바일 UX 고려사항](#4-모바일-ux-고려사항)
5. [우선순위별 로드맵](#5-우선순위별-로드맵)

---

## 1. 현재 UI 구성 현황

### 1.1 점수 표시 (Score Display)

**구현 파일:** `UIManager.cs` (line 18, 199-260)

**현재 구현:**
- **UI 요소:** `goldText` (Text 컴포넌트)
- **표시 위치:** 상단 HUD (정확한 위치는 Scene에서 설정)
- **애니메이션:**
  - 점수 증가 시 카운팅 애니메이션 (0.5초 duration)
  - EaseOutQuart 이징으로 부드러운 카운트업
  - 증가 중 하이라이트 색상 전환 (골드 색상 `#FFD700`)
  - 숫자 포맷팅: 1,000 단위 콤마 구분 (`FormatNumber`)
- **연동:** `ScoreManager.OnScoreChanged` 이벤트 구독
- **시각적 피드백:**
  - 증가 시 골드 색상으로 변경
  - 완료 후 흰색으로 페이드 복귀 (0.3초)

**추가 점수 팝업 시스템:**
- **파일:** `ScorePopupManager.cs`
- **기능:** 블록 제거 위치에 "+300" 같은 플로팅 점수 표시
- **티어 시스템:**
  - Small (0~299): 28px, White
  - Medium (300~999): 34px, Gold
  - Large (1000~2999): 42px, Orange, Bold
  - Epic (3000+): 50px, Red-Orange, Bold
- **애니메이션:**
  - Scale 0 → 1.2 → 1.0 (EaseOutBack)
  - 위로 이동 (80~140px, EaseOutQuart)
  - 랜덤 좌우 지터
  - 페이드아웃 (60% 유지 후 40% 구간에서 사라짐)

### 1.2 회전 횟수 (Move Counter)

**구현 파일:** `UIManager.cs` (line 15, 19-21, 129-175)

**현재 구현:**
- **UI 요소:**
  - `turnText`: 현재 남은 턴 표시
  - `moveMaxText`: 최대 턴 표시 (예: "/30")
  - `moveProgressRing`: 원형 진행 바 (Image, fillAmount)
- **애니메이션:**
  - **턴 감소 시:** 바운스 애니메이션 (1.0 → 1.15 → 1.0, 0.2초)
  - **위험 상태 펄스:**
    - 5턴 이하: 빨간색 + 펄스 (0.5초 주기)
    - 3턴 이하: 강한 빨간색 + 빠른 펄스 (0.3초 주기)
- **프로그레스 링 색상:**
  - 100% ~ 30%: 흰색
  - 30% ~ 17%: 주황색 (`#FF9900`)
  - 17% ~ 0%: 빨간색
- **사운드 연동:**
  - 3턴 이하일 때 `AudioManager.PlayWarningBeep()` 호출

**VisualConstants 상수:**
```csharp
MoveBounceScale = 1.15f
MoveBounceDuration = 0.2f
MovePulseSpeed = 0.5f
```

### 1.3 아이템 UI

**구현 파일:** `UIManager.cs` (line 42-43, 393-405, 844-904)

**현재 구현:**
- **UI 컴포넌트:** `ItemButtonUI[]` (최대 4개)
- **각 버튼 구성:**
  - `iconImage`: 아이템 아이콘
  - `countText`: 보유 수량 ("3" 또는 "+")
  - `lockOverlay`: 잠금 오버레이
  - `lockStageText`: 해금 스테이지 표시 ("stage 10")
  - `button`: 클릭 가능 버튼
- **아이템 타입:** (ItemManager.cs 참조)
  - Hammer (stage 10 해금)
  - Bomb (stage 30 해금)
  - SixWayLaser (stage 50 해금)
  - SSD (stage 100 해금)
- **인터랙션:**
  - 해금 전: 버튼 비활성 + 오버레이 표시
  - 수량 0개: "+" 표시 (구매 UI 트리거)
  - 수량 있음: 숫자 표시 + 클릭 가능
- **사용 플로우:**
  1. 버튼 클릭 → `ItemManager.UseItem()`
  2. 타겟 선택 모드 진입 (inputSystem 비활성화)
  3. 타겟 선택 → 아이템 효과 실행 → 수량 감소

**문제점:** 버튼 배치/크기/피드백에 대한 구체적 명세 없음 (Scene 의존)

### 1.4 전체 HUD 레이아웃

**구현 파일:** `UIManager.cs` (line 14-48)

**상단 HUD 요소:**
```csharp
[Header("HUD Elements")]
turnText          // 남은 턴 숫자
stageText         // 현재 스테이지 번호
goldText          // 골드(점수)

[Header("Move Counter")]
moveProgressRing  // 턴 진행 바 (원형)
moveMaxText       // 최대 턴 표시

[Header("Buttons")]
pauseButton       // 일시정지 버튼
rotationToggleButton  // 회전 방향 토글
rotationDirectionIcon // 시계/반시계 아이콘
```

**미션 표시:**
```csharp
[Header("Mission Display")]
missionContainer  // 미션 컨테이너
missionItemPrefab // 미션 아이템 프리팹
missionSlots[]    // 최대 3개 미션 슬롯

// 각 MissionUI 컴포넌트:
- iconImage (미션 아이콘)
- countText (남은 목표 수량, "x 15")
- progressFill (진행 바)
```

**콤보 표시:** (ComboDisplay.cs)
- 위치: 화면 상단 중앙, HUD 아래 120px
- 크기: 300x60px
- 표시: "COMBO x3!" (2콤보 이상일 때만 표시)
- 자동 숨김: 1.5초 후 페이드아웃

**레이아웃 문제:**
- 정확한 배치는 Scene 파일 의존
- SerializeField만 있고 앵커/피벗 정보 없음
- 해상도 대응 전략 불명확

### 1.5 스테이지 클리어/게임오버 팝업

**구현 파일:** `UIManager.cs` (line 45-57, 412-713)

#### 1.5.1 스테이지 클리어 팝업

**구조:**
```csharp
[Header("Stage Clear Detail")]
clearTitleText          // "STAGE X CLEAR!"
clearBaseScoreText      // 기본 점수
clearTurnBonusText      // 남은 턴 보너스 "+1000 (x5)"
clearEfficiencyBonusText // 효율 보너스
clearTotalScoreText     // 총점
starImages[]            // 별 등급 (최대 3개)
```

**애니메이션 시퀀스:** (line 473-563)
1. 팝업 등장 (0.3초 스케일 + 페이드)
2. 타이틀 표시 ("STAGE X CLEAR!")
3. 기본 점수 카운팅 (0.4초)
4. 턴 보너스 카운팅 (0.4초) + 0.15초 대기
5. 효율 보너스 카운팅 (0.3초) + 0.15초 대기
6. 총점 카운팅 (0.6초) + 스케일 펀치 (1.0 → 1.2 → 1.0)
7. 별 등급 순차 표시 (각 0.25초, EaseOutBack)

**별 등급 계산:** (ScoreManager.cs)
- 점수 / 목표점수 비율 기반
- 2.0배 이상: 3성
- 1.5배 이상: 2성
- 1.0배 이상: 1성
- 미만: 0성

#### 1.5.2 게임오버 팝업

**구조:**
```csharp
gameOverPopup
- gameOverOutButton (로비 나가기)
- gameOverBuyButton (턴 구매, +5턴)
- gameOverRetryButton (재시작)
```

**문제점:**
- 점수 브레이크다운 없음 (최종 점수만 표시 추정)
- 감정적 피드백 부족 (격려/아쉬움 메시지)
- 재도전 동기 부여 약함

#### 1.5.3 일시정지 팝업

**구조:**
```csharp
pausePopup
- pauseOutButton (로비 나가기)
- pauseHelpButton (도움말)
- pauseRetryButton (재시작)
```

**기능:**
- `Time.timeScale = 0` (게임 일시정지)
- 도움말 팝업 연동
- 외부 클릭 시 닫기 지원

### 1.6 기타 UI 요소

#### 1.6.1 콤보 디스플레이

**파일:** `ComboDisplay.cs`

**위치:** 화면 상단 중앙 (Y: -120px)

**표시 조건:** 2콤보 이상

**비주얼:**
- 2콤보: "COMBO x2", Yellow, 32px
- 3콤보: "COMBO x3", Orange, 38px, Bold
- 4콤보+: "COMBO x4!", Red-Orange, 44px, Bold + Shake

**애니메이션:**
- 등장: Scale 1.3 → 1.0 (EaseOutBack, 0.3초)
- 4콤보 이상: 셰이크 효과 (0.15초, intensity 4px)
- 자동 숨김: 1.5초 후 페이드아웃

#### 1.6.2 특수 블록 인디케이터

**현재 구현:** 없음

**관련 코드:**
- `BlockData.pendingActivation` 플래그 존재
- `HexBlock.StartWarningBlink()` / `StopWarningBlink()` 메서드 (코드 미확인)
- 특수 블록 발동 예정 시 블링크 애니메이션 추정

**개선 필요:**
- 어떤 특수 블록이 발동될지 시각적 프리뷰
- 발동 순서 명확화
- 영향 범위 하이라이트

---

## 2. 문제점 및 개선 기회

### 2.1 점수 시스템

**문제점:**
1. **점수 증가 체감 약함**
   - HUD 상단의 숫자만 변경되어 눈에 띄지 않음
   - 플로팅 팝업과 HUD 점수의 연결성 부족
2. **점수 의미 불명확**
   - "골드"인지 "점수"인지 혼재 (`goldText` vs `currentScore`)
   - 게임 내 화폐와 점수의 관계 불분명
3. **하이라이트 타이밍**
   - 점수 증가 중 골드 색상 전환이 짧음 (0.3초)
   - 큰 점수 획득 시 임팩트 부족

**개선 기회:**
- 점수 획득 시 "골드 획득" 애니메이션 강화
- 대량 점수 시 화면 효과 추가 (파티클, 플래시)
- 점수/골드 용어 통일 및 UI 분리 고려

### 2.2 턴 카운터

**문제점:**
1. **위험 상태 인지 늦음**
   - 5턴 이하부터 경고 시작 (너무 늦음)
   - 펄스 애니메이션이 미묘하여 주의 끌기 어려움
2. **진행 바 시인성**
   - 원형 진행 바(`moveProgressRing`)의 크기/위치 불명확
   - 숫자 텍스트와 분리되어 있을 가능성
3. **턴 소모 피드백**
   - 바운스 애니메이션만 있음
   - 턴 사용의 중요성 강조 부족

**개선 기회:**
- 10턴 이하부터 경고 단계 시작
- 진행 바를 숫자 주변에 원형/아크 형태로 통합
- 턴 사용 시 "-1" 팝업 또는 디미니시 효과
- 턴 0에 도달 시 극적인 애니메이션

### 2.3 아이템 UI

**문제점:**
1. **접근성**
   - 버튼 위치/크기 불명확 (Scene 의존)
   - 손가락으로 가려질 가능성
2. **사용 가이드 부족**
   - 처음 사용 시 튜토리얼 없음
   - 타겟 선택 모드 진입 시 명확한 안내 부족
3. **시각적 피드백**
   - 버튼 클릭 시 애니메이션 없음 (추정)
   - 아이템 효과 프리뷰 없음
4. **잠금 해제 알림**
   - 새 아이템 해금 시 축하 팝업 없음

**개선 기회:**
- 버튼 크기 최소 60x60dp (모바일 터치 표준)
- 클릭 시 스케일/색상 변화 애니메이션
- 타겟 선택 모드 시 반투명 오버레이 + 안내 텍스트
- 아이템 효과 범위 프리뷰 (드래그하여 영역 확인)
- 해금 시 "NEW!" 배지 + 축하 애니메이션

### 2.4 HUD 레이아웃

**문제점:**
1. **정보 밀도 불명확**
   - 점수, 턴, 스테이지, 미션, 아이템 버튼 모두 표시
   - 정보 우선순위가 불분명
2. **모바일 Safe Area 미고려**
   - 노치/홈 바 영역 침범 가능성
   - 코드에 Safe Area 처리 없음
3. **게임 영역 침범**
   - HUD가 게임 보드를 가릴 가능성
   - 특히 미션 UI가 좌우 중 어디 배치되는지 불명확
4. **동적 변화 부족**
   - 미션 완료 시 UI 변화 없음 (체크 표시만 추정)
   - 스테이지 진행에 따른 UI 진화 없음

**개선 기회:**
- 상단: 턴 + 점수 (핵심 정보)
- 좌측/우측: 미션 (세로 배치)
- 하단: 아이템 버튼 (엄지 접근 영역)
- Safe Area Inset 적용
- 미션 완료 시 축하 애니메이션 + UI 슬라이드아웃

### 2.5 팝업 시스템

**문제점:**
1. **스테이지 클리어 팝업**
   - 애니메이션이 길고 복잡함 (총 3~4초)
   - 스킵 기능 없음
   - 별 등급 의미가 모호함 (목표점수 미표시)
2. **게임오버 팝업**
   - 감정적 피드백 부족
   - 재도전 동기 부여 약함
   - 진행도 손실 경고 없음
3. **팝업 배경**
   - 어두운 오버레이만 추정
   - 게임 보드 블러 효과 없음

**개선 기회:**
- 스테이지 클리어: 탭하여 스킵 기능
- 별 등급 기준 명확히 표시 (목표점수 vs 획득점수)
- 게임오버: "아쉬워요!" 메시지 + 최고 점수 대비 표시
- 팝업 배경 블러 효과 추가
- 소셜 공유 버튼 고려

### 2.6 미션 표시

**문제점:**
1. **진행도 가시성**
   - 작은 진행 바만 있음
   - 미션 목표가 명확하지 않음
2. **완료 피드백**
   - 완료 시 애니메이션 없음
   - 보상 연출 부족
3. **미션 우선순위**
   - 여러 미션 중 어느 것이 긴급한지 불명확
   - 달성 가능성 힌트 없음

**개선 기회:**
- 미션 카드 디자인 개선 (아이콘 + 설명 + 진행 바)
- 완료 시 체크마크 애니메이션 + 파티클
- 남은 목표 수량 강조 (큰 숫자)
- 달성 가까운 미션 하이라이트

### 2.7 콤보 디스플레이

**문제점:**
1. **위치 충돌**
   - 상단 HUD 아래 120px → 미션 UI와 겹칠 가능성
2. **콤보 의미 불명확**
   - 콤보가 점수에 미치는 영향 미표시
   - 콤보 보너스 배율 숨김 (코드에만 존재)
3. **사라지는 타이밍**
   - 1.5초 대기 후 사라짐 → 너무 빠름
   - 연속 매치 시 콤보 유지 어려움

**개선 기회:**
- 위치를 화면 중앙으로 변경 (게임 보드 위)
- "x2.5 SCORE!" 같이 배율 명시
- 콤보 유지 타이머 바 추가
- 콤보 끊김 시 "COMBO BREAK!" 애니메이션

---

## 3. 구체적인 개선 제안

### 우선순위 1: 즉시 개선 (Critical)

#### 3.1.1 턴 카운터 강화

**목표:** 턴 소모의 중요성을 강조하고 위험 상태 조기 경고

**변경사항:**
1. **진행 바 통합**
   - `turnText` 주변에 원형 진행 바 배치 (도넛 차트 형태)
   - 숫자와 진행 바를 하나의 시각적 단위로 통합
2. **경고 단계 세분화**
   ```csharp
   // UIManager.cs UpdateTurnDisplay() 수정
   if (turns <= 3) {
       // Critical: 강한 빨강 + 빠른 펄스 + 셰이크
   } else if (turns <= 5) {
       // Warning: 빨강 + 펄스
   } else if (turns <= 10) {
       // Caution: 주황색 + 미세 펄스 (새로 추가)
   }
   ```
3. **턴 소모 피드백**
   - 턴 사용 시 "-1" 플로팅 텍스트 추가
   - 숫자 변화 시 컬러 플래시 (화이트 → 기본 색상)

**수정 파일:**
- `UIManager.cs` (UpdateTurnDisplay 메서드)
- `VisualConstants.cs` (새 상수 추가)

**예상 공수:** 2시간

---

#### 3.1.2 아이템 버튼 개선

**목표:** 터치 친화적 UI 및 명확한 피드백

**변경사항:**
1. **버튼 크기 표준화**
   - 최소 크기: 70x70px (모바일 터치 표준)
   - 버튼 간 여백: 10px 이상
2. **버튼 위치 고정**
   - 화면 하단 중앙 배치 (엄지 접근 영역)
   - Safe Area Bottom Inset 적용
3. **인터랙션 피드백**
   ```csharp
   // ItemButtonUI.cs 수정
   OnPointerDown: Scale 0.95 + 색상 Darken
   OnPointerUp: Scale 1.0 + 색상 복귀
   사용 시: 펄스 애니메이션 + 파티클
   ```
4. **타겟 선택 모드 개선**
   - 반투명 어두운 오버레이 추가
   - "터치하여 타겟 선택" 안내 텍스트
   - 취소 버튼 명확히 표시 (상단 X 버튼)

**새 UI 요소:**
```csharp
// UIManager.cs에 추가
[Header("Item Selection")]
[SerializeField] private GameObject itemSelectionOverlay;
[SerializeField] private Text itemSelectionGuideText;
[SerializeField] private Button itemCancelButton;
```

**수정 파일:**
- `UIManager.cs`
- `ItemManager.cs`
- Scene 파일 (버튼 재배치)

**예상 공수:** 4시간

---

#### 3.1.3 Safe Area 대응

**목표:** 노치/홈 바 영역 침범 방지

**변경사항:**
1. **SafeAreaHandler 컴포넌트 생성**
   ```csharp
   // 새 파일: Assets/Scripts/Utils/SafeAreaHandler.cs
   public class SafeAreaHandler : MonoBehaviour
   {
       private RectTransform rectTransform;

       void Start()
       {
           rectTransform = GetComponent<RectTransform>();
           ApplySafeArea();
       }

       void ApplySafeArea()
       {
           Rect safeArea = Screen.safeArea;
           Vector2 anchorMin = safeArea.position;
           Vector2 anchorMax = anchorMin + safeArea.size;

           anchorMin.x /= Screen.width;
           anchorMin.y /= Screen.height;
           anchorMax.x /= Screen.width;
           anchorMax.y /= Screen.height;

           rectTransform.anchorMin = anchorMin;
           rectTransform.anchorMax = anchorMax;
       }
   }
   ```
2. **HUD 루트에 적용**
   - 상단 HUD 패널에 SafeAreaHandler 추가
   - 하단 아이템 버튼 패널에 SafeAreaHandler 추가

**수정 파일:**
- 새 파일: `SafeAreaHandler.cs`
- Scene 파일 (HUD 구조 조정)

**예상 공수:** 1시간

---

### 우선순위 2: 중요 개선 (High)

#### 3.2.1 점수 시스템 재설계

**목표:** 점수 획득 체감 강화 및 의미 명확화

**변경사항:**
1. **용어 통일**
   - "골드"로 통일 (게임 내 화폐)
   - HUD 텍스트: "GOLD: 1,234"
   - `goldText` 변수명 유지
2. **HUD 점수 강화**
   ```csharp
   // UIManager.cs ScoreCountAnimation 수정
   - 증가 중 골드 아이콘 펄스
   - 1000점 이상 획득 시 골드 아이콘 파티클 효과
   - 하이라이트 시간 0.3초 → 0.6초 연장
   ```
3. **대량 획득 연출**
   ```csharp
   // 3000점 이상 획득 시:
   - 화면 플래시 (옅은 골드 색상)
   - 골드 아이콘 셰이크
   - 특수 사운드 효과
   ```

**수정 파일:**
- `UIManager.cs`
- `ScorePopupManager.cs` (티어 색상 조정)

**예상 공수:** 3시간

---

#### 3.2.2 미션 UI 개선

**목표:** 미션 목표 명확화 및 완료 연출 강화

**변경사항:**
1. **미션 카드 재디자인**
   ```
   ┌──────────────────────────┐
   │ 🔴 Red Gem   [====  ] 12 │
   │                           │
   └──────────────────────────┘
   ```
   - 아이콘 크기 증가 (32x32 → 48x48)
   - 남은 목표 수량 강조 (큰 폰트)
   - 진행 바 두께 증가
2. **완료 애니메이션**
   ```csharp
   // MissionUI.cs에 추가
   public void PlayCompleteAnimation()
   {
       // 1. 체크마크 애니메이션 (Scale 0→1.2→1.0)
       // 2. 골드 파티클 방출
       // 3. 사운드 효과
       // 4. 0.5초 후 슬라이드아웃
   }
   ```
3. **우선순위 하이라이트**
   - 50% 이상 달성 미션 테두리 골드 글로우
   - 90% 이상 달성 미션 펄스 애니메이션

**새 UI 요소:**
```csharp
// MissionUI.cs 수정
[SerializeField] private Image checkMarkImage;
[SerializeField] private ParticleSystem completeParticle;
[SerializeField] private Image glowBorder;
```

**수정 파일:**
- `UIManager.cs` (MissionUI 클래스)
- `StageManager.cs` (미션 완료 이벤트)

**예상 공수:** 5시간

---

#### 3.2.3 콤보 디스플레이 개선

**목표:** 콤보 의미 명확화 및 유지 동기 부여

**변경사항:**
1. **위치 변경**
   - 상단 HUD 아래 → 화면 중앙 (게임 보드 위)
   - Y 위치: 화면 중앙에서 +200px
2. **배율 표시**
   ```csharp
   // ComboDisplay.cs 수정
   "COMBO x3" → "COMBO x3\n+50% SCORE"
   2줄 텍스트로 변경, 배율 명시
   ```
3. **타이머 바 추가**
   - 콤보 텍스트 아래 가로 진행 바 추가
   - 남은 시간 시각화 (3초 → 0초)
   - 0초 도달 시 빨간색 → 사라짐
4. **콤보 브레이크 연출**
   ```csharp
   // 콤보 리셋 시:
   if (currentCombo >= 3) {
       ShowComboBreak(); // "COMBO BREAK!" 텍스트 + 흔들림
   }
   ```

**새 UI 요소:**
```csharp
// ComboDisplay.cs에 추가
[SerializeField] private Image comboTimerBar;
private Coroutine timerBarCoroutine;
```

**수정 파일:**
- `ComboDisplay.cs`
- `ScoreManager.cs` (콤보 리셋 이벤트 추가)

**예상 공수:** 4시간

---

### 우선순위 3: 향상 개선 (Medium)

#### 3.3.1 스테이지 클리어 팝업 개선

**목표:** 애니메이션 간소화 및 스킵 기능 추가

**변경사항:**
1. **스킵 기능**
   ```csharp
   // UIManager.cs AnimateStageClearBreakdown 수정
   bool skipRequested = false;

   void Update() {
       if (Input.GetMouseButtonDown(0)) skipRequested = true;
   }

   // 각 WaitForSeconds를 skipRequested 체크로 대체
   while (elapsed < duration && !skipRequested) { ... }
   ```
2. **별 등급 기준 표시**
   - 팝업 하단에 "목표: 15,000" 텍스트 추가
   - 별 등급별 점수 구간 표시
   ```
   ★☆☆ 15,000+
   ★★☆ 22,500+
   ★★★ 30,000+
   ```
3. **애니메이션 속도 조정**
   - 전체 시간 3~4초 → 2초로 단축
   - 카운팅 속도 증가 (0.4초 → 0.25초)

**수정 파일:**
- `UIManager.cs` (AnimateStageClearBreakdown)
- Scene 파일 (별 등급 기준 텍스트 추가)

**예상 공수:** 3시간

---

#### 3.3.2 게임오버 팝업 개선

**목표:** 감정적 피드백 및 재도전 동기 부여

**변경사항:**
1. **메시지 추가**
   ```csharp
   // 상황별 메시지
   if (currentScore < highScore * 0.5) {
       message = "조금만 더 노력하면 돼요!";
   } else if (currentScore < highScore * 0.8) {
       message = "아쉽네요! 거의 다 왔어요!";
   } else {
       message = "새로운 기록까지 조금만 더!";
   }
   ```
2. **진행도 표시**
   - 최고 점수 대비 현재 점수 비율 바
   - "최고 기록의 73% 달성!"
3. **버튼 재배치**
   ```
   [  턴 +5  ] (강조, 큰 버튼)
   [ 재시작 ] [ 나가기 ] (작은 버튼)
   ```

**새 UI 요소:**
```csharp
// UIManager.cs에 추가
[SerializeField] private Text gameOverMessageText;
[SerializeField] private Image gameOverProgressBar;
[SerializeField] private Text gameOverProgressText;
```

**수정 파일:**
- `UIManager.cs`
- `GameManager.cs` (게임오버 시 점수 데이터 전달)

**예상 공수:** 4시간

---

#### 3.3.3 특수 블록 인디케이터

**목표:** 특수 블록 발동 예정 시 시각적 가이드 제공

**변경사항:**
1. **발동 예정 표시**
   - `pendingActivation` 플래그가 있는 블록에 오렌지 테두리
   - 테두리 펄스 애니메이션
2. **영향 범위 프리뷰**
   - 특수 블록 터치 시 영향 받을 블록 하이라이트
   - 드릴: 직선 방향 블록 반투명 표시
   - 폭탄: 주변 블록 반투명 표시
   - 레이저: 6방향 블록 반투명 표시
3. **발동 순서 번호**
   - 여러 특수 블록이 연쇄 발동될 때 "1", "2", "3" 숫자 표시

**구현 위치:**
- `HexBlock.cs` (하이라이트 메서드 추가)
- `DrillBlockSystem.cs`, `BombBlockSystem.cs` 등 (프리뷰 메서드)

**수정 파일:**
- `HexBlock.cs`
- 각 특수 블록 시스템 파일

**예상 공수:** 6시간

---

### 우선순위 4: 추가 기능 (Low)

#### 3.4.1 HUD 애니메이션 강화

**목표:** 정적인 HUD를 동적으로 만들기

**변경사항:**
1. **스테이지 시작 애니메이션**
   - "STAGE 5" 텍스트가 화면 중앙에서 등장 후 상단으로 슬라이드
2. **미션 완료 애니메이션**
   - 미션 완료 시 해당 슬롯이 축소되며 사라짐
   - 나머지 미션이 중앙으로 재배치
3. **점수 마일스톤**
   - 10,000점 단위로 "10K REACHED!" 배지 표시
4. **턴 추가 애니메이션**
   - 아이템 사용 시 "+5" 플로팅 텍스트 + 진행 바 증가 애니메이션

**수정 파일:**
- `UIManager.cs`
- `GameManager.cs`

**예상 공수:** 5시간

---

#### 3.4.2 설정 UI

**목표:** 사운드, 진동, 언어 등 설정 기능 제공

**변경사항:**
1. **설정 팝업 생성**
   ```
   ┌─────────────────────┐
   │      SETTINGS       │
   ├─────────────────────┤
   │ BGM        [====  ] │
   │ SFX        [======] │
   │ Vibration  [ ON ]   │
   │ Language   [한국어▼]│
   └─────────────────────┘
   ```
2. **설정 저장**
   - PlayerPrefs에 저장
   - AudioManager 연동

**새 파일:**
- `Assets/Scripts/Managers/SettingsManager.cs`
- Scene에 설정 팝업 추가

**예상 공수:** 6시간

---

#### 3.4.3 튜토리얼 시스템

**목표:** 신규 플레이어 온보딩 개선

**변경사항:**
1. **첫 플레이 가이드**
   - 회전 방법 설명 (손가락 애니메이션)
   - 매칭 규칙 설명 (3개 이상)
2. **특수 블록 소개**
   - 각 특수 블록 첫 등장 시 설명 팝업
3. **아이템 튜토리얼**
   - 아이템 해금 시 사용법 가이드

**새 파일:**
- `Assets/Scripts/Managers/TutorialManager.cs`

**예상 공수:** 8시간

---

## 4. 모바일 UX 고려사항

### 4.1 터치 영역 최적화

**현재 문제:**
- 버튼 크기 미정의 (Scene 의존)
- 손가락으로 게임 보드 가림

**권장사항:**
1. **최소 터치 크기**
   - 모든 버튼: 60x60dp 이상 (Apple: 44pt, Android: 48dp)
   - 중요 버튼 (아이템): 70x70dp 이상
2. **버튼 배치**
   - 상단: 정보 표시 (터치 불필요)
   - 하단: 액션 버튼 (엄지 접근)
   - 좌우: 미션 표시 (터치 불필요)
3. **손가림 방지**
   - 게임 보드를 화면 상단 1/3에 배치
   - 하단은 UI 전용 영역으로 예약

### 4.2 해상도 대응

**현재 문제:**
- Safe Area 미고려
- 다양한 종횡비 미테스트 (16:9, 18:9, 19.5:9, 4:3)

**권장사항:**
1. **Safe Area 필수 적용**
   - 모든 HUD 요소에 SafeAreaHandler 적용
2. **캔버스 설정**
   ```
   Canvas Scaler:
   - UI Scale Mode: Scale With Screen Size
   - Reference Resolution: 1080x1920 (9:16)
   - Match: 0.5 (Width/Height 균형)
   ```
3. **앵커 전략**
   - 상단 HUD: Top-Center 앵커
   - 하단 버튼: Bottom-Center 앵커
   - 미션: Left/Right 앵커

### 4.3 성능 최적화

**현재 문제:**
- 많은 코루틴 및 애니메이션
- 점수 팝업 풀링 (12개, 확장 가능) → 메모리 관리 필요

**권장사항:**
1. **Draw Call 최소화**
   - 아틀라스 사용 (UI 스프라이트 통합)
   - Canvas 분리 (정적 UI / 동적 UI)
2. **애니메이션 최적화**
   - DOTween 사용 고려 (코루틴 대신)
   - 오브젝트 풀링 확대 (파티클 등)
3. **메모리 관리**
   - 팝업 풀 크기 제한 (최대 20개)
   - 사용하지 않는 파티클 시스템 비활성화

### 4.4 입력 제스처

**현재 구현:**
- 회전만 지원 (드래그)

**추가 고려:**
1. **스와이프 제스처**
   - 빠른 회전 (플릭 제스처)
2. **핀치 줌**
   - 보드 확대/축소 (선택 사항)
3. **롱프레스**
   - 특수 블록 프리뷰 (누르고 있으면 영향 범위 표시)

### 4.5 접근성

**권장사항:**
1. **색맹 모드**
   - 블록에 아이콘/패턴 추가 (색상만 의존하지 않음)
2. **진동 피드백**
   - 매치 성공: 짧은 진동
   - 특수 블록 발동: 강한 진동
   - 게임오버: 긴 진동
3. **텍스트 크기**
   - 최소 폰트 크기: 16sp
   - 중요 정보 (턴, 점수): 28sp 이상

---

## 5. 우선순위별 로드맵

### Phase 1: 즉시 개선 (1주)
- [x] 3.1.1 턴 카운터 강화 (2h)
- [x] 3.1.2 아이템 버튼 개선 (4h)
- [x] 3.1.3 Safe Area 대응 (1h)
- **총 공수: 7시간 (~1일)**

### Phase 2: 중요 개선 (2주)
- [x] 3.2.1 점수 시스템 재설계 (3h)
- [x] 3.2.2 미션 UI 개선 (5h)
- [x] 3.2.3 콤보 디스플레이 개선 (4h)
- **총 공수: 12시간 (~1.5일)**

### Phase 3: 향상 개선 (2주)
- [x] 3.3.1 스테이지 클리어 팝업 개선 (3h)
- [x] 3.3.2 게임오버 팝업 개선 (4h)
- [x] 3.3.3 특수 블록 인디케이터 (6h)
- **총 공수: 13시간 (~1.5일)**

### Phase 4: 추가 기능 (4주)
- [ ] 3.4.1 HUD 애니메이션 강화 (5h)
- [ ] 3.4.2 설정 UI (6h)
- [ ] 3.4.3 튜토리얼 시스템 (8h)
- **총 공수: 19시간 (~2.5일)**

### 전체 예상 공수
- **총합:** 51시간 (~6.5일)
- **권장 일정:** 8주 (여유 포함)

---

## 6. 추가 권장사항

### 6.1 UI/UX 테스트

1. **사용성 테스트**
   - 5명 이상의 신규 플레이어 관찰
   - 첫 5분 플레이 영상 녹화
   - 혼란스러운 UI 요소 파악
2. **A/B 테스트**
   - 턴 경고 타이밍 (5턴 vs 10턴)
   - 콤보 표시 위치 (상단 vs 중앙)
   - 아이템 버튼 배치 (하단 vs 좌우)

### 6.2 애널리틱스 추가

**추적 지표:**
- 평균 플레이 시간
- 스테이지별 클리어율
- 아이템 사용률
- 게임오버 전 평균 턴 수
- 콤보 최대 도달 수

**구현:**
```csharp
// Analytics 이벤트 예시
Analytics.CustomEvent("stage_clear", new Dictionary<string, object> {
    { "stage", currentStage },
    { "turns_left", currentTurns },
    { "score", currentScore },
    { "time", playTime }
});
```

### 6.3 UI 일관성 가이드

**색상 팔레트:**
- Primary: `#FFD700` (Gold) - 점수, 보상
- Warning: `#FF6600` (Orange) - 주의 (10턴 이하)
- Danger: `#FF0000` (Red) - 위험 (3턴 이하)
- Success: `#00CC66` (Green) - 완료, 성공
- Neutral: `#FFFFFF` (White) - 기본 텍스트

**폰트 크기:**
- Huge: 48sp (타이틀)
- Large: 36sp (중요 숫자)
- Medium: 24sp (일반 정보)
- Small: 16sp (부가 정보)

**애니메이션 타이밍:**
- Instant: 0초 (정보 업데이트)
- Fast: 0.15초 (버튼 클릭)
- Normal: 0.3초 (팝업 등장)
- Slow: 0.5초 (점수 카운팅)

---

## 7. 결론

**핵심 발견:**
1. **기본 UI 구조는 견고함** - UIManager, ScoreManager, StageManager 분리가 명확
2. **애니메이션 시스템 우수** - VisualConstants, EaseOut 함수 등 체계적
3. **주요 개선 필요 영역:**
   - 터치 친화성 (버튼 크기, 위치)
   - Safe Area 대응 (노치 영역)
   - 정보 계층화 (중요도별 시각적 강조)
   - 감정적 피드백 (성공/실패 연출)

**권장 순서:**
1. **1주차:** Safe Area + 아이템 버튼 개선 (모바일 기본)
2. **2주차:** 턴 카운터 + 점수 시스템 (핵심 게임플레이)
3. **3-4주차:** 미션/콤보 UI (몰입도 향상)
4. **5-6주차:** 팝업 개선 (감정적 피드백)
5. **7-8주차:** 추가 기능 (설정, 튜토리얼)

**최우선 과제 (Phase 1):**
- Safe Area 대응 (필수)
- 아이템 버튼 터치 영역 확대 (필수)
- 턴 카운터 위험 상태 강화 (게임 밸런스)

이 분석 보고서를 바탕으로 단계적으로 UI/UX를 개선하면 플레이어 만족도와 리텐션이 크게 향상될 것으로 기대됩니다.

---

**작성 완료**
다음 단계: Phase 1 개선 작업 착수 또는 UI Mock-up 제작
