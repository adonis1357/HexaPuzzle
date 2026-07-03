# 스킬 시스템 전환 — 영구 해금 → 런(게임)별 로그라이크 리워드

> 2026-06-17 사용자 지시로 확정. **스킬 영구 해금(스킬트리 메타 progression)을 폐기**하고,
> 매 게임(런=스테이지 1회 도전) 중 **리워드 선택**으로 스킬을 업그레이드하며,
> **레벨을 처음부터 다시 하면 모든 스킬이 초기화**되어 다시 배우는 로그라이크 모델로 전환한다.

## 새 모델 (확정)
- **해금 개념 폐기**: SP/골드로 스킬을 영구 해금하는 메타 progression은 더 이상 필요 없음.
- **런별 스킬**: 스킬은 한 게임(스테이지 1회 도전) 동안만 유지된다. 영구 저장하지 않는다.
- **획득 경로 = 리워드 선택만**: 인게임에서 `SkillUpgradeOfferSystem`(주황 블록 10개 제거당 3카드 — 고급+1/희귀+2/전설+3)으로만 스킬을 얻고 업그레이드한다.
- **재시작 = 초기화**: 레벨을 처음부터 다시 하면(또는 새 스테이지 시작 시) 모든 스킬이 base로 리셋되어 다시 배워야 한다.

## 기존 시스템 대비 (무엇이 바뀌나)
| 항목 | 기존(영구) | 신규(런별) |
|---|---|---|
| 스킬 획득 | 스킬트리 UI에서 SP/골드로 영구 해금 + 리워드 선택 | **리워드 선택만** |
| 영속성 | `unlockedSkills` → PlayerPrefs 영구 저장 | **저장 안 함 / 스테이지 시작 시 초기화** |
| 레벨 게이팅 | requiredLevel(Lv.21~80), 카테고리 순차 해금 | (검토: 런 내 등장 제한은 유지 가능, 메타 레벨 게이팅은 폐기 방향) |
| 재시작 | 영구 유지 | **모든 스킬 초기화 → 재학습** |

## 구현 (이번 적용)
- `SkillTreeManager.ResetRunSkills()` 신규 — `unlockedSkills`/`freeUnlockedSkills` 비우고 `OnSkillTreeReset` 발동(아이콘 바 갱신). **SP/골드 환급·쿨다운 없음**(영구 경제 폐기), 영속 상태도 비워 앱 재시작 후에도 런 시작은 항상 빈 상태.
- `SkillUpgradeOfferSystem.ResetForNewStage()`(GameManager:3593, **매 스테이지 시작/재시작 시 호출**)에서 `ResetRunSkills()`를 호출 → 스테이지 시작마다 스킬이 base로 초기화. 이게 "매 게임마다 처음부터 다시 배움 / 재시작 시 초기화"의 핵심.
- 리워드 선택 자체(`OnChoiceClicked → ForceUnlockSkill`)는 그대로 동작 — 다만 스테이지 시작 클리어로 인해 결과적으로 런 단위로만 유지됨.

## 확정 결정 (2026-06-18 사용자 확정 — 적용)
1. **영구 스킬트리 + SP 경제 완전 제거**: SkillTreeUI 화면, 하단 내비 '스킬' 탭, 로비 SP 표시, 최초클리어 SP 지급, 소급 SP(EnsureRetroactiveSP) 모두 제거.
2. **레벨 게이팅 제거 → 희귀도 확률만**: 리워드 후보/희귀도에서 `GetHighestReachedLevel`·`requiredLevel`·`IsCategoryAvailable` 검사 제거(3곳: MaxUnlockableGain / GetOrangeRewardCandidates / ForceUnlockSkill). 희귀도 확률(고급60/희귀30/전설10) + 체인max 클램프만 유지.
3. **리워드 스테이지 21+ 유지 + 21+ 플레이타임↑**: `MIN_STAGE_FOR_REWARD=21` 유지. 21+ 스테이지는 대기 미션을 더 만들어(MissionBalance 목표 미션 수↑) 플레이타임 연장 → 리워드를 전략적으로 활용.
4. **런 = 레벨 진행 중 유지, 로비 이탈 시 초기화**: 스테이지 시작(StartGameCoroutine) 리셋 제거. 로비 이탈 3경로(ExitToLobby/ReturnToLobby/게임오버 ForceResetAllGameSystems)에서만 ResetRunSkills 호출. 스테이지 전환·재시도(로비 미경유) 중엔 스킬 유지.

검증 체크리스트: Dev_SkillReward_Checklist.md (10에이전트 토론 + 적대검증).
