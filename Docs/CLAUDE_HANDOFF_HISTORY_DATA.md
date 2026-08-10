# CLAUDE HANDOFF — HISTORY DATA V1

## 작업 분리

Claude는 **실제 회사 역사 조사·정규화·출처·데이터 검증**만 맡는다. Codex는 **Unity 런타임, 회사/계약/경쟁/M&A 시뮬레이션, `simul` 시장 이식, 씬과 자동 테스트**를 맡는다.

동시 작업 중에는 같은 파일을 수정하지 않는다.

### Claude 전용 경로

- `Assets/FamilyCompany/Content/History/**`
- `HistoryTools/**`
- `Docs/CLAUDE_HISTORY_PROGRESS.md`

### Claude가 수정하지 않는 경로

- `Assets/FamilyCompany/Simulation/**`
- `Assets/FamilyCompany/Save/**`
- `Assets/FamilyCompany/Infrastructure.Unity/**`
- `Assets/FamilyCompany/Presentation.Unity/**`
- `Assets/FamilyCompany/Editor/**`
- `Assets/FamilyCompany/Scenes/**`
- `ProjectSettings/**`, `Packages/**`
- `C:/Users/godho/Documents/Codex/simul/**`
- `Docs/PROJECT_STATE.md`, `Docs/DECISIONS.md`를 포함한 Claude 전용이 아닌 문서

Claude는 진행 내용을 자기 전용 파일에 남기고, Codex가 검토 후 정본 문서로 합친다.

## Claude에 그대로 붙여 넣을 지시문

```text
프로젝트 경로는 C:\Users\godho\Documents\Codex\family_company_unity 이다.

너는 이 Unity 가족회사 게임에서 실제 회사 역사 데이터 작업만 담당한다. 먼저 AGENTS.md, CLAUDE.md, Docs/PROJECT_STATE.md, Docs/CANON.md, Docs/DECISIONS.md, Docs/ARCHITECTURE.md, Docs/ULTIMATE_VISION.md, Docs/REAL_COMPANY_ALT_HISTORY.md, Docs/CLAUDE_HANDOFF_HISTORY_DATA.md를 UTF-8로 전부 읽어라.

이번 작업은 History V1 세로 조각만 한다. 2000-01-01~2003-12-31을 상세 범위로 하고, 2004~2026은 거시·산업 핵심 사건의 얇은 앵커 목록만 만든다. 처음부터 수백 회사를 채우지 마라.

대상 회사 12개:
Apple, Microsoft, Google, Amazon.com, Yahoo!, AOL, Samsung Electronics, LG Electronics, SK Telecom, NTT DoCoMo, Sony, Nokia.

정확한 당시 법인명, 이름 변경일, 상장/합병/분사/종료 상태는 기업 공식 연혁·IR, 거래소·규제기관 서류 등 1차 출처로 검증하라. 기억만으로 날짜를 확정하지 마라. 실제 회사명은 혼동 방지를 위한 debugDisplayName으로 유지하되, 영구 참조는 안정적인 companyId를 사용하라.

산출물은 Claude 전용 경로에만 만든다:
1. Assets/FamilyCompany/Content/History/schema_version_1.json
2. Assets/FamilyCompany/Content/History/company_registry_2000_2003.json
3. Assets/FamilyCompany/Content/History/company_events_2000_2003.json
4. Assets/FamilyCompany/Content/History/macro_timeline_2000_2026_anchor.json
5. Assets/FamilyCompany/Content/History/sources.json
6. Assets/FamilyCompany/Content/History/README.md
7. HistoryTools/validate_history_data.ps1
8. Docs/CLAUDE_HISTORY_PROGRESS.md

JSON 제약:
- UTF-8, 주석 없음, ISO YYYY-MM-DD 날짜
- Unity DTO 친화적인 최상위 객체 + 명시적 배열 사용
- 키를 동적으로 쓰는 object dictionary를 사용하지 말 것
- 모든 companyId/eventId/sourceId는 중복 금지
- 모든 참조 ID의 존재 여부를 validator가 검사
- 종료일이 시작일보다 빠른지, 날짜 범위가 겹치는 이름 이력이 있는지 검사
- 모든 역사 사실 event에 최소 하나의 sourceId 연결
- 출처 충돌 또는 불확실성은 needsReview=true로 남기고 추측 금지

사건은 baselineDate만 넣고 끝내지 말고 prerequisites와 failurePolicy(cancel, delay, substitute, transfer)를 데이터로 표현할 수 있게 설계하라. 플레이어가 사전에 회사를 인수하거나 기술을 선점하면 원래 사건이 강제 발생하지 않아야 한다.

2000~2003 상세 데이터는 회사당 모든 사소한 일을 수집하지 말고 게임에 영향을 주는 창업/이름 변경/상장/대형 제품군 변화/중요 M&A/파산·퇴출/핵심 산업 전환만 선별하라. 2004~2026 앵커는 닷컴 이후 검색·광대역·모바일 인터넷·스마트폰·앱 생태계·클라우드·주요 금융 및 공급망 충격처럼 나중에 세부화할 뼈대만 만든다.

런타임 C# 코드, Unity 씬, Editor, ProjectSettings, Packages, simul 저장소, Docs/PROJECT_STATE.md와 Docs/DECISIONS.md는 수정하지 마라. 외부 패키지도 추가하지 마라. 다른 작업자의 변경을 되돌리지 마라.

마지막에 validate_history_data.ps1을 실행하고 결과, 실제 생성한 회사/사건/출처 수, needsReview 목록, 다음 확장 제안을 Docs/CLAUDE_HISTORY_PROGRESS.md에 기록하라. 가능하면 네 작업만 별도 커밋하되 기존 미커밋 변경이 있으면 커밋하지 말고 보고만 해라.
```

## 완료 판정

- 12개 회사의 안정 ID와 날짜별 이름이 서로 참조 가능하다.
- 2000~2003 주요 사건에 출처와 분기 정책이 있다.
- 2004~2026 앵커는 세부 역사를 대신하지 않고 확장 순서만 제공한다.
- validator가 오류 0으로 끝나며 불확실성은 숨기지 않고 보고한다.
- Unity 런타임과 기존 `simul`은 한 줄도 수정하지 않는다.
