# SFX 제작 사양서 - 헥사 퍼즐 게임

## 프로젝트 개요

**게임**: 헥사곤 매치 퍼즐 (MatchMine)
**비주얼 테마**: 소프트 파스텔 (캔디크러시 / 쿠키잼 / 토카보카 스타일)
**색상 팔레트**: 로즈 핑크, 하늘 블루, 민트 그린, 레몬 옐로우, 라벤더, 피치 오렌지
**질감**: 마카롱, 젤리, 구미 캔디 느낌의 부드럽고 탱글한 텍스처
**사운드 방향성**: 따뜻하고 귀여운, 공격적이지 않은, 오르골/실로폰 느낌의 파스텔 톤 사운드

### 특수 블록 시스템 (사운드 디자인 참조)
| 특수 블록 | 생성 조건 | 발동 효과 | VFX 특성 |
|-----------|-----------|-----------|----------|
| **Drill** | 4매치 | 한 방향 라인 제거 (Vertical/Slash/BackSlash) | 발사체 projectile, 빠른 속도, 웜 옐로우 플래시 |
| **Bomb** | 5매치 이상 | 인접 6칸 폭발 | 강한 셰이크, 넓은 파편, 웜 골드 플래시 |
| **Rainbow/Donut** | 7매치 이상 (도넛 패턴) | 보드 전체 같은 색 제거 | 링 회전, 물결 파동, 24개 스파크 |
| **XBlock** | 중앙+주변6개 동색 | 보드 전체 같은 색 제거 | 물결 파동, 20개 스파크 |
| **Laser** | 6매치 | 헥사 3축 전체 직선 제거 | 빔 0.3초, 쿨 블루 플래시 |

### 이펙트 타이밍 기준 (애니메이션과 동기화)
- 블록 파괴 애니메이션: **0.14초** (DestroyDuration)
- 매치 하이라이트 펄스: **0.15초** (MatchPulseDuration)
- 캐스케이드 딜레이: **0.1초** (cascadeDelay)
- 센터 플래시: **0.35초** (FlashDuration)
- 히트스톱 (Large): **0.06초** (HitStopDurationLarge)
- 스폰 애니메이션: **0.25초** (SpawnDuration)
- 점수 팝업 지속: **0.8초** (PopupBaseDuration)

---

## Deliverable 1: jsfxr / ChipTone 파라미터 가이드

> **참고**: jsfxr (https://sfxr.me) 또는 ChipTone (https://sfbgames.itch.io/chiptone) 에서 아래 파라미터로 생성 후, DAW에서 리버브/EQ 후처리를 추가하여 "파스텔 톤"을 완성합니다.

---

### 1. 버튼 클릭 (Button Click) - "팝" / "딩"

**목표**: 마카롱을 손가락으로 살짝 누르는 느낌의 짧고 귀여운 "뽁" 소리

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine | 가장 부드러운 파형, 파스텔에 필수 |
| **Base Frequency** | 880 Hz (A5) | 밝지만 날카롭지 않은 음역 |
| **Attack** | 0.0s | 즉각 반응 |
| **Sustain** | 0.03s | 매우 짧은 유지 |
| **Decay** | 0.08s | 빠르게 감쇠 |
| **Frequency Slide** | +200 Hz (상향) | 살짝 위로 올라가는 "팝" 느낌 |
| **Volume** | 0.4 | 과하지 않은 볼륨 |

**ChipTone 설명**: "Pickup/Coin" 프리셋에서 시작. Sine wave, 짧은 duration(0.1s), pitch를 A5로, slide up 약간 적용. Harmonics 끄기.

**후처리 권장**:
- 가벼운 Room Reverb (Decay 0.3s)
- High-pass filter 300Hz (저음 잡음 제거)
- Soft limiter

---

### 2. 팝업 열기 (Popup Open) - 부드러운 상승 슬라이드

**목표**: 풍선이 부드럽게 부풀어 오르는 느낌, UI 패널이 스르르 나타나는 소리

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine + 약간의 Triangle 레이어 | 둥글고 따뜻한 음색 |
| **Base Frequency** | 440 Hz (A4) | 시작 음 |
| **End Frequency** | 880 Hz (A5) | 1옥타브 상승 |
| **Attack** | 0.05s | 살짝 부드러운 진입 |
| **Sustain** | 0.1s | 중간 유지 |
| **Decay** | 0.15s | 여운 있는 감쇠 |
| **Frequency Slide** | +440 Hz (선형 상승) | 0.3s에 걸쳐 부드럽게 슬라이드 |
| **Volume Envelope** | Ease-out | 끝으로 갈수록 자연스럽게 줄어듦 |

**ChipTone 설명**: "Powerup" 프리셋에서 시작. Sine wave, Duration 0.3s, Pitch slide from A4 to A5. Attack을 약간 늘려 부드러운 진입. Vibrato 없음.

**후처리 권장**:
- Medium Room Reverb (Decay 0.5s)
- Slight chorus effect (미세한 코러스로 공간감)
- Low-pass filter 4kHz (날카로운 고음 제거)

---

### 3. 블록 회전 (Block Rotate) - 부드러운 "슈웅"

**목표**: 젤리 블록이 탱글탱글하게 회전하는 느낌, 공기를 가르는 부드러운 바람 소리

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | White Noise (필터 적용) | 바람/움직임 느낌 |
| **Base Frequency** | 800 Hz (Band-pass center) | 중음역대 |
| **Attack** | 0.03s | 빠른 진입 |
| **Sustain** | 0.08s | 짧은 유지 |
| **Decay** | 0.12s | 부드러운 감쇠 |
| **Filter** | Band-pass, Q=2.0 | 중음역만 통과 |
| **Frequency Sweep** | 600Hz → 1200Hz → 600Hz | 올라갔다 내려오는 "슈웅" |
| **Volume** | 0.25 | 배경 느낌, 조용하게 |

**ChipTone 설명**: "Laser/Shoot" 프리셋에서 시작하되 공격성 제거. Noise 기반, Band-pass filter를 좁게 설정(Q=2). Pitch를 중간에서 시작해 위로 갔다 내려오도록 설정. Duration 0.2s.

**후처리 권장**:
- 강한 Low-pass filter 2kHz (날카로운 노이즈 완전 제거)
- Room Reverb (Decay 0.3s)
- Volume을 낮게 유지 (다른 효과보다 -6dB)

---

### 4. 매치 성공 3매치 (Match Success 3) - 밝은 오르골 코드

**목표**: 오르골에서 맑은 음 3개가 빠르게 울리는 느낌, "띵띵띵" 맑은 종소리

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine (또는 Sine + 약한 Square 하모닉) | 오르골 느낌 |
| **Note Sequence** | C6 → E6 → G6 (도미솔, 장화음) | 30ms 간격으로 아르페지오 |
| **Per-Note Attack** | 0.0s | 즉각 어택 |
| **Per-Note Sustain** | 0.06s | 짧은 유지 |
| **Per-Note Decay** | 0.15s | 여운 있는 감쇠 |
| **Total Duration** | ~0.3s | 매치 하이라이트(0.15s)와 맞춤 |
| **Volume** | 0.5 | 적당한 볼륨 |

**ChipTone 설명**: 세 개의 "Pickup" 사운드를 30ms 간격으로 레이어. 각각 C6, E6, G6 음으로 설정. Sine wave, 짧은 sustain. 마지막 음(G6)의 decay를 약간 길게.

**jsfxr 싱글 방법**: Arpeggio 기능 사용. Base pitch C6, Arpeggio speed 0.03, Arpeggio mod +4 semitones(장3도), 2nd step +3 semitones(단3도). Sine wave.

**후처리 권장**:
- Plate Reverb (Decay 0.6s, 오르골 공간감)
- Slight high shelf boost +2dB at 3kHz (맑은 반짝임)
- Stereo widening 20%

---

### 5. 매치 성공 4매치 (Match Success 4) - 더 풍부한 화음

**목표**: 3매치보다 한 음 높고 풍성한 느낌, "반짝" 하는 보상감

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine + Triangle 레이어 | 약간 더 풍성한 배음 |
| **Note Sequence** | C6 → E6 → G6 → C7 (도미솔도) | 25ms 간격, 4음 아르페지오 |
| **Per-Note Attack** | 0.0s | 즉각 어택 |
| **Per-Note Sustain** | 0.05s | 짧은 유지 |
| **Per-Note Decay** | 0.18s | 좀 더 긴 여운 |
| **Total Duration** | ~0.35s | |
| **Volume** | 0.55 | 3매치보다 살짝 큰 볼륨 |
| **추가 효과** | 마지막 음에 약한 shimmer (진폭 비브라토 2Hz, depth 5%) | 반짝이는 느낌 |

**ChipTone 설명**: 4매치 = 4음 아르페지오. C6→E6→G6→C7, 25ms 간격. Sine+Triangle 혼합. 마지막 C7에 약한 vibrato 추가. 전체 볼륨 3매치 대비 +1dB.

**후처리 권장**:
- Plate Reverb (Decay 0.8s)
- Shimmer reverb 레이어 추가 (매우 약하게)
- Stereo widening 30%

---

### 6. 매치 성공 5매치 (Match Success 5) - 반짝이는 승리감

**목표**: 축제 분위기, 별이 반짝이는 느낌, 오르골 + 글로켄슈필 같은 화려한 소리

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine + Square(10%) + Noise burst | 가장 풍성한 배음 구성 |
| **Note Sequence** | C6 → E6 → G6 → C7 → E7 | 20ms 간격, 5음 상행 |
| **Per-Note Attack** | 0.0s | |
| **Per-Note Sustain** | 0.04s | |
| **Per-Note Decay** | 0.22s | 긴 여운 |
| **Total Duration** | ~0.5s | |
| **Volume** | 0.6 | 가장 큰 매치 사운드 |
| **추가 효과 1** | 마지막 음에 shimmer (진폭 비브라토 3Hz, depth 8%) | |
| **추가 효과 2** | 전체에 걸쳐 필터링된 noise burst (sparkle) | 반짝이 효과 |

**ChipTone 설명**: 5음 상행 아르페지오 C6→E6→G6→C7→E7, 20ms 간격. 마지막 두 음에 강한 shimmer. 배경에 필터된 white noise "쉬이이" 추가(매우 낮은 볼륨). Sine 메인 + 미세한 Square 하모닉.

**후처리 권장**:
- Shimmer Reverb (주 효과)
- Plate Reverb (Decay 1.0s)
- Stereo widening 40%
- High shelf boost +3dB at 4kHz

---

### 7. 매치 실패 (Match Fail) - 부드러운 하강

**목표**: "아, 아쉽다" 느낌. 절대 벌 주는 느낌이 아닌, 살짝 풀이 죽는 귀여운 소리

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine | 가장 부드러운 파형 |
| **Note Sequence** | E5 → C5 (단3도 하행) | 100ms 간격, 2음 |
| **Per-Note Attack** | 0.02s | 살짝 부드럽게 |
| **Per-Note Sustain** | 0.08s | |
| **Per-Note Decay** | 0.15s | |
| **Total Duration** | ~0.35s | |
| **Volume** | 0.3 | 조용하게 (벌이 아니므로) |
| **Pitch Bend** | 각 음에서 -20 cents | 살짝 처지는 느낌 |

**ChipTone 설명**: "Hurt/Hit" 프리셋 절대 사용 금지. 대신 두 개의 Sine tone을 100ms 간격으로: E5→C5. 각각에 미세한 pitch drop(-20cents) 적용. 볼륨을 매우 낮게.

**후처리 권장**:
- Warm Room Reverb (Decay 0.4s)
- Low-pass filter 2.5kHz (부드럽게)
- 전체 볼륨 다른 효과 대비 -4dB

---

### 8. 블록 파괴/팝 (Block Destroy/Pop) - 버블 팝

**목표**: 비눗방울 또는 젤리가 "톡" 터지는 느낌, 가볍고 경쾌한 소리

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine | |
| **Base Frequency** | 1200 Hz | 높고 밝은 음 |
| **Attack** | 0.0s | 즉각 |
| **Sustain** | 0.01s | 극히 짧음 |
| **Decay** | 0.06s | 빠른 감쇠 |
| **Frequency Slide** | -800 Hz (급하강) | "톡" 느낌의 핵심 |
| **Volume** | 0.35 | 가벼운 볼륨 |
| **Total Duration** | ~0.07s | DestroyDuration(0.14s) 앞절반에 맞춤 |

**ChipTone 설명**: "Blip/Select" 프리셋에서 시작. Sine, 매우 짧은 duration(0.07s). 높은 pitch에서 시작해 급격히 하강. "뽁" 하는 방울 터지는 소리.

**구현 참고**: 블록 색상별로 pitch를 +-50Hz 랜덤 변조하면 연속 파괴 시 자연스러운 다양성이 생김. DestroyDuration(0.14s)과 동기화하되, 사운드는 앞쪽 절반(0.07s)에서 어택이 완료되어야 함.

**후처리 권장**:
- 극미세 Room Reverb (Decay 0.15s)
- High-pass filter 400Hz

---

### 9. 블록 착지 (Block Land) - 마카롱 낙하

**목표**: 폭신한 마카롱이 쿠션 위에 "톡" 떨어지는 느낌, 묵직하지만 부드러운

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine + Noise burst (저역 필터) | 묵직함 + 착지 질감 |
| **Base Frequency** | 180 Hz | 낮고 둥근 음 |
| **Attack** | 0.0s | 즉각 충격 |
| **Sustain** | 0.02s | 매우 짧음 |
| **Decay** | 0.1s | 중간 감쇠 |
| **Frequency Slide** | -60 Hz (미세 하강) | 착지 후 안정 |
| **Noise layer** | Low-pass 500Hz, Duration 0.03s | "퍽" 하는 착지 질감 |
| **Volume** | 0.3 | 배경 느낌 |

**ChipTone 설명**: "Explosion" 프리셋에서 시작하되 완전히 길들이기. Sine+Noise mix, 주파수를 180Hz로 매우 낮게. Duration 0.12s. Noise에 강한 Low-pass(500Hz). 전체 볼륨 낮게.

**구현 참고**: BounceRatio(0.3)에 맞춰 바운스 시 동일 사운드를 -6dB 감쇄하여 재생하면 물리적 반발 느낌이 남. gravity=2500, maxFallSpeed=1500과 맞물려 자연스러운 착지감 구현.

**후처리 권장**:
- Warm Reverb (Decay 0.2s)
- Low-pass filter 1.5kHz (전체적으로 둥글게)
- Sub bass enhancement (80Hz 부근)

---

### 10. 콤보 캐스케이드 (Combo Cascade) - 상행 실로폰

**목표**: 실로폰이나 비브라폰으로 "도레미파솔" 올라가는 느낌, 연쇄 반응의 쾌감

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine + Triangle | 실로폰/비브라폰 느낌 |
| **Note Scale** | C Major Pentatonic: C5, D5, E5, G5, A5, C6... | 캐스케이드 깊이별 다음 음 |
| **Per-Note Attack** | 0.0s | |
| **Per-Note Sustain** | 0.04s | |
| **Per-Note Decay** | 0.12s | |
| **Note Interval** | CascadeDelay(0.1s)와 동기화 | 각 캐스케이드 단계마다 다음 음 재생 |
| **Volume Progression** | 0.35 → 0.55 (점진적 증가) | 캐스케이드가 깊어질수록 강해짐 |

**ChipTone 설명**: 개별 "Pickup" 사운드를 펜타토닉 음계로 5~6개 준비. Sine+Triangle, Duration 0.15s. 각각 C5, D5, E5, G5, A5, C6. 게임에서 캐스케이드 깊이(currentCascadeDepth)에 따라 순차 재생.

**구현 참고**: `BlockRemovalSystem.CurrentCascadeDepth`에 따라 인덱스 선택. `VisualConstants.GetCascadeMultiplier(depth)`와 연동하여 depth가 깊을수록 볼륨도 비례 증가. maxCombo(4)에 도달하면 마지막 음을 shimmer로 꾸밈.

**후처리 권장**:
- Plate Reverb (Decay 0.5s)
- Each note slightly panned L/R alternately (스테레오 움직임)
- 깊이별 점진적 shimmer reverb 추가

---

### 11. 경고 비프 (Warning Beep) - 귀엽지만 주의 환기

**목표**: TimeBomb 카운트다운이나 이동 횟수 부족 시. "위험해!" 가 아닌 "조심해~" 느낌

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Waveform** | Sine + 약한 Square(5%) | 살짝만 경고 느낌 |
| **Note Pattern** | A5 → A5 (동일 음 2회 반복) | 80ms 간격 |
| **Per-Note Attack** | 0.01s | |
| **Per-Note Sustain** | 0.05s | |
| **Per-Note Decay** | 0.05s | |
| **Total Duration** | ~0.25s | |
| **Volume** | 0.45 | 주의를 끌되 불쾌하지 않게 |
| **Vibrato** | 6Hz, depth 3% | 약간의 떨림으로 긴장감 |

**ChipTone 설명**: 같은 음(A5)을 80ms 간격으로 2회 반복하는 패턴. Sine 메인, 아주 약한 Square 혼합(5%). 각 비프에 미세한 vibrato. "삐삐" 가 아닌 "띵띵" 느낌 유지.

**구현 참고**: TimeBomb 카운트다운(timeBombCount)이 줄어들 때마다 재생. 남은 카운트가 3 이하일 때 피치를 반음씩 올려서 긴장감 증가. MovePulseSpeed(0.5s)와 동기화 가능.

**후처리 권장**:
- Warm Room Reverb (Decay 0.2s)
- Low-pass filter 3kHz (날카로운 비프 느낌 완화)
- 약간의 compression (일정한 볼륨 유지)

---

## jsfxr 파라미터 요약 테이블

| # | 사운드 | Wave | Base Hz | Slide | Attack | Sustain | Decay | 총 길이 | Vol |
|---|--------|------|---------|-------|--------|---------|-------|---------|-----|
| 1 | 버튼 클릭 | Sine | 880 | +200 | 0.00 | 0.03 | 0.08 | 0.11s | 0.40 |
| 2 | 팝업 열기 | Sine+Tri | 440 | +440 | 0.05 | 0.10 | 0.15 | 0.30s | 0.40 |
| 3 | 블록 회전 | Noise(BP) | 800 | sweep | 0.03 | 0.08 | 0.12 | 0.23s | 0.25 |
| 4 | 3매치 | Sine | C6 arp | +4,+3st | 0.00 | 0.06 | 0.15 | 0.30s | 0.50 |
| 5 | 4매치 | Sine+Tri | C6 arp | +4,+3,+5st | 0.00 | 0.05 | 0.18 | 0.35s | 0.55 |
| 6 | 5매치 | Sine+Sq | C6 arp | +4,+3,+5,+4st | 0.00 | 0.04 | 0.22 | 0.50s | 0.60 |
| 7 | 매치 실패 | Sine | E5 | -3st | 0.02 | 0.08 | 0.15 | 0.35s | 0.30 |
| 8 | 블록 파괴 | Sine | 1200 | -800 | 0.00 | 0.01 | 0.06 | 0.07s | 0.35 |
| 9 | 블록 착지 | Sine+Noise | 180 | -60 | 0.00 | 0.02 | 0.10 | 0.12s | 0.30 |
| 10 | 캐스케이드 | Sine+Tri | C5 penta | per-step | 0.00 | 0.04 | 0.12 | 0.15s/note | 0.35-0.55 |
| 11 | 경고 비프 | Sine+Sq(5%) | A5 | x2 rep | 0.01 | 0.05 | 0.05 | 0.25s | 0.45 |

---

## Deliverable 2: ElevenLabs SFX 프롬프트

> **참고**: ElevenLabs Sound Effects API에 아래 영어 프롬프트를 입력하여 생성합니다.
> 각 프롬프트는 "파스텔 톤" 사운드를 위해 **mood descriptor**(cute, soft, warm, gentle, playful)를 반드시 포함합니다.
> 생성 후 DAW에서 트리밍, EQ, 리버브 후처리를 진행합니다.

---

### 1. 드릴 발동 (Drill Activation)

**게임 메커닉**: 4매치로 생성된 드릴 블록이 활성화되어 한 방향 라인을 관통 제거. projectileSpeed=1200, drillSpeed=0.06s. 웜 옐로우 플래시.

**ElevenLabs Prompt:**
```
A cute, playful mechanical wind-up toy sound with a soft whirring noise. Think of a tiny pastel-colored toy drill made of candy spinning gently. Short duration, about 0.5 seconds. Starts with a soft click, then a brief high-pitched gentle whir that rises slightly in pitch. Warm and friendly tone, not industrial or harsh. Music box quality mixed with a gentle spinning sound. Suitable for a children's puzzle game with a soft pastel macaroon aesthetic.
```

**핵심 키워드**: `cute`, `toy drill`, `candy`, `pastel`, `gentle whir`, `music box quality`
**목표 길이**: 0.4 ~ 0.6초
**트리밍 가이드**: 첫 클릭 유지, 꼬리 부분(0.5초 이후) 페이드아웃

---

### 2. 폭탄 폭발 (Bomb Explosion)

**게임 메커닉**: 5매치 이상으로 생성된 폭탄 블록이 인접 6칸을 폭발로 제거. shakeIntensity=12, debrisCount=8. 웜 골드 플래시. HitStop 0.06s.

**ElevenLabs Prompt:**
```
A soft, friendly cartoon "poof" explosion sound, like a confetti cannon popping at a children's birthday party. Not a real explosion - more like a fluffy cotton candy cloud bursting open with a gentle "pomf" sound. Very short, about 0.3 seconds. Warm and round low-frequency body with sparkly high-frequency sprinkles on top. Think of popping a giant pastel-colored bubble filled with glitter. Cute and satisfying, never scary or aggressive. Suitable for a kawaii-style puzzle game.
```

**핵심 키워드**: `poof`, `confetti cannon`, `cotton candy`, `pomf`, `pastel bubble`, `glitter`, `kawaii`
**목표 길이**: 0.25 ~ 0.4초
**트리밍 가이드**: HitStop(0.06s) 직후에 메인 "pomf"가 오도록 타이밍 조정. 센터 플래시(0.35s)와 꼬리 동기화.

---

### 3. 레이저 빔 (Laser Beam)

**게임 메커닉**: 6매치로 생성된 레이저가 헥사 3축(별 모양) 전체 직선을 삭제. beamDuration=0.3s, laserSpeed=0.04s. 쿨 블루 플래시.

**ElevenLabs Prompt:**
```
A magical fairy wand light beam sound, like Tinkerbell casting a gentle spell. Not a sci-fi laser at all - instead, a warm, shimmering beam of starlight traveling across a surface. About 0.5 seconds long. Starts with a soft crystalline "ting" then a gentle sustained shimmer that sweeps from left to right. Think of moonlight being drawn across frosted glass. Ethereal and dreamy, with a slight musical quality like a glass harmonica. Warm pastel energy, not cold or aggressive. Suitable for a cute puzzle game with a soft candy aesthetic.
```

**핵심 키워드**: `fairy wand`, `Tinkerbell`, `starlight`, `crystalline`, `glass harmonica`, `moonlight`, `dreamy`
**목표 길이**: 0.4 ~ 0.6초
**트리밍 가이드**: beamDuration(0.3s)과 메인 스윕 구간을 정확히 맞추기. 꼬리 shimmer는 자연 감쇠.

---

### 4. 도넛/레인보우 (Donut/Rainbow)

**게임 메커닉**: 7매치 이상의 도넛 패턴에서 생성. 보드 전체에서 같은 색 블록을 물결 파동으로 제거. waveDelay=0.04s, sparkCount=24, 링 회전.

**ElevenLabs Prompt:**
```
A magical rainbow shimmer sound, like sunlight refracting through a crystal prism and creating a cascade of pastel-colored light. About 0.8 seconds long. Begins with a gentle ascending chime, then expands into a warm, enveloping shimmer that radiates outward in waves. Think of a music box playing while rainbow-colored soap bubbles float through the air. Includes subtle wind chime tinkling and a soft, dreamy sustain. Warm, magical, and wonder-filled. Not sharp or piercing. The sound should feel like it's expanding outward in a circle. Suitable for a cute pastel-themed puzzle game where a special rainbow power activates.
```

**핵심 키워드**: `rainbow shimmer`, `crystal prism`, `music box`, `soap bubbles`, `wind chime`, `expanding outward`, `wonder`
**목표 길이**: 0.6 ~ 1.0초
**트리밍 가이드**: 첫 chime은 링 생성 순간에 맞추고, shimmer 파동은 waveDelay(0.04s)의 물결 확산과 동기화. sparkCount(24)의 개별 스파크와 어우러지도록 반짝이는 꼬리 유지.

---

### 5. X블록 (X-Block)

**게임 메커닉**: 중앙 블록 주변 6개가 동색일 때 중앙에 생성. 발동 시 보드 전체 같은 색 제거. waveDelay=0.03s, sparkCount=20.

**ElevenLabs Prompt:**
```
A crossing sparkle pattern sound, like two streams of fairy dust crossing each other in an X shape. About 0.6 seconds long. Starts with two quick, bright crystalline "ting" sounds almost simultaneously (slightly offset), then a burst of sparkling particles spreading outward. Think of two magic wands touching tips and creating a shower of pastel stars. The tone is bright, warm, and celebratory but gentle. Includes a subtle whoosh as the sparkles expand. Musical quality similar to a celesta or glockenspiel. Not harsh or metallic. Suitable for a soft, cute puzzle game with a macaroon and jelly visual theme.
```

**핵심 키워드**: `crossing sparkle`, `fairy dust`, `X shape`, `two crystalline tings`, `celesta`, `glockenspiel`, `pastel stars`
**목표 길이**: 0.5 ~ 0.7초
**트리밍 가이드**: 두 개의 "ting"이 거의 동시(0.02초 차이)에 울리도록 앞부분 조정. 물결 파동(waveDelay 0.03s)과 스파크 확산 동기화.

---

### 6. 스테이지 클리어 팡파레 (Stage Clear Fanfare)

**게임 메커닉**: 목표 점수 달성 또는 모든 조건 충족 시 스테이지 완료.

**ElevenLabs Prompt:**
```
A delightful music box celebration fanfare, about 2 seconds long. Like a tiny antique music box opening and playing a short victorious melody. The melody is simple and joyful: ascending notes in a major key (think "da-da-da-DAAA" pattern), played with music box or celesta timbres. Add gentle sparkle sounds and a soft bell at the climax. The mood is warm, cozy, and congratulatory - like being praised by a kind friend. Think of the sound when you complete a level in a cute mobile game like Candy Crush or Cookie Jam. Pastel and sweet, never bombastic or aggressive. Ends with a gentle trailing shimmer. Suitable for a soft pastel-themed children's puzzle game.
```

**핵심 키워드**: `music box`, `celebration`, `celesta`, `ascending major key`, `da-da-da-DAAA`, `sparkle`, `cozy`, `congratulatory`
**목표 길이**: 1.5 ~ 2.5초
**트리밍 가이드**: 팡파레 시작은 스테이지 클리어 판정 직후. 메인 멜로디(~1.5초) + 여운 shimmer(~0.5초). 결과 화면 전환과 맞물리도록 끝부분 자연 감쇠.

---

### 7. 게임 오버 (Game Over)

**게임 메커닉**: 이동 횟수 소진 또는 TimeBomb 폭발로 게임 종료.

**ElevenLabs Prompt:**
```
A gentle, comforting, slightly sad sound, about 1.5 seconds long. Like a music box slowly winding down and playing its last few notes. Descending melody, minor key, but still warm and gentle - not depressing or harsh. Think of a soft "aww" moment, like a puppy tilting its head sadly. The sound should make the player feel "that's okay, try again!" rather than punished. Includes a soft, warm pad sustain underneath the descending notes. The final note should linger gently before fading. Similar to the game-over sound in cute mobile games - disappointing but encouraging. Pastel and soft, never dark or ominous. Suitable for a gentle, cute puzzle game for all ages.
```

**핵심 키워드**: `music box winding down`, `descending minor key`, `warm`, `gentle`, `aww moment`, `try again`, `encouraging`, `never dark`
**목표 길이**: 1.2 ~ 1.8초
**트리밍 가이드**: 하행 멜로디(~1.0초) + 마지막 음 여운(~0.5초). 게임 오버 UI 팝업과 동기화.

---

### 8. 특수 젬 생성 (Special Gem Creation)

**게임 메커닉**: 4매치 이상에서 특수 블록(Drill/Bomb/Rainbow/XBlock/Laser)이 생성될 때. SpawnDuration=0.25s, SpawnOvershoot=1.15, SpawnStartScale=0.5, SpawnGlowAlpha=0.4.

**ElevenLabs Prompt:**
```
A magical sparkle transformation sound, about 0.5 seconds long. Like a regular candy transforming into a special magical gem with a brief flash of light. Starts with a soft crystalline shimmer building up quickly, then a bright, satisfying "ting-GLING" at the peak, followed by a gentle sparkling tail. Think of a fairy godmother touching something with her wand and it transforming with a burst of tiny stars. The crescendo should peak at about 0.2 seconds in (matching the spawn animation). Warm, magical, and rewarding. Music box and celesta tones with added sparkle texture. Not harsh or sudden. The overall feeling should be "something special just appeared!" Suitable for a cute pastel puzzle game with a soft, warm aesthetic.
```

**핵심 키워드**: `magical transformation`, `crystalline shimmer`, `ting-GLING`, `fairy godmother`, `burst of tiny stars`, `rewarding`, `something special`
**목표 길이**: 0.4 ~ 0.6초
**트리밍 가이드**: 크레셴도 피크를 SpawnDuration(0.25s) 시점의 SpawnOvershoot(1.15배) 순간에 정확히 맞추기. GlowAlpha(0.4) 빛남 효과와 shimmer 꼬리 동기화.

---

### 9. 캐스케이드 콤보 (Cascade Combo)

**게임 메커닉**: 연쇄 매칭 발생 시. cascadeDelay=0.1s 간격으로 연속 제거. CascadeMultiplier: depth 0=1.0x → 4+=1.8x. comboMultiplierIncrement=0.5.

**ElevenLabs Prompt:**
```
A playful ascending musical scale sound, about 1 second long, like a xylophone or marimba being played with soft mallets going up a cheerful major pentatonic scale. Each note should be distinct and separated by a brief gap (about 0.1 seconds between notes). The notes ascend from low to high, getting progressively brighter, louder, and more sparkly. Think of a cartoon character running up a magical staircase made of pastel-colored macaroons, each step making a cute musical tone. The final highest note should have extra shimmer and sparkle. Warm, playful, and increasingly exciting. Wood percussion tones (like a toy xylophone) mixed with gentle bell-like overtones. Not metallic or harsh. Suitable for a cute pastel puzzle game cascade combo system.
```

**핵심 키워드**: `ascending pentatonic`, `xylophone`, `soft mallets`, `macaroon staircase`, `progressively brighter`, `toy xylophone`, `bell overtones`
**목표 길이**: 0.8 ~ 1.2초
**트리밍 가이드**: 각 음을 개별적으로 잘라내어 cascadeDelay(0.1s)에 맞춰 프로그래밍 방식으로 재생하는 것도 가능. 또는 통째로 사용 시 0.1초 간격이 자연스럽게 들어간 버전을 선택. CascadeMultiplier에 따라 각 음의 볼륨을 코드에서 조절.

---

## ElevenLabs 프롬프트 요약 테이블

| # | 사운드 | 목표 길이 | 핵심 무드 키워드 | 참조 악기/음색 |
|---|--------|-----------|-----------------|---------------|
| 1 | 드릴 발동 | 0.4-0.6s | cute, toy, candy drill | 뮤직박스 + 장난감 모터 |
| 2 | 폭탄 폭발 | 0.25-0.4s | poof, confetti, cotton candy | 만화 폭발 + 글리터 |
| 3 | 레이저 빔 | 0.4-0.6s | fairy wand, starlight, dreamy | 글라스 하모니카 |
| 4 | 도넛/레인보우 | 0.6-1.0s | rainbow, prism, expanding waves | 윈드차임 + 뮤직박스 |
| 5 | X블록 | 0.5-0.7s | crossing sparkle, fairy dust | 첼레스타 + 글로켄슈필 |
| 6 | 스테이지 클리어 | 1.5-2.5s | celebration, cozy, congratulatory | 뮤직박스 팡파레 |
| 7 | 게임 오버 | 1.2-1.8s | gentle, comforting, encouraging | 뮤직박스 와인딩 다운 |
| 8 | 특수 젬 생성 | 0.4-0.6s | magical transformation, rewarding | 첼레스타 + 스파클 |
| 9 | 캐스케이드 콤보 | 0.8-1.2s | playful, ascending, exciting | 실로폰 + 벨 |

---

## 공통 후처리 가이드라인

### 전체 사운드에 적용할 마스터 프로세싱

| 처리 단계 | 설정 | 목적 |
|-----------|------|------|
| **High-pass Filter** | 80Hz cutoff, 12dB/oct | 불필요한 저역 잡음 제거 (착지 사운드 제외) |
| **Low-pass Filter** | 8kHz cutoff, 6dB/oct | 전체적으로 부드러운 톤 유지 |
| **Compression** | Ratio 2:1, Attack 10ms, Release 50ms | 급격한 피크 방지, 일관된 볼륨 |
| **Master Reverb** | Small Room, Decay 0.3s, Wet 15% | 공간감 통일 (개별 리버브 후에 적용) |
| **Limiter** | -1dB ceiling | 클리핑 방지 |

### 파스텔 톤 사운드의 3가지 핵심 원칙

1. **부드러운 어택**: 모든 사운드의 Attack을 0 ~ 0.05s 범위로 유지. 절대 "찌르는" 느낌이 없어야 함
2. **따뜻한 음색**: 3kHz 이상의 고역을 자연스럽게 롤오프. 날카롭거나 금속성이 느껴지면 Low-pass 강화
3. **적절한 공간감**: 모든 사운드에 약간의 리버브를 적용하여 "공기 중에 떠 있는" 느낌. 드라이한 사운드 금지

### 볼륨 우선순위 (믹싱 가이드)

```
[가장 큰] 스테이지 클리어 팡파레
         5매치 성공
         4매치 성공
         경고 비프
         3매치 성공
         버튼 클릭
         특수 블록 발동 (드릴/폭탄/레이저/도넛/X)
         블록 파괴/팝
         게임 오버
         블록 착지
         캐스케이드 (단일 음)
[가장 작은] 블록 회전
```

### Unity AudioSource 권장 설정

| 파라미터 | 값 | 비고 |
|---------|-----|------|
| **Spatial Blend** | 0 (2D) | UI/퍼즐 게임이므로 완전 2D 오디오 |
| **Priority** | 128 (기본) | 특수 블록 발동은 64 (높음) |
| **Max Distance** | N/A (2D) | |
| **AudioClip LoadType** | Decompress On Load | 짧은 SFX이므로 메모리 로드 |
| **Compression Format** | Vorbis (Quality 70%) | 파일 크기와 품질 밸런스 |

---

## 파일 명명 규칙 (권장)

```
SFX_UI_ButtonClick.wav
SFX_UI_PopupOpen.wav
SFX_Block_Rotate.wav
SFX_Match_Success3.wav
SFX_Match_Success4.wav
SFX_Match_Success5.wav
SFX_Match_Fail.wav
SFX_Block_Destroy.wav
SFX_Block_Land.wav
SFX_Combo_Cascade_C5.wav
SFX_Combo_Cascade_D5.wav
SFX_Combo_Cascade_E5.wav
SFX_Combo_Cascade_G5.wav
SFX_Combo_Cascade_A5.wav
SFX_Combo_Cascade_C6.wav
SFX_Warning_Beep.wav
SFX_Special_DrillActivate.wav
SFX_Special_BombExplode.wav
SFX_Special_LaserBeam.wav
SFX_Special_DonutRainbow.wav
SFX_Special_XBlock.wav
SFX_Special_GemCreate.wav
SFX_Stage_Clear.wav
SFX_Stage_GameOver.wav
SFX_Combo_CascadeFull.wav
```

---

*이 문서는 MatchMine 헥사 퍼즐 게임의 소프트 파스텔 테마에 최적화된 SFX 제작 사양서입니다.*
*모든 타이밍 수치는 실제 게임 코드(BlockRemovalSystem, VisualConstants)에서 추출한 값과 동기화되어 있습니다.*
