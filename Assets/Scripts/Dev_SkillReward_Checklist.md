# 런별 스킬 전환 — 검증 체크리스트 (10에이전트 토론 + 적대검증)

> 2026-06-18 10에이전트 패널 토론 → 종합 → 적대적 비평 → 최종화. 범주당 3~5개. P0=필수.
> 적대검증이 잡은 핵심: 컴파일 게이트 누락, 경로 정확성(SkillUpgradeOfferSystem/SkillTreeManager=Managers/, MissionBalance=StageManager.cs:743 nested), 드릴관통 런리셋 누수, 대기미션 모바일 가독성, 모달 중 로비이탈, EnsureRetroactiveSP는 Awake 호출 제거(본문 무력화 아님), 통계 측정 불가→결정론 차분.

## G0 — 선행 게이트
- **G0-1** 4개 결정 확정 — ✅ 사용자 2026-06-18 확정(#1 제거 / #2 희귀도확률 / #3 21+ & 플레이타임↑ / #4 로비이탈 리셋).
- **G0-2 (P0)** `dotnet build "Assembly-CSharp.csproj"` 0 에러 — ✅ 통과. 신규 미사용 심볼 경고 식별.
- **G0-3 (P0)** 검증 grep 경로 정확: SkillUpgradeOfferSystem/SkillTreeManager = Assets/Scripts/Managers/, MissionBalance = StageManager.cs:743 nested static.

## R1 — 영구 스킬트리/SP 제거 (변경1)
- **R1-1 (P0)** 로비 스킬 진입점 0: 하단 내비 '스킬' 탭/스킬 육각 버튼/red dot 노출 0개. (측정: nav labels에 '스킬' 없음 grep + 로비 캡처)
- **R1-2 (P0)** skillTreeUI 미생성·Show() 미호출, 어떤 경로로도 스킬트리 화면 안 열림, NRE 0. (측정: AddComponent<SkillTreeUI> grep 0 + 전이 캡처)
- **R1-3 (P0)** SP 수치/지급 0: EnsureRetroactiveSP Awake 호출 제거 + CalculateAndGrantClearRewards SP 미지급 + SkillTreeUI SP 위젯 미생성. PlayerPrefs SP_KEY 게임 전후 미증가.
- **R1-4 (P0)** UI/이벤트 제거 후 런타임 NRE/MissingReference 0 (로비 진입/이탈·클리어·게임오버 각 1회).

## R2 — 희귀도 확률·레벨게이팅 제거 (변경2)
- **R2-1 (P0)** 리워드 후보에서 계정레벨/requiredLevel/IsCategoryAvailable 게이팅 완전 제거(3곳). 계정레벨 무관 동일 후보 풀. (측정: 3메서드 grep 0)
- **R2-2 (P1)** 희귀도 분포 고급60/희귀30/전설10 유지(RollRarity 미변경). remain=2 시 66.7/33.3 재정규화.
- **R2-3 (P1)** 카드 표기 +N == 실제 ForceUnlockSkill 해금량(조용한 유실 0). 체인max 클램프만 잔존.
- **R2-4 (P1)** 체인 끝단(remain≤1)엔 전설/희귀 미표기(체인max 클램프 유지).

## R3 — 21+ 플레이타임 (변경3, MissionBalance)
- **R3-1 (P1)** 21+ 목표 미션 수 증가로 기대 플레이타임↑. (측정: GetTargetMissionCount(20)==3 < (21)==6, 결정론 차분으로 Stage21 기대 소비무브 > Stage20)
- **R3-2 (P2)** 21+ 보충 첫 슬롯 Orange 배정은 조건부(baseMissions에 Orange 없을 때만). 미션 배정 ≠ 모달 등장 보장(주황 10개 '파괴' 누적이 게이지 충전).
- **R3-3 (P2)** 플레이 연장이 '막힘' 아닌 '연장': 미션 완료 이동보상(+6)이 추가 미션 비용 상쇄 → 게임오버율 급증 없음. (결정론 차분)
- **R3-4 (P2)** 대기 미션 승격 가시화 + 결정성(동일 스테이지 재입장 시 동일 미션 세트).

## R4 — 런 리셋 트리거 (변경4)
- **R4-1 (P0)** 리셋 = 로비 이탈 시에만. 스테이지 시작(StartGameCoroutine/ResetForNewStage)에서 ResetRunSkills 제거. 스테이지 전환·재시도(로비 미경유) 시 스킬 유지.
- **R4-2 (P0)** 로비 이탈 3경로(ExitToLobby/ReturnToLobby/게임오버 ForceResetAllGameSystems) 모두 ResetRunSkills(환급 없음)로 통일. 재입장 시 학습 바 0 + 골드 환급 증가 0.
- **R4-3 (P1)** 런 리셋 시 진행 중 특수효과(드릴 관통/투사체 in-flight) 안전 종료 — 누수/NRE 0, 다음 런 base 복귀.
- **R4-4 (P2)** 코드 주석/설계문서가 확정 동작과 일치(ResetForNewStage 주석·Dev_SkillReward_RunBased.md).

## RC — 공통 회귀·런타임
- **RC-1 (P1)** 콜드 스타트 후 런 시작 빈 스킬(LoadSkillData가 잔존 부활 안 함, SP_KEY가 스킬 해금 경로 미참조).
- **RC-2 (P1)** 모달 처리 중 timeScale 항상 1 복원(영구정지 0). 모달 표시 중 로비 이탈 시 choiceModal 정리 + timeScale 복원.
- **RC-3 (P2)** 게이트(21) 미만 스테이지에서 게이지/모달 미노출, orange 카운트 미증가, NRE 0.
- **RC-4 (P2)** 21+ 모바일(~380px) HUD에서 활성4+대기 다수 미션 가독성(잘림/오버랩 0, 다크글래스 토큰).

## 본 세션 검증 결과(코드·정적 가능 항목)
- G0-2 ✅(0에러) / R1-1·R1-2·R1-3(grep) / R2-1(grep) / R3-1(GetTargetMissionCount 21=6) / R4-1·R4-2(grep) — 캡처/런타임/플레이는 갱신 후 확인.
- 플레이·통계·whitebox(R2-2/2-3/2-4, R3-3, R4-3) 항목은 인게임 진입·계측 필요 → 후속.
