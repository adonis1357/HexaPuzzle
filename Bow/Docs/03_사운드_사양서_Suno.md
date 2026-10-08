# 「활」(Hwal) 사운드 사양서 — Suno BGM + SFX 프로시저럴 폴백

> 작성: 사운드 디렉터 | 기준 문서: `00_프로젝트_브리프.md` (4. 사운드 방향 확정) | 작성일 2026-10-08
> 코드(.cs) 구현은 `programmer`가 담당한다. 이 문서는 설계·사양만 정의한다. 확정 수치를 바꾸는 항목은 7장 "변경 제안"에 근거와 함께 적었다.

---

## 1. 사운드 컨셉 & 믹스 정책

### 1.1 컨셉: 「먹이 번지는 소리」
시각 컨셉(먹과 한지)을 청각으로 옮긴다. **여백이 많고, 한 번의 소리가 또렷하며, 잔향은 한지에 스며들듯 짧게** 끝난다.

| 요소 | 방향 |
|------|------|
| 국악 악기 | 대금(공기 섞인 대나무 피리, 주선율), 해금(두 줄 찰현, 애수·긴장 오스티나토), 가야금(뜯는 소리, 아르페지오·포인트), 장구(리듬 골격), 북(저음 심장박동·타격) |
| 현대 결합 | 로우파이 텍스처(약한 테이프 새추레이션, 먼지 노이즈), 시네마틱 서브 펄스, 절제된 리버브. 신스 리드·EDM 요소 금지 |
| 조성 | 전 트랙 **D 기준**으로 통일 (로비·대전·패배 = D minor, 승리 = D major). 모든 SFX 음정도 D 음계(D·F#/F·A)를 따라 BGM과 충돌하지 않게 한다 |
| 무드 곡선 | 로비(고요·명상) → 대전(집중·긴장) → HP 30% 이하(절박) → 승리(청명한 자부심) / 패배(여운 있는 쓸쓸함) |
| 금지 | 보컬, 8비트/칩튠, 과한 저역(서브 60Hz 이하 에너지 과다), 금관 팡파르 |

### 1.2 믹스 정책
| 항목 | 값 | 비고 |
|------|----|------|
| BGM 파일 마스터 | **−14 LUFS-I**, True Peak ≤ −1 dBTP | 후처리 단계에서 노멀라이즈 (8장) |
| **플레이 중 전체 믹스 목표** | **−12 LUFS (Short-term 평균, BGM+SFX 합산)** | BGM 단독 −14 + SFX 합산분 약 +2 LU. 실기기 측정 후 BGM 버스 ±2dB 조정 |
| 버스 구성 | Master → BGM(1.0) / SFX_Game(1.0) / SFX_UI(0.9) / Ambience(0.8) | AudioMixer 없이 코드 스칼라 곱으로 구현 가능 (P2에서 AudioMixer 이전) |
| SFX 윈도우 확보 | BGM 후처리 시 2.5~3.5kHz −2dB(넓은 Q), 40Hz 하이패스 | 화살 휘파람(1.5~4kHz)·피격음이 BGM에 묻히지 않게 |
| 더킹(SFX 우선) | 우선순위 ≥8 SFX 발생 시 BGM ×0.63(−4dB), Attack 0.03s / Hold 0.35s / Release 0.6s | 머리 피격은 ×0.5(−6dB), Hold 0.6s / Release 0.9s |
| 스팅어 | 승리/패배 스팅어 시작 0.3s 전 BGM 페이드아웃, 스팅어 후 결과 BGM 진입 | 6.4 참조 |
| 동시 발음 | 최대 12보이스 (풀 초기 10, 자동 확장 한도 14) | 초과 시 우선순위 낮은 것 → 오래된 것 순으로 스틸 |
| 유저 설정 | BGM / SFX 슬라이더 분리, PlayerPrefs 저장 (`hwal_bgm_vol`, `hwal_sfx_vol`) | 기본 BGM 0.8, SFX 1.0 |
| 공간화 | 자기 소리 Pan −0.35, 상대 소리 Pan +0.35 (상대 발사음 볼륨 ×0.8) | 화면 좌=플레이어, 우=상대 |

---

## 2. ★ Suno AI 프롬프트 시트

### 2.0 공통 사항
- **버전**: Suno v4.5 이상 권장 (Exclude Styles 필드, 긴 곡 생성 지원). 구버전은 Exclude 필드가 없으므로 Style 끝에 `no vocals`를 덧붙인다.
- **모드**: Custom Mode ON, **Instrumental ON**. Instrumental ON 시 가사창이 숨겨지는 버전은 메타태그를 생략하고 Style 키워드로 구조를 유도한다. 가사창이 남아있다면 아래 "Lyrics 칸" 코드블록을 그대로 입력.
- **메타태그 정책**: Suno에 `[Loop]` 태그는 없다(공식 지원 아님). `[Intro]` `[Instrumental]` `[Outro]` `[End]`는 구조 힌트일 뿐 **보장되지 않는다** → 루프는 항상 후처리(8장)로 만든다.
- **길이 제어**: 길이 지정 파라미터가 없다. 2:00~3:00 길이로 생성한 뒤 DAW에서 **정수 마디만큼 잘라** 쓴다. BPM·조성 프롬프트는 "경향"일 뿐이므로 생성물의 실제 BPM을 DAW로 측정하고 ±4% 내에서 타임스트레치로 정확히 맞춘다 (±4% 초과 시 폐기).
- **라이선스**: 상업 이용은 유료 플랜(Pro 이상)으로 생성한 곡만 가능하다. **반드시 유료 플랜에서 생성**하고 곡 ID·생성일·플랜을 2.7 로그에 기록한다 (현재 약관 재확인 필요).
- **공통 Exclude (복사용)**:
```
vocals, lyrics, singing, rap, choir, EDM drop, trap hi-hats, distorted guitar, K-pop, pop punk, 8-bit, brass fanfare
```

### 2.1 요약표
| 트랙 | 파일명 | 목적 | 길이(루프) | BPM | 조성 | 정확 샘플수(44.1kHz) |
|------|--------|------|-----------|-----|------|---------------------|
| 로비 | `bgm_lobby` | 메뉴·매칭 대기, 명상적 대기 | 24마디 = 80.000s | 72 | D minor | 3,528,000 |
| 대전(평상) | `bgm_duel` | 경기 전반, 집중 긴장 | 32마디 = 80.000s | 96 | D minor | 3,528,000 |
| 대전(위기) | `bgm_duel_tense` | HP 30% 이하 전환용 (bgm_duel과 **마디 동기**) | 32마디 = 80.000s | 96 | D minor | 3,528,000 |
| 승리 | `bgm_victory` | 결과 화면(승), 청명한 여운 | 10마디 = 30.000s | 80 | D major | 1,323,000 |
| 패배 | `bgm_defeat` | 결과 화면(패), 쓸쓸한 여운 | 8마디 = 32.000s | 60 | D minor | 1,411,200 |

### 2.2 로비 — `bgm_lobby`
**Style of Music (복사용, 180자)**
```
Korean traditional instrumental, daegeum bamboo flute lead, gayageum plucks, soft haegeum pads, light janggu, ink-wash minimal, warm lo-fi texture, calm meditative, 72 BPM, D minor
```
**Exclude Styles**: 공통 Exclude + `, heavy percussion, fast tempo`
**Lyrics 칸 (메타태그만)**
```
[Intro]
[Instrumental]
```
- 구조 의도: 도입 4마디 후 대금이 느린 주선율, 가야금이 8분 아르페지오. 드럼은 장구 가벼운 터치만.
- **생성 횟수**: 4회(후보 8곡). **선별**: 2.6 기준표 적용, 특히 "음량 변화가 완만한가(빌드업·클라이맥스 없음)" 우선.
- 루프 구간: 도입 4마디를 건너뛴 마디 5~28 (24마디) 사용 → 8장 루프 절차.

### 2.3 대전(평상) — `bgm_duel`
**Style of Music (복사용, 173자)**
```
Cinematic Korean traditional, tense steady pulse, buk drum, janggu rhythm, haegeum ostinato, plucked gayageum, sparse daegeum, dark lo-fi, 96 BPM, D minor, focused, loopable
```
**Exclude Styles**: 공통 Exclude + `, big climax, orchestral swell, riser`
**Lyrics 칸**
```
[Intro]
[Instrumental]
[Instrumental]
```
- 의도: 일정한 맥박(북 4분음표 약하게 + 장구 엇박) 위에 해금 2~4음 오스티나토. 대금은 드문 롱톤. **고조·라이저 금지** (실시간 플레이 방해, 루프 이음새 노출).
- **생성 횟수**: 6회(후보 12곡). 가장 중요한 트랙이므로 최다 생성.
- 루프 구간: 마디 5~36 (32마디).

### 2.4 대전(위기) — `bgm_duel_tense` (HP 30% 이하 전환)
**Style of Music (복사용, 172자)**
```
Intense cinematic Korean traditional, driving buk and janggu, fast haegeum tremolo, staccato gayageum, rising daegeum, heartbeat bass, urgent, 96 BPM, D minor, high tension
```
**Exclude Styles**: 공통 Exclude + `, ballad, slow, calm`
**Lyrics 칸**: 2.3과 동일.
- **동기 생성 방법** (우선순위 순)
  1. **(권장) Cover/Extend 활용**: 선별된 `bgm_duel` 원본을 Suno에 업로드/참조해 위 프롬프트로 Cover 생성 → 템포·화성 골격이 따라와 마디 동기가 쉽다 (기능 제공 여부·플랜은 Suno 현행 UI에서 확인).
  2. 독립 생성 후 DAW에서 `bgm_duel`과 **BPM 96.000 / 첫 다운비트 0.000s / D minor 일치** 확인, 불일치 시 폐기·재생성.
- 두 파일은 **정확히 같은 샘플 길이(3,528,000)** 여야 한다 (런타임에서 `timeSamples` 동기 전환).
- 상승 포인트: 8분음표 드라이브, 해금 트레몰로, 심장 박동 베이스(저역은 80Hz 이상으로 제한). **음량은 bgm_duel 대비 +1dB 이내**로 노멀라이즈(체감 긴장은 밀도로 만든다).
- **생성 횟수**: 5회(후보 10곡) + Cover 시도 2회.

### 2.5 승리 — `bgm_victory` / 패배 — `bgm_defeat`
**승리 Style (복사용, 162자)**
```
Triumphant Korean traditional, bright gayageum arpeggios, uplifting daegeum melody, warm janggu, gentle bells, hopeful serene pride, 80 BPM, D major, instrumental
```
**승리 Exclude**: 공통 Exclude + `, aggressive, dark, minor key`
**패배 Style (복사용, 163자)**
```
Somber Korean traditional, slow haegeum lament, sparse low daegeum, deep soft buk, sparse gayageum, melancholic, fading, empty space, 60 BPM, D minor, instrumental
```
**패배 Exclude**: 공통 Exclude + `, upbeat, bright, major key, dramatic climax`
**Lyrics 칸 (둘 다)**
```
[Intro]
[Instrumental]
```
- 승리: 결과 화면에서 반복 재생되므로 **팡파르가 아닌 안정된 평온한 승리감**. 스팅어(6.4)가 환호를 담당하고 이 곡은 그 후 2.0s 뒤 페이드인.
- 패배: 해금 롱톤 + 가야금 드문 음. 처지는 느낌이되 루프 시 우울이 과하지 않게 템포 60 유지.
- **생성 횟수**: 각 4회(후보 8곡). 루프 구간은 도입 직후부터 승리 10마디 / 패배 8마디.

### 2.6 선별 기준표 (곡당 0~2점, 합계 ≥ 9/12 채택)
| 항목 | 2점 | 0점 |
|------|-----|-----|
| 루프 이음새 | 앞뒤 마디 에너지·악기 구성 유사, 크로스페이드로 이음새 안 들림 | 끝에 페이드아웃/리타르단도/새 섹션 |
| 템포 안정 | 전 구간 BPM 편차 ±1% 이내 | 중간 가속/감속 |
| 저음 | 80Hz 이하 에너지가 전체 −20dB 이하, 북이 뭉치지 않음 | 서브 럼블로 스피커 먹먹 |
| SFX 여백 | 1.5~4kHz에 시끄러운 리드 없음 (대금은 한 음씩) | 해금·대금이 고음역에서 계속 울림 |
| 악기 정체성 | 5개 악기 중 3개 이상이 국악 음색으로 식별 | 일반 신스/기타로 변질 |
| 결함 | 보컬 환청·글리치·클리핑 없음 | 보컬/글리치 있음 |

### 2.7 생성 로그 (작업자가 채움)
| 트랙 | Suno 곡 ID/URL | 모델 버전 | 플랜 | 측정 BPM | 채택 여부 | 최종 LUFS |
|------|---------------|----------|------|---------|----------|----------|
| bgm_lobby | | | | | | |
| bgm_duel | | | | | | |
| bgm_duel_tense | | | | | | |
| bgm_victory | | | | | | |
| bgm_defeat | | | | | | |

---

## 3. 파일 배치 · 명명 · Unity 임포트

### 3.1 경로 & 명명
- 소문자 snake_case, ASCII 전용, 접두사 `bgm_` / `sfx_`. 변형은 `_01`, `_02` 접미사.
- 로드: `Resources.Load<AudioClip>("Audio/SFX/sfx_release")` (확장자 없이). **파일이 없으면 null → 프로시저럴 폴백 생성 → 캐시**.
```
Bow/Assets/Resources/Audio/BGM/bgm_lobby.ogg
Bow/Assets/Resources/Audio/BGM/bgm_duel.ogg          bgm_duel_tense.ogg
Bow/Assets/Resources/Audio/BGM/bgm_victory.ogg        bgm_defeat.ogg
Bow/Assets/Resources/Audio/SFX/sfx_*.wav              (4장 목록)
```
- BGM 파일이 없으면 **무음 + 로그 1회**(BGM은 프로시저럴 폴백 없음, 7장 P2에서 최소 드론 검토).
- BGM은 **MP3 금지** (인코더 딜레이로 루프 갭 발생). OGG(Vorbis) 사용.

### 3.2 Unity 임포트 권장값
| 대상 | Load Type | Compression Format | Quality | Sample Rate | 기타 |
|------|-----------|-------------------|---------|-------------|------|
| `bgm_duel`, `bgm_duel_tense` | Compressed In Memory | Vorbis | 70% (≈q5) | Preserve | Preload Audio Data ON, Load In Background OFF, Force To Mono OFF. 전환 시 `timeSamples` 이동 시 히치 방지 |
| `bgm_lobby`, `bgm_victory`, `bgm_defeat` | Streaming | Vorbis | 70% | Preserve | Preload OFF, Load In Background ON |
| 짧은 SFX (≤0.5s) | Decompress On Load | PCM | — | Preserve (44.1k) | Force To Mono ON, Preload ON |
| 중간 SFX (0.5~1.5s) | Decompress On Load | ADPCM | — | Preserve | Mono ON |
| 루프 SFX (wind, draw_loop, tension_loop, whistle) | Compressed In Memory | Vorbis | 60% | Preserve | Mono ON, Loop는 AudioSource에서 설정 |
- Project Settings > Audio: **DSP Buffer Size = Best Latency** (발사·피격음 즉시성, 모바일 지연 최소화), Max Real Voices 32 / Virtual 64.
- 모든 SFX AudioSource: Spatial Blend 0 (2D), Doppler Level 0 (도플러는 6.1의 수식으로 직접 피치 제어).

---

## 4. SFX 목록

우선순위: 0(낮음)~10(높음). 볼륨은 SFX 버스 기준 선형값. 피치는 AudioSource.pitch 배율 범위(무작위 균등분포). "동시"는 같은 ID 최대 동시 재생 수.

| ID (`sfx_…`) | 트리거 이벤트 | 길이 | 볼륨 | 피치 범위 | 우선순위 | 동시 | 비고 |
|--------------|--------------|------|------|-----------|---------|------|------|
| `bow_draw_start` | 드래그 시작(터치 다운 후 이동 첫 프레임) | 0.18s | 0.55 | 0.96~1.04 | 3 | 1 | 활을 잡고 당기기 시작 |
| `bow_draw_loop` | 드래그 중 (루프) | 1.2s 루프 | 0.20+0.30×힘 | 0.90+0.35×힘 | 2 | 1 | 나무 삐걱임. 손 떼면 0.08s 페이드아웃 |
| `string_tension_loop` | 드래그 중 (루프, draw_loop와 병행) | 1.0s 루프 | 0.12+0.25×힘 | 1.0+1.0×힘 (D3→D4) | 2 | 1 | 시위의 가는 긴장음, 힘이 클수록 음정 상승 |
| `meter_tick` | 추 미터 작동 중 period/12 간격 | 0.05s | 0.40 | 6.2 공식 (1.0~1.5) | 4 | 2 | 중앙 근접 시 피치 상승. 최소 재생 간격 40ms |
| `release` | 발사(탭 또는 3초 자동 발사) | 0.40s | 0.85 | 0.95~1.05 | 8 | 2 | 시위 튕김 + 바람 가르기 + 활 몸통 진동 |
| `arrow_whistle` | 화살 비행 중 (루프) | 0.8s 루프 | 6.1 거리 공식 (≤0.40) | 6.1 도플러 공식 | 3 | 3 | 화살마다 독립 보이스, 명중/소멸 시 0.05s 페이드아웃 |
| `wind_loop` | 경기 시작 카운트다운~종료 | 4.0s 루프 | 6.1 공식 (0.10~0.60) | 6.1 공식 (0.90~1.15) | 1 | 1 | 풍속 연동, 경기 종료 시 1.0s 페이드아웃 |
| `hit_body` | 몸통 피격 (25 데미지) | 0.32s | 0.90 | 0.93~1.07 | 9 | 2 | |
| `hit_head` | 머리 피격 (40 데미지) | 0.55s | 1.00 | 0.95~1.05 | 10 | 2 | 둔탁함 + 잉크 스플래시. 더킹 −6dB, 히트스톱 연동 |
| `perfect_bell` | Perfect 발사 확정(탭 abs(p)<0.08) 시 발사음 +0.05s / Perfect 명중 시 재생(×0.55, 피치 1.5) | 1.2s | 0.75 | 발사 시 1.0 고정 | 9 | 2 | 금박색 금속 종, 음정 고정(A5) |
| `miss_ground` | 화살 지면/화면 밖 착탄 | 0.30s | 0.60 | 0.90~1.10 | 4 | 3 | 땅에 박히며 화살대 떨림 |
| `reload_ready` | 장전 완료 (t_r 도달) | 0.30s | 0.50 | 1.0 고정 | 5 | 1 | 나무 "딱" + 맑은 D5. 호흡 효율 누적 시작 신호 |
| `breath_peak` | 호흡 정점 도달 (초과 대기 h = 10s) | 1.0s | 0.60 | 1.0 고정 | 6 | 1 | 황금 싱잉볼, 금색 링 정점 연출과 동기. 10초 전에 발사하면 재생 안 함 |
| `countdown_tick` | 카운트다운 3 · 2 · 1 각각 | 0.22s | 0.70 | 3:0.94 / 2:1.0 / 1:1.06 | 7 | 1 | 같은 원음, 피치만 상승 |
| `countdown_go` | "시작" | 0.70s | 0.90 | 1.0 고정 | 8 | 1 | 북 타격 + 밝은 D 코드 |
| `victory_stinger` | 결과: 승리 | 2.5s | 0.90 | 1.0 고정 | 10 | 1 | D major 아르페지오, BGM 페이드아웃 후 |
| `defeat_stinger` | 결과: 패배 | 2.8s | 0.90 | 1.0 고정 | 10 | 1 | D minor 하강 + 저음 북 |
| `ui_click` | UI 버튼 눌림 | 0.09s | 0.60 | 0.97~1.03 | 3 | 2 | SFX_UI 버스 |
| `ui_back` (P2) | 뒤로가기/취소 | 0.09s | 0.55 | 0.97~1.03 | 3 | 1 | SFX_UI 버스 |
| `time_warning` (P2) | 남은 시간 10초부터 1초마다 | 0.12s | 0.45 | 1.0+0.02×(10−남은초) | 6 | 1 | 마른 나무 틱 |

---

## 5. 프로시저럴 폴백 합성 사양

### 5.1 공통 규칙 (ProceduralAudio 패턴 준용)
- 샘플레이트 **44100Hz**, 모노 1채널, 레이어 합산 후 **Normalize(피크 0.9)** → `ApplyFades(64, 128 샘플)` → 필요 시 `ApplyReverb`. 클립은 최초 요청 시 1회 생성해 캐시한다.
- 파형: `sine / tri(삼각) / saw / noise(백색)`. 하모닉 합성은 `ToneWithHarmonics`(예: `h2=0.25`는 2배음 진폭 0.25).
- **주파수 `a→b`**: 길이에 걸친 지수 보간(위상은 누적 적분으로 계산, 위상 불연속 금지).
- **ADSR(A,D,S,R)**: 단위 초, S는 유지 레벨(0~1). 레이어 길이 − (A+D+R)가 유지 구간. 기존 `ADSR(tNorm,…)`은 정규화 시간이므로 **초 ÷ 레이어 길이**로 변환해서 호출. `S=0`이면 타격음(감쇠형).
- **필터**: `LP(fc)`/`HP(fc)` = 1극 필터, α = 1 − e^(−2π·fc/44100). `BP(fc,Q)` = 노이즈를 HP·LP 직렬로 근사(대역폭 ≈ fc/Q). `LP a→b`는 시간에 따라 선형 스윕.
- **delay**: 레이어 시작 오프셋(초). **amp**: 레이어 합산 전 진폭(0~1).
- **루프 클립**: LFO 주기를 클립 길이의 정수 분할로 선택하고, 노이즈 포함 시 양 끝 50ms **등전력 크로스페이드**로 이음새를 제거한다.
- 사용 음정은 D 음계: D3=146.83 / D5=587.33 / A5=880 / D6=1174.66 Hz.

### 5.2 레이어 표
| ID | # | 파형 | 주파수 시작→끝 (Hz) | 길이 | ADSR (A,D,S,R) | 필터 | amp | delay | 비고 |
|----|---|------|--------------------|------|---------------|------|-----|-------|------|
| bow_draw_start | 1 | noise | — | 0.18 | 0.02, 0.06, 0.40, 0.08 | BP(900, Q1.5) | 0.50 | 0 | 천/나무 마찰 |
| | 2 | tri | 110→150 | 0.18 | 0.01, 0.05, 0.50, 0.10 | LP(800) | 0.35 | 0 | 활 몸통 삐걱 |
| | 3 | sine | 392 | 0.06 | 0.002, 0.03, 0, 0.02 | — | 0.15 | 0.02 | 시위 터치 |
| bow_draw_loop | 1 | noise | — | 1.2 | 루프(페이드 50ms) | BP(700, Q2) + AM 5Hz 깊이 0.35 | 0.25 | 0 | 삐걱 트레몰로 (1.2s에 6사이클) |
| | 2 | saw | 82 ±3Hz (0.833Hz LFO) | 1.2 | 루프 | LP(400) | 0.30 | 0 | 나무 긴장 저음 |
| string_tension_loop | 1 | sine | 147 (+h2 0.25, h3 0.10) | 1.0 | 루프 | LP(1800) | 0.50 | 0 | 정수 사이클 147회 |
| | 2 | sine | 148 | 1.0 | 루프 | LP(1800) | 0.30 | 0 | 1Hz 비팅으로 떨림감 |
| meter_tick | 1 | tri | 660→520 | 0.05 | 0.001, 0.015, 0.25, 0.025 | LP(3500) | 0.60 | 0 | 나무 딱. 재생 피치로 음정 상승 |
| | 2 | noise | — | 0.008 | 0, 0.004, 0, 0.004 | HP(2500) | 0.20 | 0 | 클릭 어택 |
| release | 1 | sine | 392→370 (+h2 0.25, h3 0.12) | 0.40 | 0.001, 0.25, 0, 0.14 | — | 0.50 | 0 | 시위 튕김 (감쇠형) |
| | 2 | noise | — | 0.35 | 0.02, 0.12, 0.30, 0.20 | BP 600→3200 (Q1.2) | 0.50 | 0.01 | 바람 가르기 상승 스윕 |
| | 3 | sine | 90→55 | 0.08 | 0.001, 0.05, 0, 0.03 | — | 0.40 | 0 | 활 몸통 반동 |
| arrow_whistle | 1 | noise | — | 0.8 | 루프(페이드 50ms) | BP(2400, Q6) | 0.35 | 0 | 좁은 대역 휘파람 |
| | 2 | sine | 1900 (±40Hz, 3Hz LFO) | 0.8 | 루프 | LP(6000) | 0.12 | 0 | 가는 톤 |
| | — | AM | 30Hz 깊이 0.2 | | | | | | 깃 떨림 (0.8s에 24사이클) |
| wind_loop | 1 | noise | — | 4.0 | 루프(페이드 250ms) | LP(1200) → BP(500, Q0.7), 컷오프 LFO 0.25Hz ±200 | 0.60 | 0 | 낮은 바람 몸통 |
| | 2 | noise | — | 4.0 | 루프 | HP(1500)+LP(5000), AM 0.5Hz 깊이 0.25 | 0.20 | 0 | 풀잎 스치는 소리 |
| hit_body | 1 | sine | 140→60 | 0.32 | 0.001, 0.08, 0.20, 0.20 | — | 0.70 | 0 | 둔탁한 타격 |
| | 2 | noise | — | 0.15 | 0.001, 0.04, 0.10, 0.10 | LP(1200) | 0.45 | 0 | 천/살 타격감 |
| | 3 | sine | 520 (비브라토 14Hz ±25Hz) | 0.25 | 0.005, 0.05, 0.40, 0.20 | — | 0.15 | 0.04 | 박힌 화살대 떨림 |
| hit_head | 1 | sine | 110→45 | 0.55 | 0.001, 0.10, 0.25, 0.30 | — | 0.85 | 0 | 두개골 둔탁음 |
| | 2 | noise | — | 0.05 | 0, 0.015, 0, 0.03 | HP(1800)+LP(7000) | 0.50 | 0 | 크랙 어택 |
| | 3 | noise | — | 0.40 | 0.01, 0.12, 0.30, 0.30 | BP 1400→400 (Q0.9) | 0.40 | 0.03 | 잉크 스플래시 "촤악" |
| | 4 | sine ×2 | 220 + 331 (비정수배) | 0.45 | 0.01, 0.20, 0.30, 0.30 | — | 0.20 | 0.02 | 공포감 있는 저음 링잉 |
| perfect_bell | 1 | sine | 880 | 1.2 | 0.001, 1.0, 0, 0.10 | — | 0.50 | 0 | 기본음 A5 (감쇠 1.0s) |
| | 2 | sine | 1760 | 0.8 | 0.001, 0.7, 0, 0.10 | — | 0.25 | 0 | |
| | 3 | sine | 2429 (×2.76) | 0.6 | 0.001, 0.5, 0, 0.10 | — | 0.20 | 0 | 금속 비정수 부분음 |
| | 4 | sine | 4752 (×5.4) | 0.3 | 0.001, 0.25, 0, 0.05 | — | 0.08 | 0 | |
| | 5 | noise | — | 0.005 | 0, 0.005, 0, 0 | HP(4000) | 0.20 | 0 | 타격 어택. 전체 트레몰로 6Hz 깊이 0.08, `ApplyReverb(40ms, 0.30, 3)` |
| miss_ground | 1 | sine | 160→80 | 0.20 | 0.001, 0.04, 0.20, 0.10 | — | 0.50 | 0 | 흙에 박히는 퍽 |
| | 2 | noise | — | 0.08 | 0, 0.02, 0.10, 0.05 | LP(900) | 0.35 | 0 | 흙 마찰 |
| | 3 | sine | 300→260 (비브라토 18Hz ±15Hz) | 0.27 | 0.003, 0.12, 0.20, 0.15 | — | 0.25 | 0.03 | 화살대 떨림 |
| reload_ready | 1 | tri | 520→400 | 0.05 | 0.001, 0.02, 0, 0.03 | LP(4000) | 0.50 | 0 | 나무 딱 |
| | 2 | sine | 587.33 (h2 0.20) | 0.27 | 0.005, 0.10, 0.30, 0.15 | — | 0.30 | 0.03 | 맑은 D5 |
| breath_peak | 1 | sine | 587.33 (+2Hz 비팅 쌍) | 1.0 | 0.01, 0.50, 0, 0.30 | — | 0.40 | 0 | 싱잉볼 D5 |
| | 2 | sine | 880 | 0.85 | 0.01, 0.45, 0, 0.30 | — | 0.35 | 0.15 | A5 (완전5도 상승) |
| | 3 | sine | 2349 (트레몰로 7Hz 깊이 0.5) | 0.7 | 0.02, 0.30, 0, 0.30 | — | 0.06 | 0.10 | 금빛 반짝임. `ApplyReverb(60ms, 0.35, 3)` |
| countdown_tick | 1 | tri | 440→420 | 0.22 | 0.001, 0.05, 0.20, 0.10 | — | 0.60 | 0 | 장구 채 느낌 목탁음 |
| | 2 | sine | 880 | 0.10 | 0.001, 0.03, 0, 0.06 | — | 0.20 | 0 | 배음 |
| | 3 | noise | — | 0.005 | 0, 0.005, 0, 0 | LP(2000) | 0.20 | 0 | 어택 |
| countdown_go | 1 | sine | 100→50 | 0.70 | 0.001, 0.15, 0.20, 0.40 | — | 0.70 | 0 | 북 타격 |
| | 2 | noise | — | 0.04 | 0, 0.01, 0.20, 0.02 | BP(700, Q1) | 0.30 | 0 | 가죽 어택 |
| | 3 | sine ×3 | 587.33 / 880 / 1174.66 | 0.50 | 0.005, 0.15, 0.40, 0.40 | — | 0.25 각 | 0.02 | 밝은 D 코드 |
| victory_stinger | 1 | 아르페지오 | D5 587.33 @0.00 / F#5 739.99 @0.18 / A5 880 @0.36 / D6 1174.66 @0.54 | 각 1.2 | 0.002, 0.35, 0.15, 0.30 (가야금식 뜯음) | LP(5000) | 0.30 각 | 음별 | sine+h2 0.2 |
| | 2 | 코드 | D5+A5+D6+F#6(1479.98) | 1.9 | 0.05, 0.30, 0.60, 0.50 | LP(4000) | 0.18 각 | 0.54 | 지속 후 페이드 |
| | 3 | — | `perfect_bell` 레이어 재사용 | | | | 0.30 | 0.54 | `ApplyReverb(45ms, 0.30, 3)` |
| defeat_stinger | 1 | sine | 100→45 | 0.60 | 0.001, 0.15, 0.20, 0.30 | — | 0.50 | 0 | 저음 북 |
| | 2 | 하강 선율 | A4 440 @0.0 → F4 349.23 @0.4 → D4 293.66 @0.8 | 각 0.6 | 0.02, 0.25, 0.40, 0.30 | LP(2500) | 0.35 | 음별 | tri |
| | 3 | saw | 293.66 (비브라토 5Hz ±8Hz) | 2.0 | 0.15, 0.30, 0.50, 1.00 | LP(1800) | 0.25 | 0.80 | 해금식 찰현 롱톤 |
| | 4 | sine | 146.83 | 1.5 | 0.10, 0.40, 0.50, 0.80 | — | 0.30 | 1.30 | 낮은 D 마무리 |
| ui_click | 1 | tri | 780→620 | 0.09 | 0.001, 0.02, 0.20, 0.05 | LP(3500) | 0.45 | 0 | |
| | 2 | noise | — | 0.004 | 0, 0.004, 0, 0 | HP(3000) | 0.15 | 0 | |
| ui_back (P2) | 1 | tri | 620→480 | 0.09 | 0.001, 0.02, 0.20, 0.05 | LP(3000) | 0.45 | 0 | ui_click 하강형 |
| time_warning (P2) | 1 | tri | 1000 | 0.12 | 0.001, 0.03, 0.30, 0.06 | LP(4000) | 0.50 | 0 | |

> 폴백 품질 목표: 파일 SFX와 **음정·타이밍·음량은 동일**, 질감만 단순. 사운드 파일이 투입되면 이름이 같은 폴백은 자동 대체된다.

---

## 6. 다이내믹 오디오 규칙

### 6.1 풍속 → 바람/화살 오디오
입력 `w` = `WindModel.GetWind(t_match)` (브리프 2.3의 순수 함수; 별도 네트워크 동기화 불필요, 양쪽 클라가 동일 값을 낸다), `u = clamp01(abs(w) / 7)`.

| 파라미터 | 공식 |
|----------|------|
| 바람 볼륨 | `vol = 0.10 + 0.50 × u^1.3 + 0.12 × gust` |
| 돌풍 값 | `gust = clamp01(abs(dw/dt) / 3.5)` (dw/dt는 w(t) 해석적 미분. 최대 약 5.6 m/s²) |
| 바람 피치 | `pitch = 0.90 + 0.25 × u` (0.90~1.15) |
| 로우패스 | `AudioLowPassFilter.cutoff = 500 + 3500 × u^1.2` Hz (약풍 어둡게 → 강풍 밝게) |
| 팬 | `pan = clamp(w/7, −1, 1) × 0.3` (부는 방향 쪽으로 치우침) |
| 스무딩 | 모든 값에 `x += (target − x) × (1 − e^(−dt/0.35))` (unscaledDeltaTime) |

**화살 휘파람 (도플러 느낌)**: 리스너 = 플레이어 궁수 위치, `d` = 화살–리스너 거리(m), `v_r` = d의 시간 미분(멀어지면 +).
- `pitch = clamp(1 − 4 × v_r / 340, 0.80, 1.25)` (물리 도플러를 4배 과장; 22m/s에서 약 ±0.26)
- `vol = clamp(0.9 × 8 / (8 + d), 0.15, 0.90) × 0.45` (결과 최대 0.40)
- `pan = clamp((arrow.x − listener.x) / 12, −1, 1) × 0.6`
- 상대 화살이 다가올 때 피치가 내려가며 지나가는 느낌, 내 화살이 날아갈 때는 멀어지며 하강.

**추 미터 틱 피치**: 추 오프셋 p ∈ [−1, 1], 틱 재생 간격 = 추 주기/12 (주기 = 1.1×(1+0.3E)).
- `pitch = 2^( (1 − abs(p))^1.5 × 7 / 12 )` → 가장자리 1.0, 중앙 약 1.5 (완전5도 상승)
- Perfect 구간(abs(p) < 0.08): 피치 1.5 고정, 볼륨 ×1.3 (손가락 타이밍 힌트)

**호흡 연동**: 호흡 효율 E가 높으면 추가 느려지므로(주기 증가) 틱 간격이 길어져 자연스럽게 여유로운 리듬이 된다 — 별도 처리 불필요.

**힘 게이지 연동**: `bow_draw_loop` 피치 = `0.90 + 0.35 × 힘`, `string_tension_loop` 피치 = `1.0 + 1.0 × 힘`, 볼륨은 4장 표 공식. 힘은 드래그 길이(0~1).

**공간 규칙**: 내 발사·피격 = Pan −0.35, 상대 발사·피격 = Pan +0.35 (상대 쪽 볼륨 ×0.8). 상대 조준 소리(draw/tension)는 상대 상태를 알 수 없으므로 **재생하지 않는다**.

### 6.2 HP 30% 이하 BGM 전환
- **조건**: `min(내 HP, 상대 HP) ≤ 30` (HP는 한 번 내려가면 회복 없음 → 단방향 전환). 판정은 호스트 권위 HP 확정 이벤트 기준.
- **방식**: `bgm_duel`을 재생 중인 AudioSource A에서 현재 `timeSamples`를 읽어 `bgm_duel_tense`를 AudioSource B에 같은 `timeSamples`로 시작 → **1.5초 등전력 크로스페이드** (`A = cos(π/2·x)`, `B = sin(π/2·x)`, x = 0→1, unscaledDeltaTime).
- 두 파일의 길이·BPM이 같으므로 마디 정렬이 유지된다. 전환 직후 A는 `Stop()`하고 보유 중인 클립 참조는 유지.
- 피격 순간 더킹과 겹치면 더킹을 우선하고 크로스페이드 진행은 계속한다.
- 경기 종료 시: 현재 BGM 0.3s 페이드아웃 → 스팅어.

### 6.3 히트스톱 중 오디오
`Time.timeScale = 0`이어도 `AudioSource`는 멈추지 않는다. 아래를 지킨다.
1. **`AudioListener.pause`를 쓰지 않는다** (히트스톱 중 피격음이 끊김). 일시정지 메뉴에서만 `AudioListener.pause = true` + UI 보이스는 `ignoreListenerPause = true`.
2. 피격 SFX는 히트스톱 시작과 **같은 프레임**에 재생한다.
3. 모든 오디오 페이드/더킹/크로스페이드 코루틴은 `WaitForSecondsRealtime` 또는 `Time.unscaledDeltaTime` 사용.
4. 히트스톱 구간(제안: 머리 0.10s / 몸통 0.06s / Perfect 명중 +0.02s) 동안 BGM에 `LP 22kHz → 1.2kHz`(20ms) 적용, 종료 후 0.25s에 복귀. 바람 루프는 ×0.4.
5. 슬로모션(timeScale < 1)이 쓰이는 경우 BGM·바람의 피치에 `lerp(0.85, 1.0, timeScale)` 배율 적용, SFX는 변경하지 않음.
6. 예약 재생은 `AudioSettings.dspTime` 기준(`PlayScheduled`)으로 하고 `PlayDelayed`(timeScale 영향 가능성)는 쓰지 않는다.

### 6.4 씬/상태 흐름
| 상태 전환 | 오디오 동작 |
|-----------|-------------|
| 앱 시작 → 로비 | `bgm_lobby` 1.0s 페이드인, 루프 |
| 로비 → 매칭 시작 | 로비 BGM 0.8s 페이드아웃, `bgm_duel` 2.0s 페이드인(볼륨 50%) 시작 |
| 카운트다운 3-2-1 | `countdown_tick` ×3, `wind_loop` 1.0s 페이드인, 시작 시 `countdown_go` + BGM 100%로 상승 |
| 경기 중 | 6.1·6.2 규칙 |
| 경기 종료 | BGM·바람 0.3s 페이드아웃 → 스팅어 → 2.0s 후 `bgm_victory`/`bgm_defeat` 1.5s 페이드인(루프). 무승부는 `bgm_defeat` 대신 `bgm_lobby` 재사용 |
| 앱 백그라운드 | `AudioListener.pause = true`, 복귀 시 해제 |

---

## 7. 구현 우선순위 · 요청 사항 · 변경 제안

### 7.1 우선순위
| 등급 | 항목 |
|------|------|
| **P0** (프로토타입 필수) | AudioManager(BGM 2채널 + SFX 풀 + 폴백 캐시), `release` `hit_body` `hit_head` `perfect_bell` `miss_ground` `reload_ready` `bow_draw_start` `bow_draw_loop` `countdown_tick/go` `ui_click` `victory/defeat_stinger`, `wind_loop`(6.1 공식), BGM `bgm_lobby` `bgm_duel` 재생, 볼륨 설정 저장 |
| **P1** | `meter_tick` 피치 공식, `arrow_whistle` 도플러, `string_tension_loop`, `breath_peak`, `bgm_duel_tense` 1.5s 크로스페이드, BGM 더킹, 히트스톱 처리(6.3), `bgm_victory/defeat`, 보이스 우선순위 스틸 |
| **P2** | `time_warning`, `ui_back`, AudioMixer 이전, 루프 포인트 메타(루프 구간만 반복), BGM 파일 없음 시 최소 프로시저럴 드론, Suno 스템 기반 레이어 적응형 BGM, 햅틱(진동) 동기 |

### 7.2 프로그래머 요청서 (필요한 이벤트 훅)
| 이벤트 (제안 이름) | 전달 값 | 사용처 |
|-------------------|---------|--------|
| `OnDragStart / OnDragUpdate(power)` | 힘 0~1 | draw_start, draw_loop, tension_loop |
| `OnDragEnd` | — | 루프 정지 |
| `OnMeterTick(p, period)` | 추 오프셋, 현재 주기 | meter_tick (period/12 간격으로 발생시키거나 오디오가 자체 타이머) |
| `OnArrowLaunched(arrowId, isLocal, perfect)` | | release, perfect_bell, 휘파람 시작 |
| `OnArrowUpdate(arrowId, pos, vel)` | | 휘파람 도플러/볼륨/팬 |
| `OnArrowHit(arrowId, part, perfect, dmg)` / `OnArrowMiss(arrowId, pos)` | part = Body/Head | hit_*, miss_ground, 휘파람 정지 |
| `OnReloadReady` / `OnBreathPeak` | | reload_ready, breath_peak (h = 10s) |
| `OnWindChanged(w, dwdt)` | 매 프레임 | 6.1 |
| `OnHpChanged(isLocal, hp)` | | 6.2 전환 판정 |
| `OnCountdown(n)` / `OnMatchStart` / `OnMatchEnd(result)` | | 6.4 |
| `OnHitStop(duration)` | | 6.3 |
- 클래스 제안(`Bow.Audio`): `AudioManager`(싱글톤, 풀, 버스 볼륨), `SfxLibrary`(Resources 로드 → 폴백 → 캐시), `ProceduralSfx`(5장 표를 코드화), `BgmController`(크로스페이드/동기 전환), `WindAudio`(6.1).
- 모든 클립 null-safe, 같은 ID 최소 재생 간격 40ms, `ForceReset`(씬 전환/재시작) 시 루프 보이스 전체 정리.

### 7.3 변경 제안 (브리프 확정값과의 관계)
1. **히트스톱 수치 신설**: 브리프에는 히트스톱 규정이 없다. 머리 0.10s / 몸통 0.06s / Perfect 명중 +0.02s를 제안한다 (아트 사양서 VFX 타이밍표 및 기획서와 일치 필요). 조정 시 6.3의 필터 구간도 동일하게 바꾼다.
2. **라우드니스 이중 표기 정리**: 믹스 목표 −12 LUFS(전체 플레이 믹스)와 파일 노멀라이즈 −14 LUFS(BGM 단독)를 구분했다. 실기기 측정 후 BGM 버스를 ±2dB 조정한다.
3. **무승부 BGM**: 브리프에 무승부 연출이 없어 `bgm_lobby` 재사용을 제안 (별도 트랙 불필요).
4. **상대 조준음 미재생**: 네트워크로 상대의 드래그 상태를 전송하지 않으므로 상대 draw/tension 소리는 없다. 상대 존재감은 발사음·휘파람으로 표현한다.
5. **위기 BGM 트리거 확장(선택)**: HP 30% 외에 남은 시간 15초 이하에도 `bgm_duel_tense`로 전환하는 안을 기획 검토 요청.
6. **Suno 라이선스/품질 리스크**: 유료 플랜 생성 확인, BPM 비정확·루프 이음새 위험은 2장의 선별·후처리 절차로 대응. 위기 BGM 동기 실패 시 대안은 "하나의 곡 + 스템 레이어 추가"(P2).

---

## 8. 후처리 가이드 (BGM 공통, Suno → OGG)

1. **다운로드**: WAV로 받는다 (MP3 다운로드는 재인코딩 열화). 스템 분리가 가능한 플랜이면 드럼·멜로디 스템도 보관.
2. **BPM·다운비트 측정**: DAW(Reaper/Audacity 등)에서 그리드 맞춤. 2.1의 목표 BPM과 ±4% 이내면 타임스트레치로 정확히 맞춘다 (고품질 알고리즘, 포먼트 보존).
3. **루프 구간 절단**: 2.1의 마디 수만큼 정수 마디 선택, 시작·끝은 **제로 크로스 + 다운비트**. 샘플 길이를 2.1 표의 값에 정확히 맞춘다.
4. **이음새 처리 (tail-over-head)**: 선택 구간 뒤에 **2비트(약 1.25s@96BPM)의 꼬리**를 더 떼어 등전력 크로스페이드로 **구간의 앞부분과 겹쳐** 합친다. 합친 뒤 길이를 다시 정확한 샘플수로 맞춘다. 반복 재생해 이음새 클릭·에너지 단차가 없는지 청취 확인.
5. **EQ**: 40Hz 하이패스, 2.5~3.5kHz −2dB (Q≈0.7), 10kHz 이상 하이셸프 −2dB. 저역 과다 시 80Hz 이하 −3dB.
6. **라우드니스**: 2패스 `loudnorm`으로 **−14 LUFS-I / TP −1.0 dBTP / LRA ≤ 7**.
```
# 1패스 측정
ffmpeg -i bgm_duel_loop.wav -af loudnorm=I=-14:TP=-1.0:LRA=7:print_format=json -f null -
# 2패스 적용 + OGG q5 (측정값을 measured_* 에 입력)
ffmpeg -i bgm_duel_loop.wav -af loudnorm=I=-14:TP=-1.0:LRA=7:measured_I=<값>:measured_TP=<값>:measured_LRA=<값>:measured_thresh=<값>:linear=true -c:a libvorbis -q:a 5 bgm_duel.ogg
```
7. **duel 2종 레벨 매칭**: `bgm_duel_tense`는 `bgm_duel` 대비 +1dB 이내가 되게 조정 (노멀라이즈 후 둘 다 −14 ± 1 LUFS).
8. **검증**: 유니티 인게임에서 3회 이상 연속 루프 청취, 모바일 스피커·이어폰 양쪽 확인, SFX 동시 재생 시 묻힘 확인(특히 `arrow_whistle`, `hit_body`).
9. **보관**: 원본 WAV와 마스터 WAV는 저장소 외부(또는 별도 `Bow/_AudioSource/`)에 보관하고 Resources에는 OGG만 둔다 (빌드 용량 방지). 2.7 로그 갱신.
