# 「활」 Unity 직접 조작 연결 가이드 (Unity MCP + Claude Code)

> 목적: Claude가 사용자 PC의 Unity 에디터를 **직접** 조작(씬 열기, Play, 콘솔 오류 읽기, 오브젝트 생성/수정)할 수 있게 연결한다.
> 클라우드 세션(claude.ai/code 웹)은 사용자 PC에 닿을 수 없으므로, **PC에서 실행되는 Claude** 가 필요하다.

## 0. 준비물
| 항목 | 비고 |
|------|------|
| Unity 2022.3.62f2 | Unity Hub에서 설치 |
| Node.js 18 이상 | https://nodejs.org (LTS). `mcp-unity` 서버가 node로 돈다 |
| Claude Code (CLI) 또는 Claude Desktop 앱 | `npm install -g @anthropic-ai/claude-code` 또는 https://claude.ai/download |
| 이 저장소 브랜치 `claude/confident-babbage-6lk3zc` | `git clone -b claude/confident-babbage-6lk3zc https://github.com/adonis1357/HexaPuzzle.git` |

## 1. Unity 쪽: MCP 서버 켜기 (Bow 프로젝트에 이미 패키지가 들어 있음)
`Bow/Packages/manifest.json` 에 다음 두 패키지가 포함되어 있어 Unity Hub에서 `Bow` 폴더를 열면 자동 설치된다.
- `com.gamelovers.mcp-unity` (CoderGamester/mcp-unity) — **기본 사용**. HexaPuzzle에서 이미 쓰던 것과 동일, 포트 8090 자동 시작(`ProjectSettings/McpUnitySettings.json`).
- `com.coplaydev.unity-mcp` (CoplayDev MCP for Unity) — 예비. 필요 없으면 manifest에서 지워도 된다.

순서:
1. Unity Hub → Add project from disk → `.../HexaPuzzle/Bow` → 열기 (최초 패키지 다운로드 1~3분, 인터넷 필요).
2. 메뉴 **Tools → MCP Unity → Server Window** 를 연다.
3. 창에서 **Start Server** (AutoStartServer가 켜져 있으면 이미 "Running"). 포트 8090 확인.
4. 같은 창의 **Configure Claude Code** (또는 "Configure Claude Desktop") 버튼을 누르면 사용자 PC의 Claude 설정 파일에 서버 항목이 자동으로 기록된다.
   - 버튼이 없거나 실패하면 아래 2-B의 수동 설정을 쓴다.

## 2. Claude 쪽: 서버 등록

### 2-A. Claude Code CLI (권장)
저장소 루트(`HexaPuzzle`) 폴더에서 터미널을 열고:
```
claude mcp add mcp-unity -s project -- node "<Bow 프로젝트 절대경로>\Library\PackageCache\com.gamelovers.mcp-unity@<해시>\Server~\build\index.js"
```
`<해시>` 부분은 `Bow\Library\PackageCache` 폴더에서 실제 폴더명을 확인해 넣는다. (Server Window의 Configure 버튼이 이 경로를 자동으로 채워주므로 보통 수동 입력은 필요 없다.)
등록 확인:
```
claude mcp list
```
`mcp-unity` 가 보이면 완료. 이후 `claude` 를 실행해 "활 프로젝트 BowDuel 씬 열고 Play 눌러서 콘솔 오류 알려줘"처럼 지시하면 된다.

### 2-B. 수동 설정 (Claude Desktop 앱)
`%APPDATA%\Claude\claude_desktop_config.json` 에 추가:
```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": ["C:\\경로\\HexaPuzzle\\Bow\\Library\\PackageCache\\com.gamelovers.mcp-unity@<해시>\\Server~\\build\\index.js"],
      "env": { "UNITY_PORT": "8090" }
    }
  }
}
```
저장 후 Claude Desktop을 재시작하면 도구 목록에 Unity 도구(씬/오브젝트/콘솔/메뉴 실행 등)가 뜬다.

## 3. 이 클라우드 세션을 PC로 이어받기 (Remote Control)
클라우드 세션(지금 이 대화)이 PC의 Unity를 직접 다루게 하려면, PC 쪽에서 세션을 열어 연결한다.
1. PC에 Claude Code CLI 설치 후 로그인: `claude` 실행 → 브라우저 로그인.
2. 저장소 루트 폴더에서:
   ```
   claude remote-control
   ```
3. 그러면 Claude Code 앱(claude.ai/code)의 세션 목록에 PC 세션이 나타난다. 그 세션에서 작업하면 Claude가 **사용자 PC에서** git 체크아웃, Unity 실행(`"C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe" -projectPath ...\Bow`), 콘솔 로그 읽기, MCP 조작을 직접 수행한다.
   - 또는 Claude Desktop 앱 → Code 탭 → 저장소 폴더 열기도 같은 효과.

## 4. 연결 확인 체크리스트
- [ ] Unity: Tools → MCP Unity → Server Window 가 "Running (port 8090)"
- [ ] `claude mcp list` 에 `mcp-unity` 표시
- [ ] Claude에게 "현재 열린 씬 이름 알려줘" → `BowDuel` 응답
- [ ] "Play 모드 진입하고 콘솔 에러 있으면 보여줘" → 오류 목록 또는 "없음"

## 5. 문제 해결
| 증상 | 조치 |
|------|------|
| Server Window 메뉴가 없음 | Package Manager에서 mcp-unity 설치 확인. 인터넷/깃 접근 차단이면 manifest의 git URL 다운로드가 실패한 것 → Git 설치 후 Unity 재시작 |
| `claude mcp list`에 없음 | 2-A 명령 재실행. `-s project` 는 저장소의 `.mcp.json` 에 기록되므로 해당 폴더에서 `claude` 를 실행해야 보임 |
| 포트 8090 충돌 | `Bow/ProjectSettings/McpUnitySettings.json` 의 Port 변경 후 Claude 설정의 `UNITY_PORT` 도 동일하게 변경 |
| node 없음 | Node.js LTS 설치 후 터미널 재시작 |
| Play 중 Claude 도구가 응답 없음 | Unity가 컴파일 중이면 대기. Domain Reload 후 서버가 재시작되므로 몇 초 뒤 재시도 |
