#!/bin/bash
# ============================================================================
# dev-agent-done.sh — SubagentStop Hook: 개발 에이전트 완료 시 빌드 검증
# ============================================================================

PROJECT_DIR="$CLAUDE_PROJECT_DIR"
cd "$PROJECT_DIR" || exit 0

BUILD_OUTPUT=$(dotnet build "Assembly-CSharp.csproj" 2>&1)
ERROR_COUNT=$(echo "$BUILD_OUTPUT" | grep -cE "error CS")

if [ "$ERROR_COUNT" -gt 0 ]; then
    ERROR_LINES=$(echo "$BUILD_OUTPUT" | grep -E "error CS" | head -10)
    echo "개발 에이전트 작업 완료 전 빌드 에러 ${ERROR_COUNT}건:"
    echo "$ERROR_LINES"
    exit 2
else
    exit 0
fi
