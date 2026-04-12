#!/bin/bash
# ============================================================================
# post-cs-edit.sh — PostToolUse Hook: .cs 파일 수정 후 자동 컴파일 검증
# ============================================================================
# .cs 파일이 수정될 때마다 dotnet build를 실행하여 컴파일 에러를 즉시 감지.
# 에러 발생 시 additionalContext로 Claude에게 피드백 → 자동 수정 유도.
# ============================================================================

# stdin에서 JSON 읽기
INPUT=$(cat)

# Python으로 file_path 추출 (jq 대체)
FILE_PATH=$(echo "$INPUT" | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    ti = data.get('tool_input', {})
    print(ti.get('file_path', ti.get('file', '')))
except:
    print('')
" 2>/dev/null)

# .cs 파일이 아니면 통과
if [[ "$FILE_PATH" != *.cs ]]; then
    exit 0
fi

# 프로젝트 디렉토리로 이동하여 빌드
PROJECT_DIR="$CLAUDE_PROJECT_DIR"
cd "$PROJECT_DIR" || exit 0

BUILD_OUTPUT=$(dotnet build "Assembly-CSharp.csproj" 2>&1)
BUILD_EXIT=$?

# 에러 카운트 (한국어 "오류" 또는 영문 "error CS")
ERROR_LINES=$(echo "$BUILD_OUTPUT" | grep -E "error CS|오류" | head -5)
ERROR_COUNT=$(echo "$BUILD_OUTPUT" | grep -cE "error CS")

if [ "$ERROR_COUNT" -gt 0 ]; then
    # 컴파일 에러 → additionalContext로 에러 정보 전달
    # 특수문자 이스케이프
    ESCAPED_ERRORS=$(echo "$ERROR_LINES" | python3 -c "
import sys, json
lines = sys.stdin.read().strip()
print(json.dumps(lines))
" 2>/dev/null)

    echo "{\"additionalContext\": \"[컴파일 에러 ${ERROR_COUNT}건] 즉시 수정하세요:\\n${ERROR_LINES}\"}"
    exit 0
else
    echo "{\"additionalContext\": \"빌드 성공 ✓\"}"
    exit 0
fi
