# HexaPuzzle VFX 품질 업그레이드 로드맵
## Art Director 제안서 v1.0

---

# 1. 리서치 요약: 2025-2026 모바일 퍼즐 VFX 트렌드

## 1.1 탑 퍼즐 게임 분석 (Royal Match, Candy Crush, Toon Blast)

Royal Match가 현재 모바일 퍼즐 시장의 벤치마크. 핵심 특징:
- **속도감**: 파워업 애니메이션이 짧고 유려하여 게임 흐름을 방해하지 않음
- **만족감**: 파편이 "딱 적당한 양"으로 화려하되 보드를 가리지 않음
- **직관성**: VFX가 게임 상태를 명확히 전달 (매칭 성공, 콤보, 파워업 준비 상태)
- **동시성**: 매칭과 연쇄가 빠르게 동시 진행되어 시각적 임팩트 극대화

## 1.2 핵심 Juice 기법 (업계 공통)

| 기법 | 설명 | 현재 구현 상태 |
|------|------|----------------|
| Squash & Stretch | 디즈니 12원칙 기반 변형 | O (squeeze+shrink) |
| Anticipation | 큰 액션 전 사전 신호 | △ (폭탄 compression만) |
| Follow-through | 액션 후 잔여 효과 | X |
| Screen Shake | 충격 전달 | O (3단계) |
| Hit Stop/Freeze | 순간 정지로 임팩트 강조 | X |
| Color Flash | 순간 색상 변화로 피드백 | △ (플래시만) |
| Overshoot | 목표를 넘었다 돌아오기 | O (spawn pop) |
| Secondary Motion | 주동작에 따라오는 부차 움직임 | X |
| Ambient/Idle | 대기 중 미세 애니메이션 | X |
| Bloom 시뮬레이션 | 밝은 오브젝트 주변 빛번짐 | X |

## 1.3 UI.Image 기반 제약 내 가능한 기법

쉐이더/파티클 시스템 없이 UI.Image GameObject만으로 구현 가능한 것:
- 반투명 흰색/컬러 오버레이로 Bloom 시뮬레이션
- 여러 레이어 중첩으로 글로우 효과
- Scale/Position/Rotation/Color 코루틴으로 모든 모션
- RectTransform sizeDelta 조작으로 라인/빔/웨이브
- 중첩된 Image들의 알파 합성으로 발광 효과

---

# 2. 제안 목록: 카테고리별 구체 사항

---

## 2.1 Anticipation (사전 신호 효과)

### P-01: 특수블록 활성화 전 "충전" 효과 [우선순위: 높음]

**무엇인가**: 특수 블록이 매칭에 포함되어 발동 직전, 0.08~0.12초 동안 블록이 안쪽으로 수축했다가 터지는 "숨 들이쉬기" 효과. 현재 폭탄에만 BombCompressionDuration(0.05s)이 있으나, 모든 특수 블록에 적용하고 더 극적으로 강화.

**왜 좋은가**: Royal Match가 이 기법을 모든 파워업에 적용. 플레이어에게 "곧 뭔가 터진다"는 기대감을 주어 임팩트를 2배로 느끼게 함.

**적용 시스템**: Drill, Bomb, Laser, Donut, XBlock (전체)

**구현 방식**:
- VisualConstants에 추가:
  - `PreFireCompressionDuration = 0.1f` (현재 Bomb의 0.05f보다 길게)
  - `PreFireScaleMin = 0.85f` (안으로 줄어듬)
  - `PreFireScaleMax = 1.12f` (살짝 팽창 후 폭발)
  - `PreFireBrightenAmount = 0.25f` (밝아짐)
- 각 시스템의 코루틴 시작부에 공통 PreFire 단계 추가:
  1. 0~50% 구간: Scale 1.0 -> 0.85 (EaseInQuad) + 색상 밝기 +0.25
  2. 50~100% 구간: Scale 0.85 -> 1.12 (EaseOutCubic)
  3. 이후 기존 폭발/발사 시작
- 동시에 주변 블록 6개가 2px씩 바깥으로 밀렸다 돌아오는 "반동" 추가

**성능**: GameObject 추가 없음 (기존 블록의 Scale/Color만 조작). 코루틴 1개 추가.

---

### P-02: 매칭 감지 시 "하이라이트 펄스" [우선순위: 높음]

**무엇인가**: 매칭이 확인된 순간(matchHighlightDuration 0.15s 동안), 매칭 블록들이 동시에 한 번 "펄스"하는 효과. 현재는 SetMatched(true)로 시각 표시만 함.

**왜 좋은가**: Candy Crush 시리즈의 핵심 Juice. 매칭 블록이 "반짝" 빛나면서 약간 커졌다 돌아오면 매칭을 더 명확히 인지하고 만족감 증가.

**적용 시스템**: BlockRemovalSystem.ProcessMatchesInline

**구현 방식**:
- 매칭된 블록 SetMatched(true) 직후:
  1. 각 블록 Scale: 1.0 -> 1.08 -> 1.0 (EaseOutElastic, 0.12s)
  2. 블록 위에 흰색 반투명 오버레이 Image 생성 (sizeDelta = 블록 크기)
     - Color: (1, 1, 1, 0.35f) -> (1, 1, 1, 0f) fade over 0.12s
  3. 블록 간 stagger: 중앙 블록 먼저, 외곽으로 0.015s 딜레이

**성능**: 매칭 블록당 1 GameObject (흰색 오버레이), lifetime 0.12s. 일반적 매칭 3~5개 = 최대 5 GO.

---

### P-03: 콤보 빌드업 시각 에스컬레이션 [우선순위: 중간]

**무엇인가**: 캐스케이드 depth가 깊어질수록 (연쇄 2, 3, 4...) 화면 전체에 긴장감이 쌓이는 비주얼. 현재 GetCascadeMultiplier로 파편 수만 증가.

**왜 좋은가**: Toon Blast의 핵심 전략. 연쇄가 이어질수록 보드 경계에 빛 테두리가 나타나고, 화면이 미세하게 줌인되어 "대단한 일이 벌어지고 있다"는 느낌 전달.

**적용 시스템**: BlockRemovalSystem (CascadeWithPendingLoop 내부)

**구현 방식**:
- 캐스케이드 depth에 따른 시각 에스컬레이션:
  - depth 1: 없음 (기본)
  - depth 2: 보드 테두리에 반투명 흰색 rect 4개 (상하좌우), alpha 0.1, 미세 펄스
  - depth 3: 테두리 alpha 0.2 + 보드 Scale 1.0 -> 1.015 (미세 줌인)
  - depth 4+: 테두리 alpha 0.3 + 보드 Scale 1.03 + 배경 색온도 약간 따뜻하게
- VisualConstants 추가:
  - `CascadeBorderAlphaPerDepth = 0.1f`
  - `CascadeZoomPerDepth = 0.015f`
  - `CascadeZoomMax = 1.05f`
- 캐스케이드 종료 시 모든 효과를 0.3s에 걸쳐 EaseOutCubic으로 해제

**성능**: 보드 테두리 rect 4개 (상시 존재, alpha 0일 때 렌더링 무시) = 4 GO 상시.

---

## 2.2 Impact (충격 / 파괴 순간 효과)

### I-01: Hit Stop (순간 정지) [우선순위: 높음]

**무엇인가**: 특수 블록 폭발 순간, 게임 전체가 1~3프레임(0.03~0.05s) 동안 Time.timeScale = 0으로 "멈추는" 효과. 이후 약간 슬로모(timeScale 0.5)로 0.05s 진행 후 원래 속도 복귀.

**왜 좋은가**: 격투 게임에서 검증된 "히트스톱" 기법. 순간 정지가 뇌에 "방금 강력한 일이 발생했다"고 각인. Royal Match의 파워업이 "무겁게" 느껴지는 핵심 이유.

**적용 시스템**: Bomb(0.04s stop), Laser(0.035s stop), Donut(0.03s stop), XBlock(0.03s stop), Drill(발사 시 0.02s stop)

**구현 방식**:
- VisualConstants 추가:
  - `HitStopDuration_Large = 0.04f` (Bomb, Laser)
  - `HitStopDuration_Medium = 0.03f` (Donut, XBlock)
  - `HitStopDuration_Small = 0.02f` (Drill)
  - `HitStopSlowMoDuration = 0.05f`
  - `HitStopSlowMoScale = 0.4f`
- 구현 코루틴 (VisualConstants 또는 별도 유틸):
  ```
  // 의사코드 (실제 코드 아님)
  Time.timeScale = 0;
  yield return WaitForSecondsRealtime(stopDuration);
  Time.timeScale = slowMoScale;
  yield return WaitForSecondsRealtime(slowMoDuration);
  Time.timeScale = 1;
  ```
- 주의: WaitForSeconds는 timeScale=0에서 멈추므로 반드시 WaitForSecondsRealtime 사용
- 연속 HitStop이 겹치지 않도록 cooldown 0.3s 적용

**성능**: GameObject 추가 없음. timeScale 조작만.

---

### I-02: 파괴 시 "화이트 플래시 오버레이" [우선순위: 높음]

**무엇인가**: 블록이 파괴되는 정확한 순간, 블록 위치에 1프레임 동안 순백색 사각형이 나타났다 빠르게 사라지는 효과. 현재는 squeeze+shrink만 있어 "사라지는" 느낌만 있고 "터지는" 느낌이 약함.

**왜 좋은가**: 스프라이트 플래시는 게임 Juice의 가장 기본이자 효과 대비 비용이 가장 낮은 기법. "화면이 잠깐 번쩍"하면 파괴가 훨씬 시원하게 느껴짐.

**적용 시스템**: 모든 DestroyBlockWith* 함수 (Drill/Bomb/Laser/Donut/XBlock + 일반 AnimateRemove)

**구현 방식**:
- VisualConstants 추가:
  - `DestroyFlashDuration = 0.06f`
  - `DestroyFlashAlpha = 0.7f`
  - `DestroyFlashSize = 1.3f` (블록 크기 대비 배율)
- 각 파괴 함수 시작부에:
  1. 흰색 Image 생성 (block.transform 자식)
  2. sizeDelta = 블록 크기 * 1.3
  3. Color (1, 1, 1, 0.7f) -> (1, 1, 1, 0f) over 0.06s (EaseOutQuart)
  4. 동시에 Scale 1.0 -> 1.5 확장
  5. Destroy(flashObj)

**성능**: 블록당 1 GO, lifetime 0.06s. 동시 최대 6개 = 6 GO (0.06s 후 소멸).

---

### I-03: 폭탄 이중 충격파 개선 - "디스토션 링" [우선순위: 중간]

**무엇인가**: 현재 폭탄의 2중 충격파(ring1, ring2)에 추가하여, 충격파가 지나가는 영역의 블록들이 잠깐 바깥으로 밀렸다 돌아오는 물리적 반응 추가.

**왜 좋은가**: 충격파가 "공기를 밀어내는" 느낌. Royal Match의 Rocket이 지나갈 때 주변 블록이 출렁이는 것과 동일한 원리.

**적용 시스템**: BombBlockSystem, LaserBlockSystem (중앙 폭발 시)

**구현 방식**:
- 충격파 타이밍에 맞춰, 폭발 중심에서 반경 내 모든 블록에 대해:
  1. 중심에서 블록 방향으로 `pushDistance = 5px * (1 - distance/maxRadius)`
  2. 0.1s에 걸쳐 밀렸다가 0.15s에 걸쳐 EaseOutElastic으로 원위치
  3. 밀리는 동안 Scale도 0.95까지 축소 -> 1.05 overshoot -> 1.0 복귀
- VisualConstants 추가:
  - `ShockwaveDisplacementMax = 5f` (px)
  - `ShockwaveDisplacementRadius = 200f` (px)
  - `ShockwaveRecoverDuration = 0.15f`

**성능**: GameObject 추가 없음 (기존 블록 Transform만 조작). 최대 ~18개 블록 동시 코루틴.

---

### I-04: 드릴 관통 시 "트레일 잔상" [우선순위: 중간]

**무엇인가**: 드릴 투사체가 지나간 경로에 0.2s 동안 남아있는 반투명 컬러 잔상 라인. 현재는 투사체만 이동하고 경로 흔적이 없음.

**왜 좋은가**: 드릴의 방향성과 관통감을 시각적으로 강조. "레이저처럼 쭉 뚫고 간다"는 만족감 배가.

**적용 시스템**: DrillBlockSystem.DrillLineWithProjectile

**구현 방식**:
- 투사체가 한 블록씩 이동할 때마다 이전 위치~현재 위치 사이에:
  1. 가느다란 Image (너비 4px, 길이 = 이동 거리) 생성
  2. 드릴 색상, alpha 0.5
  3. 0.2s에 걸쳐 alpha 0 + 너비 0px로 fade (EaseOutCubic)
  4. Destroy
- VisualConstants 추가:
  - `DrillTrailWidth = 4f`
  - `DrillTrailAlpha = 0.5f`
  - `DrillTrailFadeDuration = 0.2f`

**성능**: 타겟 블록 수만큼 GO 생성 (보통 3~8개), lifetime 0.2s.

---

### I-05: 레이저 빔 "두께 진동 + 종단 플레어" [우선순위: 낮음]

**무엇인가**: 현재 레이저 빔의 shimmer(width 진동)를 더 극적으로 만들고, 빔 끝 부분에 밝은 원형 플레어 추가.

**왜 좋은가**: 레이저가 "에너지 빔"처럼 살아있는 느낌. 끝부분 플레어는 "빔이 사라지는 지점"을 표시.

**적용 시스템**: LaserBlockSystem.LaserBeamEffect

**구현 방식**:
- 기존 shimmer 진폭 0.15 -> 0.25 + 주파수 30Hz -> 45Hz
- 빔 끝 지점에 원형 Image (16x16px):
  - Color: 흰색 alpha 0.6
  - Scale 펄스: 1.0~1.4 at 20Hz
  - 빔 페이드와 동시 소멸
- VisualConstants 추가:
  - `LaserBeamShimmerAmplitude = 0.25f`
  - `LaserBeamShimmerFrequency = 45f`
  - `LaserBeamEndFlareSize = 16f`

**성능**: 빔당 1 GO 추가 (끝 플레어). 6빔 = 6 GO, lifetime = beamDuration.

---

## 2.3 Follow-through (잔여 / 여운 효과)

### F-01: "고스트 이미지" 잔상 [우선순위: 높음]

**무엇인가**: 블록이 파괴된 후 0.15s 동안 해당 위치에 블록 색상의 반투명 잔상이 남았다가 서서히 사라지는 효과. 현재는 블록이 squeeze+shrink 후 즉시 ClearData되어 "뚝" 끊기는 느낌.

**왜 좋은가**: 인간의 시각 잔상(afterimage)을 시뮬레이션. 파괴가 더 "부드럽고 자연스러운" 느낌. Royal Match에서 블록이 사라질 때 잠깐 흔적이 남는 것과 동일.

**적용 시스템**: 모든 블록 파괴 (AnimateRemove, 각 시스템의 DestroyBlockWith*)

**구현 방식**:
- VisualConstants 추가:
  - `GhostImageDuration = 0.15f`
  - `GhostImageInitialAlpha = 0.35f`
  - `GhostImageScaleStart = 0.8f`
  - `GhostImageScaleEnd = 1.2f`
- 파괴 애니메이션 완료 직전(ClearData 전):
  1. 블록 위치에 블록 색상의 Image 생성 (effectParent 자식)
  2. sizeDelta = 블록 크기 * 0.8
  3. alpha 0.35 -> 0 (EaseOutCubic, 0.15s)
  4. Scale 0.8 -> 1.2 (확산하며 사라짐)
  5. Destroy

**성능**: 파괴 블록당 1 GO, lifetime 0.15s. 폭탄 6개 동시 = 6 GO.

---

### F-02: 낙하 착지 시 "더스트 클라우드" [우선순위: 높음]

**무엇인가**: 블록이 낙하하여 최종 위치에 도착할 때, 블록 하단 양쪽에서 작은 먼지/안개가 퍼지는 효과. 현재 SquashEffect(0.08s)만 있음.

**왜 좋은가**: 낙하에 "무게감"과 "물리적 존재감" 부여. Toon Blast의 블록 낙하 착지가 만족스러운 핵심 이유.

**적용 시스템**: BlockRemovalSystem.AnimateFall (bounceCount == 0, 최초 착지 시)

**구현 방식**:
- VisualConstants 추가:
  - `LandingDustCount = 2` (좌우 1개씩)
  - `LandingDustSize = 8f`
  - `LandingDustSpreadX = 15f`
  - `LandingDustSpeedY = 30f`
  - `LandingDustDuration = 0.2f`
  - `LandingDustAlpha = 0.25f`
- 착지 순간 (velocity가 bounceThreshold 이상일 때만):
  1. 블록 하단 좌/우에 각각 1개씩 작은 Image 생성
  2. Color: (0.9, 0.9, 0.9, 0.25) - 연한 회색
  3. 좌측 것은 (-15, 0) 방향, 우측은 (15, 0) 방향으로 이동
  4. 동시에 위로 30px/s 이동, Scale 0.5 -> 1.0 확장
  5. 0.2s에 걸쳐 alpha 0으로 fade, Destroy
- 낙하 속도에 비례하여 먼지 크기/알파 조절:
  - dustScale = Mathf.Clamp01(Mathf.Abs(velocity) / maxFallSpeed) * 1.5f

**성능**: 블록당 2 GO, lifetime 0.2s. 동시 낙하 8개 = 16 GO (빠르게 소멸).

---

### F-03: 특수블록 발동 후 "여진 웨이브" [우선순위: 중간]

**무엇인가**: Bomb/Laser 폭발 완료 후 0.3s 뒤에 한 번 더 작은 충격파가 퍼지는 "여진" 효과.

**왜 좋은가**: 큰 폭발 후 "끝났다"는 마무리감 부여. 영화에서 폭발 후 먼지가 가라앉는 것과 같은 원리.

**적용 시스템**: BombBlockSystem, LaserBlockSystem

**구현 방식**:
- 폭발 이펙트 코루틴 끝에 0.3s WaitForSeconds 후:
  1. 중앙에서 충격파 링 1개 (WaveLarge의 60% 크기, 50% alpha)
  2. 확장 속도는 기존 대비 1.5x (더 빠르게 퍼짐)
  3. duration 0.2s
- VisualConstants 추가:
  - `AftershockDelay = 0.3f`
  - `AftershockScaleRatio = 0.6f`
  - `AftershockAlphaRatio = 0.5f`

**성능**: 1 GO, lifetime 0.2s.

---

## 2.4 Ambient / Idle (대기 상태 효과)

### A-01: 특수블록 "호흡" 애니메이션 [우선순위: 높음]

**무엇인가**: 보드 위의 특수 블록(Drill, Bomb, Laser, Donut, XBlock)이 대기 중일 때 천천히 Scale이 오르락내리락하고 아이콘이 미세하게 빛나는 효과.

**왜 좋은가**: 특수 블록이 "살아있는 에너지 덩어리"처럼 느껴짐. 플레이어가 특수 블록의 존재를 더 의식하고, "이걸 터뜨려야지"라는 욕구 자극.

**적용 시스템**: HexBlock (특수 블록 상태일 때 자체 Update 또는 코루틴)

**구현 방식**:
- VisualConstants 추가:
  - `IdleBreathSpeed = 2.5f` (Hz)
  - `IdleBreathScaleMin = 0.97f`
  - `IdleBreathScaleMax = 1.03f`
  - `IdleGlowPulseSpeed = 3.0f` (Hz)
  - `IdleGlowAlphaMin = 0.0f`
  - `IdleGlowAlphaMax = 0.15f`
- HexBlock에 특수 블록 전용 코루틴:
  1. Scale: Mathf.Lerp(0.97, 1.03, (Mathf.Sin(time * 2.5f * 2PI) + 1) * 0.5f)
  2. 블록 위에 블록색 반투명 Image 오버레이 (상시 존재):
     - alpha: Mathf.Lerp(0.0, 0.15, (Mathf.Sin(time * 3.0 * 2PI) + 1) * 0.5f)
     - sizeDelta = 블록 크기 * 1.1 (약간 큰 글로우)
  3. SetBlockData에서 특수블록 감지 시 코루틴 시작, ClearData에서 중지

**성능**: 특수 블록당 1 GO (글로우 오버레이), 상시 존재. 보드에 특수 블록 보통 0~3개 = 최대 3 GO.

---

### A-02: 일반 블록 "미세 반짝임" (Shimmer) [우선순위: 낮음]

**무엇인가**: 보드의 일반 블록들이 랜덤하게 아주 가끔 "반짝" 빛나는 효과. 2~4초에 한 번, 랜덤 블록 1개가 0.3s 동안 하이라이트.

**왜 좋은가**: 보드가 정적으로 느껴지는 것을 방지. "보석"의 느낌 강화. Candy Crush에서 보석이 간헐적으로 반짝이는 것과 동일.

**적용 시스템**: HexGrid 또는 별도 AmbientEffectManager

**구현 방식**:
- 2~4초 간격으로 랜덤 일반 블록 1개 선택
- 해당 블록 위에 흰색 반투명 Image:
  1. 우상단에서 좌하단으로 45도 각도로 가느다란 빛줄기(2x20px) 이동
  2. 0.3s에 걸쳐 블록 좌상단 -> 우하단으로 슬라이드
  3. alpha 0 -> 0.4 -> 0 (삼각형 형태)
- VisualConstants 추가:
  - `ShimmerInterval_Min = 2.0f`
  - `ShimmerInterval_Max = 4.0f`
  - `ShimmerDuration = 0.3f`
  - `ShimmerAlpha = 0.4f`
  - `ShimmerAngle = 45f` (도)

**성능**: 동시 최대 1 GO, lifetime 0.3s. 극히 낮은 부하.

---

### A-03: 보드 배경 "미세 파동" [우선순위: 낮음]

**무엇인가**: 보드 배경에 매우 느리게 이동하는 반투명 원형 웨이브 2~3개가 겹치며 흐르는 효과.

**왜 좋은가**: 배경에 "깊이감"과 "살아있는 느낌" 부여. 정적인 그리드가 아니라 "에너지가 흐르는 보드" 느낌.

**적용 시스템**: HexGrid 배경 레이어

**구현 방식**:
- 보드 배경 레이어에 3개의 큰 원형 Image (200x200px):
  - 매우 낮은 alpha (0.03~0.05)
  - 각각 다른 속도(10~20px/s)로 대각선 이동
  - 화면 경계 도달 시 반대편에서 재등장
  - 색상: 현재 스테이지 주요 색의 매우 연한 버전
- VisualConstants 추가:
  - `AmbientWaveCount = 3`
  - `AmbientWaveSize = 200f`
  - `AmbientWaveAlpha = 0.04f`
  - `AmbientWaveSpeed = 15f`

**성능**: 3 GO 상시. 극히 낮은 부하 (큰 사이즈지만 alpha가 매우 낮아 GPU 부하 최소).

---

## 2.5 Screen Feel (화면 반응)

### S-01: 셰이크 패턴 다양화 - "방향성 셰이크" [우선순위: 높음]

**무엇인가**: 현재 모든 셰이크가 랜덤 (x, y) 방향인데, 특수 블록에 따라 방향성을 부여.
- Drill: 드릴 진행 방향의 수직으로만 흔들림
- Bomb: 방사형 (현재와 동일하되 Perlin noise 패턴)
- Laser: 3축 교대 방향 흔들림

**왜 좋은가**: 방향성 셰이크는 랜덤 셰이크 대비 "원인이 명확한" 피드백. GDC 2016 "Juicing Your Cameras with Math"에서 강조된 기법.

**적용 시스템**: 모든 시스템의 ScreenShake

**구현 방식**:
- ScreenShake 함수에 선택적 direction 파라미터 추가:
  ```
  ScreenShake(float intensity, float duration, Vector2? direction = null)
  ```
- direction이 null이면 기존 랜덤, 있으면:
  - 주 방향(direction) 축으로 80% intensity
  - 수직 축으로 20% intensity
  - 시간에 따라 감쇠하는 사인파 패턴 (랜덤 대신)
- Drill: perpendicular to drill direction
- Bomb: null (방사형 랜덤 유지)
- Laser: 연속 3방향 교대

**성능**: 추가 비용 없음. 기존 코드 수정만.

---

### S-02: "줌 펀치" (Zoom Punch) [우선순위: 높음]

**무엇인가**: 강력한 특수 블록 폭발 시 보드가 0.03s 동안 1.02x로 줌인했다가 0.08s에 걸쳐 1.0x로 돌아오는 효과.

**왜 좋은가**: 화면이 "앞으로 튀어나오는" 느낌. Hit Stop과 결합하면 임팩트가 극대화. 보드 Scale만 조작하므로 구현 간단.

**적용 시스템**: Bomb, Laser (강한 효과), Donut/XBlock (약한 효과)

**구현 방식**:
- VisualConstants 추가:
  - `ZoomPunchScale_Large = 1.025f` (Bomb, Laser)
  - `ZoomPunchScale_Small = 1.015f` (Donut, XBlock)
  - `ZoomPunchInDuration = 0.03f`
  - `ZoomPunchOutDuration = 0.08f`
- hexGrid.transform.localScale 조작:
  1. 0~0.03s: Scale 1.0 -> 1.025 (EaseOutCubic)
  2. 0.03~0.11s: Scale 1.025 -> 1.0 (EaseOutElastic)
- 여러 ZoomPunch가 겹치지 않도록 flag 관리 (shakeCount 패턴)

**성능**: 추가 비용 없음. Scale 조작만.

---

### S-03: 매칭 시 "보드 미세 바운스" [우선순위: 중간]

**무엇인가**: 일반 3개 매칭에서도 보드 전체가 살짝(1px) 아래로 내려갔다 올라오는 미세 반동.

**왜 좋은가**: 모든 매칭에 "물리적 무게감" 부여. 특수 블록이 아니어도 매칭 자체가 만족스러워짐.

**적용 시스템**: BlockRemovalSystem.ProcessMatchesInline (매칭 확인 직후)

**구현 방식**:
- VisualConstants 추가:
  - `MatchBounceDistance = 1.5f` (px)
  - `MatchBounceDownDuration = 0.03f`
  - `MatchBounceUpDuration = 0.08f`
- hexGrid.transform.localPosition.y 조작:
  1. 0~0.03s: y - 1.5px (EaseOutQuad)
  2. 0.03~0.11s: y + 0 (원위치) (EaseOutElastic)

**성능**: 추가 비용 없음.

---

## 2.6 Color Science (색상 과학)

### C-01: "블룸 시뮬레이션" 글로우 레이어 [우선순위: 높음]

**무엇인가**: 밝은 이펙트(플래시, 스파크, 충격파) 뒤에 더 크고 블러된 반투명 이미지를 깔아 "빛이 번지는" 블룸 효과를 시뮬레이션.

**왜 좋은가**: 실제 Bloom 포스트프로세싱 없이 "빛이 뿜어나오는" 프리미엄 느낌. 모바일 퍼즐게임에서 "고급스러움"의 핵심 차이.

**적용 시스템**: BombExplosionEffect, LaserCenterFlash, DrillLaunchEffect, DonutCenterEffect, XCenterEffect

**구현 방식**:
- 현재 모든 중앙 플래시(Flash) 뒤에 "BloomLayer" 추가:
  1. 플래시와 동일 위치, 동일 색상
  2. sizeDelta = 플래시의 1.8x
  3. alpha = 플래시의 0.25x (25%)
  4. SetAsFirstSibling (플래시 뒤에 렌더링)
  5. 플래시와 동일한 확장/페이드 타이밍, 약간 지연(0.02s lag)
- VisualConstants 추가:
  - `BloomLayerSizeMultiplier = 1.8f`
  - `BloomLayerAlphaMultiplier = 0.25f`
  - `BloomLayerLag = 0.02f`
- 스파크에도 적용 (옵션): 큰 스파크(8px+)만 뒤에 3x 크기, 0.15 alpha의 글로우 점 추가

**성능**: 중앙 이펙트당 1 GO 추가 (BloomLayer). 스파크 적용 시 큰 스파크당 +1 GO.

---

### C-02: "색온도 시프트" - 시스템별 색감 [우선순위: 중간]

**무엇인가**: 각 특수 블록 발동 시 보드 전체의 색감이 해당 시스템의 테마 색으로 아주 살짝 틴팅되는 효과.
- Bomb: 약간 따뜻한 톤 (주황/노란)
- Laser: 약간 차가운 톤 (파란)
- Donut: 무지개 색 순환
- Drill: 중립 (밝기만 살짝 증가)

**왜 좋은가**: 영화의 색보정(Color Grading)과 동일한 원리. "폭발 = 따뜻", "레이저 = 차갑게"라는 무의식적 색감 연결이 몰입감 강화.

**적용 시스템**: 보드 전체 오버레이

**구현 방식**:
- 보드 앞면(블록 위)에 전체 화면 크기의 반투명 Image 1개 상시 배치:
  - 기본 상태: alpha = 0 (투명)
  - 특수 블록 발동 시:
    - Bomb: Color(1.0, 0.8, 0.3, 0.05) - 아주 연한 주황
    - Laser: Color(0.5, 0.7, 1.0, 0.05) - 아주 연한 파랑
    - Donut: HSV 회전 Color, alpha 0.04
    - XBlock: 블록색, alpha 0.03
  - 0.1s fade-in, 이펙트 완료 후 0.3s fade-out
- VisualConstants 추가:
  - `ColorGradeAlpha = 0.05f`
  - `ColorGradeFadeInDuration = 0.1f`
  - `ColorGradeFadeOutDuration = 0.3f`
  - Color 테이블: Bomb/Laser/Donut/XBlock별

**성능**: 1 GO 상시 (alpha 0일 때 GPU 무시). 전체 화면 크기이므로 overdraw 주의, alpha를 0.05 이하로 유지.

---

### C-03: 파편/스파크 색상 다양성 강화 [우선순위: 중간]

**무엇인가**: 현재 파편 색상이 variation +/-0.15로 단조로움. HSV 공간에서 Hue를 +/-15도 변화시키고, Saturation과 Value도 변화시켜 더 풍부한 색감.

**왜 좋은가**: 같은 수의 파편이라도 색상 다양성이 높으면 시각적으로 더 화려하고 "풍성"하게 느껴짐.

**적용 시스템**: 모든 시스템의 AnimateDebris, 스파크 함수들

**구현 방식**:
- VisualConstants에 헬퍼 추가:
  - `DebrisHueVariation = 15f` (도)
  - `DebrisSaturationVariation = 0.15f`
  - `DebrisBrightnessVariation = 0.2f`
- 새 ColorVariation 함수:
  ```
  // 기존 RGB 직접 변환 대신 HSV 기반
  Color.RGBToHSV(baseColor, out h, out s, out v);
  h += Random.Range(-15f/360f, 15f/360f);
  s += Random.Range(-0.15f, 0.15f);
  v += Random.Range(-0.1f, 0.2f); // 밝은 쪽으로 bias
  return Color.HSVToRGB(Mathf.Repeat(h, 1f), Mathf.Clamp01(s), Mathf.Clamp01(v));
  ```

**성능**: 추가 비용 없음. 색상 계산만.

---

## 2.7 Motion Design (모션 설계)

### M-01: "이중 이징" 파괴 애니메이션 업그레이드 [우선순위: 높음]

**무엇인가**: 현재 파괴 애니메이션(0.14s)의 squeeze+shrink에 추가 레이어:
1. 처음 0.03s: 살짝 커짐 (1.0 -> 1.1) - "팽창"
2. 이후 0.11s: squeeze+shrink (기존) - "찌그러지며 사라짐"

**왜 좋은가**: 디즈니 애니메이션 원칙의 "Anticipation + Action". 팽창 없이 바로 줄어드는 것보다 "한번 부풀었다 터지는" 느낌이 훨씬 임팩트 있음.

**적용 시스템**: 모든 AnimateRemove, DestroyBlockWith*

**구현 방식**:
- VisualConstants 수정:
  - `DestroyDuration` 유지 (0.14f)
  - `DestroyExpandPhaseRatio = 0.2f` (전체 시간의 20% = 0.028s)
  - `DestroyExpandScale = 1.12f`
- 파괴 루프 수정:
  - t < 0.2: Scale = Lerp(1.0, 1.12, EaseOutCubic(t/0.2))
  - t >= 0.2: 기존 squeeze+shrink (t를 0.2~1.0 범위로 remap)

**성능**: 추가 비용 없음. 기존 코루틴 수정만.

---

### M-02: 스파크에 "꼬리" (Elongation) 효과 [우선순위: 중간]

**무엇인가**: 현재 스파크가 정사각형 Image인데, 이동 방향으로 길쭉하게 늘리면 "불꽃이 꼬리를 끌며 날아가는" 느낌.

**왜 좋은가**: 동일한 수의 스파크로 "빠르고 역동적인" 느낌 2배. UI.Image의 sizeDelta만 조정하므로 추가 비용 거의 없음.

**적용 시스템**: 모든 스파크 함수 (ExplosionSpark, LaunchSpark, RainbowSpark, XSpark)

**구현 방식**:
- 스파크 이동 중 매 프레임:
  1. 이동 방향 각도 계산: angle = Atan2(vel.y, vel.x)
  2. Image rotation = Euler(0, 0, angle)
  3. sizeDelta.y (길이) = size * (1 + speed / SparkMaxSpeed * 2.0)
  4. sizeDelta.x (폭) = size * 0.6
  5. 속도 감소에 따라 점점 정사각형으로 복귀
- VisualConstants 추가:
  - `SparkElongationFactor = 2.0f`
  - `SparkWidthRatio = 0.6f`

**성능**: 추가 비용 없음. 기존 코루틴 내 연산 약간 추가.

---

### M-03: "스프링 복귀" 이징 함수 추가 [우선순위: 중간]

**무엇인가**: 현재 EaseOutElastic(주기 0.3f)에 더해, 더 가볍고 빠른 "스프링" 이징 추가. 특히 충격파 디스플레이스먼트(I-03)와 블록 복귀에 적합.

**왜 좋은가**: EaseOutElastic은 진동이 크고 오래 지속되어 빠른 UI 복귀에는 과함. 짧은 1~2회 진동 후 안정되는 스프링이 퍼즐 게임에 더 적합.

**적용 시스템**: VisualConstants (공통 이징)

**구현 방식**:
- VisualConstants에 추가:
  ```
  // EaseOutSpring: 1~2회 진동 후 안정 (p=0.15, 더 짧은 주기)
  public static float EaseOutSpring(float t)
  {
      if (t <= 0f) return 0f;
      if (t >= 1f) return 1f;
      float p = 0.15f;
      return Mathf.Pow(2f, -12f * t) * Mathf.Sin((t - p/4f) * (2f * Mathf.PI) / p) + 1f;
  }
  ```
- 기존 EaseOutElastic(p=0.3) vs 새 EaseOutSpring(p=0.15):
  - Elastic: 넓은 진동, 부드러운 마무리 -> 스폰 팝, 합체 애니메이션
  - Spring: 짧은 진동, 빠른 안정 -> 충격파 복귀, 바운스

**성능**: 추가 비용 없음.

---

### M-04: 파편 비산 패턴 개선 - "부채꼴 + 중력 곡선" [우선순위: 낮음]

**무엇인가**: 현재 파편이 360도 랜덤으로 날아가는데, 위쪽 60% / 아래쪽 40%의 가중치를 두어 "위로 솟구쳤다가 떨어지는" 포물선이 더 자연스럽게 보이도록 함.

**왜 좋은가**: 현실 물리에서 파편은 중력 때문에 위쪽으로 더 많이 퍼짐(초기 속도가 위쪽). 이 bias가 있으면 무의식적으로 "자연스럽다"고 느낌.

**적용 시스템**: 모든 AnimateDebris, AnimateExplosionDebris

**구현 방식**:
- 현재: angle = Random.Range(0, 360)
- 변경: angle에 상향 bias 추가
  - 기본: Random.Range(0, 360) -> 유지
  - 추가: 상위 120도 범위(30~150도)에 60% 확률 가중치
  ```
  float angle;
  if (Random.value < 0.6f)
      angle = Random.Range(30f, 150f); // 위쪽 부채꼴
  else
      angle = Random.Range(0f, 360f);  // 전방향
  ```

**성능**: 추가 비용 없음.

---

## 2.8 보너스: 시스템별 특화 개선

### B-01: 드릴 - "소닉붐" 이펙트 [우선순위: 중간]

**무엇인가**: 드릴 투사체가 블록을 관통할 때마다 관통 지점에서 방사형으로 작은 충격파가 퍼지는 효과. 현재 ImpactWave는 있지만 투사체 진행 방향으로의 "소닉붐" 형태가 아님.

**구현**: 관통 시점에 투사체 이동 방향의 좌우로 V자 형태의 얇은 라인 2개 생성, 0.1s 확장 후 소멸.

---

### B-02: 폭탄 - "크레이터" 잔상 [우선순위: 낮음]

**무엇인가**: 폭탄 폭발 후 중앙 위치에 0.5s 동안 어두운 원형 그림자가 남았다가 사라지는 효과.

**구현**: 폭발 후 중앙에 어두운 반투명 원(30x30px, alpha 0.15) 생성, 0.5s에 걸쳐 fade out.

---

### B-03: 도넛(레인보우) - "무지개 궤적 커브" [우선순위: 중간]

**무엇인가**: 현재 RainbowConnectionLine이 직선인데, 약간의 곡선(베지어 커브)으로 만들어 "에너지가 흘러가는" 느낌.

**구현**: 직선 대신 중간점을 랜덤하게 편향시킨 2차 베지어 커브로 여러 작은 Image를 배치하여 곡선 시뮬레이션 (5~8개의 짧은 segment).

---

---

# 3. 우선순위 정리: 구현 로드맵

## Phase 1: 핵심 Juice (가장 큰 품질 향상, 3~4일 소요)

| ID | 항목 | 영향도 | 난이도 | GO 부하 |
|----|------|--------|--------|---------|
| I-01 | Hit Stop (순간 정지) | ★★★★★ | 쉬움 | 0 |
| I-02 | 화이트 플래시 오버레이 | ★★★★★ | 쉬움 | 파괴당 1, 0.06s |
| P-01 | 특수블록 충전 효과 | ★★★★☆ | 보통 | 0 |
| P-02 | 매칭 하이라이트 펄스 | ★★★★☆ | 쉬움 | 매칭당 3~5, 0.12s |
| M-01 | 이중 이징 파괴 | ★★★★☆ | 쉬움 | 0 |
| S-02 | 줌 펀치 | ★★★★☆ | 쉬움 | 0 |
| C-01 | 블룸 시뮬레이션 | ★★★★☆ | 보통 | 이펙트당 1 |

## Phase 2: 폴리시 (품질 한 단계 더 업, 3~4일 소요)

| ID | 항목 | 영향도 | 난이도 | GO 부하 |
|----|------|--------|--------|---------|
| F-01 | 고스트 이미지 잔상 | ★★★★☆ | 쉬움 | 파괴당 1, 0.15s |
| F-02 | 낙하 착지 더스트 | ★★★★☆ | 보통 | 착지당 2, 0.2s |
| A-01 | 특수블록 호흡 | ★★★★☆ | 보통 | 특수블록당 1 상시 |
| S-01 | 방향성 셰이크 | ★★★☆☆ | 보통 | 0 |
| M-02 | 스파크 꼬리 | ★★★☆☆ | 쉬움 | 0 |
| M-03 | 스프링 이징 | ★★★☆☆ | 쉬움 | 0 |
| C-03 | HSV 색상 다양성 | ★★★☆☆ | 쉬움 | 0 |

## Phase 3: 프리미엄 터치 (차별화, 2~3일 소요)

| ID | 항목 | 영향도 | 난이도 | GO 부하 |
|----|------|--------|--------|---------|
| P-03 | 콤보 빌드업 에스컬레이션 | ★★★☆☆ | 보통 | 4 상시 |
| I-03 | 디스토션 링 (블록 밀기) | ★★★☆☆ | 어려움 | 0 |
| I-04 | 드릴 트레일 잔상 | ★★★☆☆ | 보통 | 타겟당 1, 0.2s |
| F-03 | 여진 웨이브 | ★★☆☆☆ | 쉬움 | 1, 0.2s |
| C-02 | 색온도 시프트 | ★★☆☆☆ | 보통 | 1 상시 |
| S-03 | 보드 미세 바운스 | ★★☆☆☆ | 쉬움 | 0 |
| B-01 | 드릴 소닉붐 | ★★☆☆☆ | 보통 | 관통당 2, 0.1s |
| B-03 | 도넛 곡선 궤적 | ★★☆☆☆ | 어려움 | 타겟당 5~8, 0.2s |

## Phase 4: 앰비언트 (여유 있을 때)

| ID | 항목 | 영향도 | 난이도 | GO 부하 |
|----|------|--------|--------|---------|
| A-02 | 일반 블록 반짝임 | ★★☆☆☆ | 쉬움 | 1, 0.3s |
| A-03 | 배경 파동 | ★☆☆☆☆ | 쉬움 | 3 상시 |
| I-05 | 레이저 종단 플레어 | ★★☆☆☆ | 보통 | 빔당 1 |
| B-02 | 폭탄 크레이터 | ★☆☆☆☆ | 쉬움 | 1, 0.5s |
| M-04 | 파편 상향 bias | ★☆☆☆☆ | 쉬움 | 0 |

---

# 4. VisualConstants.cs 추가 파라미터 종합

```
// Phase 1: 핵심 Juice
// Hit Stop
HitStopDuration_Large = 0.04f
HitStopDuration_Medium = 0.03f
HitStopDuration_Small = 0.02f
HitStopSlowMoDuration = 0.05f
HitStopSlowMoScale = 0.4f
HitStopCooldown = 0.3f

// Destroy Flash
DestroyFlashDuration = 0.06f
DestroyFlashAlpha = 0.7f
DestroyFlashSizeMultiplier = 1.3f

// Pre-Fire Compression (모든 특수블록)
PreFireCompressionDuration = 0.1f
PreFireScaleMin = 0.85f
PreFireScaleMax = 1.12f
PreFireBrightenAmount = 0.25f

// Match Highlight Pulse
MatchPulseScale = 1.08f
MatchPulseDuration = 0.12f
MatchPulseOverlayAlpha = 0.35f
MatchPulseStagger = 0.015f

// Destroy Expand Phase
DestroyExpandPhaseRatio = 0.2f
DestroyExpandScale = 1.12f

// Zoom Punch
ZoomPunchScale_Large = 1.025f
ZoomPunchScale_Small = 1.015f
ZoomPunchInDuration = 0.03f
ZoomPunchOutDuration = 0.08f

// Bloom Simulation
BloomLayerSizeMultiplier = 1.8f
BloomLayerAlphaMultiplier = 0.25f
BloomLayerLag = 0.02f

// Phase 2: 폴리시
// Ghost Image
GhostImageDuration = 0.15f
GhostImageInitialAlpha = 0.35f
GhostImageScaleStart = 0.8f
GhostImageScaleEnd = 1.2f

// Landing Dust
LandingDustCount = 2
LandingDustSize = 8f
LandingDustSpreadX = 15f
LandingDustSpeedY = 30f
LandingDustDuration = 0.2f
LandingDustAlpha = 0.25f

// Special Block Idle Breathing
IdleBreathSpeed = 2.5f
IdleBreathScaleMin = 0.97f
IdleBreathScaleMax = 1.03f
IdleGlowPulseSpeed = 3.0f
IdleGlowAlphaMin = 0.0f
IdleGlowAlphaMax = 0.15f

// Spark Elongation
SparkElongationFactor = 2.0f
SparkWidthRatio = 0.6f

// HSV Color Variation
DebrisHueVariation = 15f (도)
DebrisSaturationVariation = 0.15f
DebrisBrightnessVariation = 0.2f

// Phase 3: 프리미엄
// Cascade Buildup
CascadeBorderAlphaPerDepth = 0.1f
CascadeZoomPerDepth = 0.015f
CascadeZoomMax = 1.05f

// Shockwave Displacement
ShockwaveDisplacementMax = 5f
ShockwaveDisplacementRadius = 200f
ShockwaveRecoverDuration = 0.15f

// Drill Trail
DrillTrailWidth = 4f
DrillTrailAlpha = 0.5f
DrillTrailFadeDuration = 0.2f

// Aftershock
AftershockDelay = 0.3f
AftershockScaleRatio = 0.6f
AftershockAlphaRatio = 0.5f

// Color Grading
ColorGradeAlpha = 0.05f
ColorGradeFadeInDuration = 0.1f
ColorGradeFadeOutDuration = 0.3f

// Match Bounce
MatchBounceDistance = 1.5f
MatchBounceDownDuration = 0.03f
MatchBounceUpDuration = 0.08f

// Phase 4: 앰비언트
ShimmerInterval_Min = 2.0f
ShimmerInterval_Max = 4.0f
ShimmerDuration = 0.3f
ShimmerAlpha = 0.4f

AmbientWaveCount = 3
AmbientWaveSize = 200f
AmbientWaveAlpha = 0.04f
AmbientWaveSpeed = 15f

LaserBeamShimmerAmplitude = 0.25f
LaserBeamShimmerFrequency = 45f
LaserBeamEndFlareSize = 16f
```

---

# 5. 성능 예산 분석

## 최악의 케이스 시나리오: 폭탄 + 레이저 동시 발동 (캐스케이드 depth 3)

현재 시스템 GameObject 동시 존재 수:
- 폭탄 중앙: Flash(1) + Ring(2) + Spark(~27) + Debris(~15) = ~45
- 레이저 중앙: Flash(1) + Spark(~27) + Beam(6+6core) = ~40
- 블록별 파괴: ImpactWave(1) + Debris(~5) per block x ~12 blocks = ~72
- **현재 총계: ~157 GO**

업그레이드 후 추가분:
- Phase 1: +Flash overlay(12) +Bloom layers(2) = ~14
- Phase 2: +Ghost images(12) +Dust(24) = ~36
- Phase 3: +Trail(8) +Aftershock(2) = ~10
- **업그레이드 추가: ~60 GO** (대부분 0.06~0.2s 이내 소멸)

**예상 총 피크: ~217 GO** (최악의 케이스, 0.1초 이내 대부분 소멸)

모바일 기준: UI.Image로 생성한 200개 이하 GO는 중/저사양 기기에서도 안정적으로 60fps 유지 가능.
대부분의 추가 GO는 lifetime이 0.06~0.2s로 극히 짧아, 실제 동시 존재 수는 피크의 30~40% 수준.

---

# 6. 핵심 요약: "가장 적은 노력으로 가장 큰 효과"

**만약 3가지만 구현한다면:**

1. **I-01 Hit Stop** - TimeScale 조작 한 줄로 임팩트 체감 200% 증가
2. **I-02 화이트 플래시** - 블록당 Image 1개, 0.06s로 "시원한 파괴감"
3. **M-01 이중 이징 파괴** - 기존 코드 수정만으로 "팽창+붕괴" 느낌

이 3가지만으로도 "인디 수준"에서 "상용 퍼즐 게임 수준"으로의 체감 품질 점프가 가능합니다.

---

*문서 작성: Art Director*
*일자: 2026-02-14*
*대상 프로젝트: HexaPuzzle (JewelsHexaPuzzle)*
*제약 조건: UI.Image 기반 프로시저럴, ParticleSystem/외부 에셋 없음*
