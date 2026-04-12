#!/bin/bash
# ============================================================================
# stop-build-check.sh — Stop Hook: 응답 완료 전 최종 빌드 게이트
# ============================================================================
# Claude가 응답을 마치기 전에 전체 빌드가 성공하는지 확인.
# 빌드 실패 시 블로킹 → Claude가 에러 수정 후 재시도.
# ============================================================================

PROJECT_DIR="$CLAUDE_PROJECT_DIR"
cd "$PROJECT_DIR" || exit 0

# .cs 파일이 최근에 수정되었는지 확인 (git diff)
CHANGED_CS=$(git diff --name-only HEAD 2>/dev/null | grep "\.cs$" | head -1)

# .cs 변경이 없으면 빌드 검증 스킵
if [ -z "$CHANGED_CS" ]; then
    exit 0
fi

BUILD_OUTPUT=$(dotnet build "Assembly-CSharp.csproj" 2>&1)
ERROR_COUNT=$(echo "$BUILD_OUTPUT" | grep -cE "error CS")

if [ "$ERROR_COUNT" -gt 0 ]; then
    ERROR_LINES=$(echo "$BUILD_OUTPUT" | grep -E "error CS" | head -10)
    echo "빌드 에러 ${ERROR_COUNT}건이 남아있습니다. 모든 컴파일 에러를 수정한 후 작업을 완료하세요:"
    echo "$ERROR_LINES"
    exit 2  # 블로킹: Claude가 계속 작업하도록 강제
else
    exit 0  # 빌드 성공: 응답 완료 허용
fi
