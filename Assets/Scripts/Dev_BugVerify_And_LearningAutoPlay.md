# 버그 검증 루프 + 학습형 자동플레이 — 설계·사양서

> 에이전트 워크플로우(이해4·설계3·비평·종합)로 도출, programmer가 구현. 세 시스템은 **Editor 어셈블리 한정**이며 `SessionState`/파일 규약으로 느슨히 결합, **상호배제**(학습 ↔ 검증 동시 실행 금지).

## 시스템 3종 요약

| 시스템 | 파일 | 역할 |
|--------|------|------|
| 버그 테스트 계획 | 본 문서 | 범주별 불변식·측정법·우선순위 |
| 런타임 검증 루프 | `Editor/RuntimeInvariantVerifier.cs` | 재생 중 불변식 매 폴 감시 → `VERIFY_FAIL` + 리포트 |
| 학습형 자동플레이 | `Editor/LearningAutoPlay.cs` + `Core/MatchingSystem.cs`(W_* 파라미터화) + `Editor/AutoPlayTester.cs`(learn 배선) | 클리어+잔여이동 fitness로 이동 가중치 (1+1)-ES 자가튜닝 |

## 사용법 (파일 트리거)

```bash
# 학습 주행 (레벨 5~7, 후보당 3패스 평가)
printf "5 7 learn N=3" > .claude/autoplay_start
#   → .claude/autoplay/learn/weights.json (best W + ES 상태)
#   → .claude/autoplay/learn/fitness.jsonl (런별 fitness 로그)

# 런타임 버그 검증 (레벨 1~150 봇 주행 감시)
printf "1 150 3" > .claude/verify_start      # verify_stop 으로 중지
#   → .claude/autoplay/verify/verify_raw.log (VERIFY_FAIL 원본)
#   → .claude/autoplay/verify/verify_report.txt (범주별 집계)

# 정적 데이터 검증(선행): Unity 메뉴 MatchMine/전 레벨 미션 검증(3회)
```

⚠️ 학습(`AutoPlay_Learn`)과 검증(`Verify_Active`)은 **동시 실행 금지** — 학습이 W를 극단 변이시켜 "비정상 플레이"를 유도하므로 버그와 구분 불가. 서로 시작을 거부한다.

---

## 학습 fitness (확정)

```
클리어  : F = CLEAR_BONUS(1000) + max(0,leftMoves)*MOVE_COEF(100)   ← 남은 이동 많을수록↑
미클리어: F = (완료미션/전체)*PARTIAL_SCALE(300) - GAMEOVER_PENALTY(50)
후보 fitness = N패스 평균.  (1+1)-ES: 자식이 부모*(1+0.5%)+ 나을 때만 승격, 1/5 규칙 σ 적응.
```
- **leftMoves 인플레 안전**: 클리어는 게임 규약상 모든 미션 완료가 전제 → 같은 레벨의 부모/자식 비교에서 이동보상 인플레는 상수로 상쇄, leftMoves가 곧 "적은 회전=효율" 신호.
- **미션색 붕괴 방지**: `W_MissionColor ≥ W_Cell` 하한 클램프. 전 가중치 `[0,64]` 클램프.
- 학습 시 **재도전 0**(첫 시도만 표본), 리로드 시 매 폴 W 재적용(static 초기화 복구).

---

## 버그 테스트 계획 (범주 · id · 기준 · 측정 채널 · 우선순위)

> 채널: [폴]=상태 스냅샷, [델타]=폴 간 비교, [로그]=Editor.log grep, [예외]=logMessageReceived, [정적]=LevelMissionVerifier.
> ✅=검증 루프 자동 구현, ▲=로그/정적 기반, ✋=수동 감사(자동 제외).

### A. 상태머신
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| SM-02 | Processing 90초 하드캡 미위반 | procEnter 후 >90s | [폴] | P0 | ✅ PROCESSING_TIMEOUT |
| SM-03 | Loading 진입 시 timeScale=1(±0.01) | Loading에서만(봇 3배속 예외) | [폴] | P0 | ✅ TIMESCALE_LOADING |
| SM-01 | 유효 GameState 전이만 | `Game State:` 로그 시퀀스 인접쌍 | [로그] | P0 | ▲ |

### B. 캐스케이드
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| CS-01 | 캐스케이드 ≤20회 | CurrentCascadeDepth>20 / `hit max iterations` | [폴]+[로그] | P0 | ✅ CASCADE_OVERFLOW / CASCADE_MAXITER |
| CS-05 | FallOnly 잔존 pending 위임발동 | `잔존 pending`→`pending specials only` | [로그] | P0 | ▲ |
| CS-02 | 스톨 무한반복 없음 | `Cascade stalled` 3회+ | [로그] | P1 | ▲ |

### C. 특수블록
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| SB-01 | pending 드론 무발동 소멸 없음 | `Leftover pending block` | [로그] | P0 | ✅ PENDING_LEFTOVER |
| SB-06 | Bomb 경계 IndexOutOfRange 0 | 최외곽 Bomb 후 예외 | [예외] | P0 | ✅ EXCEPTION |
| SB-07 | Rainbow/XBlock 대상색만 파괴 | 발동 전후 타색 순감 없음 | [델타] | P1 | ▲ |

### D. 고블린
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| GB-03 | 고블린 필드밖 갇힘 없음 | GetAliveGoblins 좌표 ∉ IsInGameField | [폴] | P0 | ✅ GOBLIN_OUT_OF_BOUNDS |
| GB-04 | BFS 추격 무한/제자리 없음 | 동일좌표 3턴+ 미이동 | [폴] | P0 | ▲(워치독 교차) |
| GB-06 | 마법사 이펙트 유한수명 | effectParent 단조증가 | [폴] | P1 | ✋ |

### E. 미션·보상
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| MR-01 | 처치→count+1, 도달→complete | RunVerification CHECK2 | [정적] | P0 | ✅ 메뉴 |
| MR-02 | 최종미션(활성 all+대기0) 완료 시 StageClear | 유예5s 내 미전이 | [델타] | P0 | ✅ FINAL_MISSION_STUCK |
| MR-06 | 활성 미션 ≤ 한도 | Length>CurrentActiveLimit | [폴] | P1 | ✅ MISSION_OVERFLOW |
| MR-04 | 이동보상 미션당 1~5 | CHECK3 | [정적] | P1 | ✅ 메뉴 |

### F. 게이지 MP·RW
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| MP-01 | CurrentMP ∈ [0,MaxMP] | 범위 | [폴] | P0 | ✅ MP_CLAMP |
| MP-03 | 부족 시 발동 차단 | `MP 부족` 후 발동로그 없음 | [로그] | P0 | ▲ |
| GC-01 | 게이지 보존(이중충전 없음) | 턴단위 ΔMP=충전-소모 | [로그] | P1 | ▲ |

### G. 데드락
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| DL-01 | 재배치 후 매칭가능 회복 | `성공=False` 반복 | [로그] | P0 | ✅ RESHUFFLE_FAIL |
| DL-04 | 무한 재배치 루프 없음 | `재배치 완료` 5회+ 연속 | [로그] | P1 | ▲ |

### H. 봇 진행
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| BT-01 | 워치독 40초 빈도 | `WARN,BOT_WATCHDOG` 집계 | [로그] | P0 | ▲(봇 자체) + STUCK_DETECTED ✅ |
| BT-04 | 회전이 실제 매칭 생성 | `matchFound=false` 연속다발 | [로그] | P1 | ▲ |
| A8 | 전역 스톨 없음 | Playing+입력활성 90s 무진행 | [폴] | P0 | ✅ RUNTIME_STALL |

### I. 렌더·UI
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| UI-08 | 예외 0건 | logMessageReceived Exception/Error | [예외] | P0 | ✅ EXCEPTION |
| UI-M | 색맹구분·어포던스·3색·롤러 | 수동 감사 세트(자동 제외) | — | P2 | ✋ |

### J. 밸런스·런리셋
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| BL-01 | 런별 스킬 리셋 | N→N+1 시작 시 초기화 | [로그] | P0 | ▲ |
| BL-04 | 클리어율/평균 잔여 | results.log 집계 | [로그] | P2 | ▲ |

### K. 신규 불변식(비평 반영)
| id | 기준 | 측정 | 채널 | 우선 | 구현 |
|----|------|------|------|------|------|
| SIM-01 | 시뮬레이션 보드 누수 없음 | FindBestMissionCluster 전후 보드 불변 | [델타] | P0 | ▲(SetBlockDataSilent 복원) |
| RLD-01 | 리로드 직후 판정 유예 | afterAssemblyReload 후 1.5s 스킵 | — | P0 | ✅ RELOAD_GRACE |

---

## 검증 루프 자동 구현 카테고리 (RuntimeInvariantVerifier)

`PROCESSING_TIMEOUT · TIMESCALE_LOADING · CASCADE_OVERFLOW · MP_CLAMP · GOBLIN_OUT_OF_BOUNDS · MISSION_OVERFLOW · FINAL_MISSION_STUCK(디바운스5s) · RUNTIME_STALL(90s) · EXCEPTION · PENDING_LEFTOVER · CASCADE_MAXITER · STUCK_DETECTED · RESHUFFLE_FAIL`

오탐 방지: Loading/Paused/전환 스킵, 리로드 1.5s 유예, 카테고리별 5s 쿨다운, 스톨/최종미션 디바운스, 봇 3배속(3.0) 정상 인정.
