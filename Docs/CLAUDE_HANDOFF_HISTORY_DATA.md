# CLAUDE HANDOFF — KOREA HISTORY DATA V1

## 집 PC 재개와 브랜치

2026-08-10 통합 작업은 `agent/contract-lifecycle-v0-3` 브랜치에 있다. 집 PC의 clean clone에서 `git fetch origin`, `git switch agent/contract-lifecycle-v0-3`, `git pull --ff-only` 순서로 받은 뒤 이 문서의 전용 경계를 다시 확인한다. dirty worktree에서는 pull·브랜치 전환·merge·rebase를 하지 않는다. Unity는 `6000.3.21f1`로만 열며, `Library`, `Temp`, `Logs`, `work` 산출물은 Git에 넣지 않는다.

## 작업 분리

Claude는 **국내 실제 회사 역사 조사·정규화·출처·데이터 검증**만 맡는다. Codex는 **Unity 런타임, 소형 하청 계약, 경쟁/M&A 시뮬레이션, `simul` 시장 이식, 씬과 자동 테스트**를 맡는다.

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

## 절대 이름 규칙

- 현재 게임과 데이터에는 실제 회사의 해당 날짜 실제 법인명·실제 통용명을 그대로 쓴다.
- 실제 이름을 `debugDisplayName`에만 넣고 게임 UI를 가명으로 바꾸지 않는다.
- `fictionalAlias`나 가명 매핑을 만들지 않는다.
- 서비스·브랜드를 법인으로 착각하지 말고 당시 실제 운영 법인을 연결한다.
- 내부 참조는 불변 `companyId`, 화면 표시는 실제 `displayNameKo`를 사용한다.

## Claude에 그대로 붙여 넣을 지시문

```text
프로젝트 경로는 C:\Users\godho\Documents\Codex\family_company_unity 이다.
읽기 전용 참고 저장소는 C:\Users\godho\Documents\Codex\simul 이다.

너는 이 Unity 가족회사 게임에서 국내 실제 회사 역사 데이터 작업만 담당한다. 먼저 AGENTS.md, CLAUDE.md, Docs/PROJECT_STATE.md, Docs/CANON.md, Docs/DECISIONS.md, Docs/ARCHITECTURE.md, Docs/ULTIMATE_VISION.md, Docs/REAL_COMPANY_ALT_HISTORY.md, Docs/SIMUL_MARKET_PORT.md, Docs/CONTRACTS_V0_3.md, Docs/CLAUDE_HANDOFF_HISTORY_DATA.md를 UTF-8로 전부 읽어라.

절대 규칙: 현재 게임에는 가명이 아니라 실제 회사의 해당 날짜 실제 한글 법인명, 실제 영문 법인명, 실제 통용명을 그대로 표시한다. actual name을 debug 전용으로 숨기지 말고 displayNameKo에 넣어라. fictionalAlias, 가상 회사명, 가명 매핑을 만들지 마라. 브랜드나 서비스가 법인이 아니면 당시 실제 운영 법인을 찾아 관계를 기록하라. Apple, Microsoft, Google 등 해외 회사 상세 데이터는 이번 작업에서 만들지 마라.

조사 전에 C:\Users\godho\Documents\Codex\simul 을 읽기 전용으로 검색하라. 특히 다음을 확인한다:
- DATA_SOURCES.md
- Docs/SIMULATION_CORE.md
- Docs/COMPANY_SYSTEM.md
- flutter_app/lib/game/market_era_events.dart
- flutter_app/lib/game/market_corpus_events.dart
- flutter_app/lib/game/market_corpus_calendar.dart
- flutter_app/lib/game/market_corpus_daily_samples.dart
- flutter_app/lib/game/market_arc_scenarios.dart
- flutter_app/lib/game/corporate_disclosure.dart
- flutter_app/lib/game/game_engine_corporate_actions.dart

simul에는 사용자가 제공했던 2000~2026 한국 주식시장 사건·주가 타임라인의 파생 데이터, 시대 사건 문법과 출처 URL이 있을 수 있다. 이를 먼저 재사용 후보로 삼되, 파생 데이터를 실제 회사의 확정 사실로 그대로 복사하지 마라. 정확한 법인명, 날짜, 상장, 합병, 분사, 파산과 종료는 기업 공식 연혁·IR, DART, KIND/KRX, 규제기관 등 원 1차 출처로 교차 검증하라. simul은 한 줄도 수정하지 마라.

이번 작업은 Korea History V1이다.
1. 2000~2026 국내 실제 회사 등록부를 최소 60개 만든다.
2. 2000-01-01~2003-12-31에 활동한 국내 회사 최소 25개는 주요 사건과 규모를 상세화한다.
3. 2004~2026에 새로 등장하거나 크게 전환한 국내 회사는 진입·퇴출 앵커를 만든다.
4. 대기업만 채우지 말고 중소·중견·코스닥·비상장·부실·퇴출 회사를 충분히 포함한다.
5. 공개 근거가 있는 소형·부실·청산 기업과 자산 매각 사례를 찾아 낮은 금액 인수 후보를 만든다. 실제 대기업 가치를 게임 편의로 낮추지 마라.

우선 조사 풀은 아래와 같다. 아래 표기의 현재 통용명을 최종 법인명으로 무조건 복사하지 말고, 날짜별 실제 법인명과 법인 연속성을 조사하라. 회사가 아니라 브랜드/서비스이면 실제 운영 법인으로 바로잡아라.

통신·전산·전자·기기:
삼성전자, LG전자, SK텔레콤, 한국통신/KT, 데이콤, 하나로통신, 두루넷, 온세통신, 삼성SDS, LG-EDS시스템, SK C&C, 팬택, 텔슨전자, 세원텔레콤, 브이케이, 휴맥스, 레인콤, 코원시스템, 아이디스.

인터넷·포털·상거래:
다음커뮤니케이션, 네이버컴/NHN, 네오위즈, 드림위즈, 프리챌, 인터파크, 옥션, 예스24, 다나와, 인크루트, 잡코리아, 나우콤.

게임:
엔씨소프트, 넥슨, 한빛소프트, 웹젠, 그라비티, 위메이드, 소프트맥스, 엠게임, 액토즈소프트, 컴투스, 게임빌, 네오플, CCR.

소프트웨어·보안·결제:
안철수연구소, 한글과컴퓨터, 이스트소프트, 다우기술, 소프트포럼, 한국정보인증, 이니시스, 한국사이버결제, 다날, 모빌리언스, 더존디지털웨어, 티맥스소프트, 핸디소프트.

전자부품·장비:
심텍, 주성엔지니어링, 서울반도체, 유일전자.

2004~2026 후속 조사 풀:
아이위랩/카카오, 쿠팡, 비바리퍼블리카, 우아한형제들, 야놀자, 쏘카, 컬리, 티몬, 위메프, 블루홀/크래프톤, 펄어비스, 데브시스터즈, 카카오게임즈. 날짜별 실제 법인명과 전신·후신 관계를 기록한다.

산출물은 Claude 전용 경로에만 만든다:
1. Assets/FamilyCompany/Content/History/schema_version_1.json
2. Assets/FamilyCompany/Content/History/company_registry_korea_2000_2026.json
3. Assets/FamilyCompany/Content/History/company_events_korea_2000_2003.json
4. Assets/FamilyCompany/Content/History/company_entry_exit_korea_2004_2026_anchor.json
5. Assets/FamilyCompany/Content/History/macro_timeline_korea_2000_2026_anchor.json
6. Assets/FamilyCompany/Content/History/acquisition_evidence_korea.json
7. Assets/FamilyCompany/Content/History/sources.json
8. Assets/FamilyCompany/Content/History/README.md
9. HistoryTools/validate_history_data.ps1
10. Docs/CLAUDE_HISTORY_PROGRESS.md

회사 이름 데이터는 최소 다음을 가진다:
- companyId: 이름과 무관한 영구 ID
- nameHistory[]: legalNameKo, legalNameEn, displayNameKo, fromDate, toDate, sourceIds[]
- actualBrands[]: brandNameKo, operatingCompanyId, fromDate, toDate, sourceIds[]
- predecessorIds[], successorIds[]
- listingHistory[], ownershipHistory[]

인수 규모 근거는 확인 가능한 날짜의 자산, 부채, 매출, 시가총액, 거래정지·관리·회생·청산 상태와 sourceIds를 저장한다. 게임 인수금액을 역사 사실인 것처럼 만들어 넣지 마라. 신뢰할 근거가 없으면 needsReview=true로 둔다. 법인 전체가 4인 회사에 너무 크면 사업부, 소스코드, 특허, 도메인, 장비, 고객계약, 핵심 인력의 역사적 자산 매각 가능성을 별도 후보로 기록한다.

JSON 제약:
- UTF-8, 주석 없음, ISO YYYY-MM-DD 날짜
- Unity DTO 친화적인 최상위 객체 + 명시적 배열 사용
- 키를 동적으로 쓰는 object dictionary를 사용하지 말 것
- 모든 companyId/eventId/sourceId는 중복 금지
- 모든 참조 ID의 존재 여부를 validator가 검사
- 종료일이 시작일보다 빠른지, 날짜 범위가 겹치는 이름 이력이 있는지 검사
- 법인명·상장·합병·분사·파산·퇴출 사실에는 최소 하나의 sourceId 연결
- 출처 충돌 또는 불확실성은 needsReview=true로 남기고 추측 금지
- fictionalAlias나 가상 회사명 필드가 있으면 validator가 실패해야 함

사건은 baselineDate만 넣고 끝내지 말고 prerequisites와 failurePolicy(cancel, delay, substitute, transfer)를 데이터로 표현할 수 있게 설계하라. 플레이어가 사전에 회사를 인수하거나 기술을 선점하면 원래 사건이 강제 발생하지 않아야 한다.

런타임 C# 코드, Unity 씬, Editor, ProjectSettings, Packages, simul 저장소, Docs/PROJECT_STATE.md와 Docs/DECISIONS.md는 수정하지 마라. 외부 패키지도 추가하지 마라. 다른 작업자의 변경을 되돌리지 마라.

마지막에 validate_history_data.ps1을 실행하고 결과, 실제 생성한 회사/사건/출처 수, 2000~2003 상세 회사 수, 소형·부실·자산 인수 후보 수, needsReview 목록, simul에서 참고한 파일과 다음 확장 제안을 Docs/CLAUDE_HISTORY_PROGRESS.md에 기록하라. 가능하면 네 작업만 별도 커밋하되 기존 미커밋 변경이 있으면 커밋하지 말고 보고만 해라.
```

## 완료 판정

- 실제 국내 회사 60개 이상이 불변 ID와 날짜별 실제 이름으로 등록된다.
- 게임 UI용 `displayNameKo`가 실제 이름이며 가명 필드가 하나도 없다.
- 2000~2003 국내 회사 최소 25개에 주요 사건과 1차 출처가 있다.
- 2004~2026 회사 진입·퇴출 앵커가 있으며 해외 기업 상세 데이터는 아직 없다.
- 실제 근거가 있는 소형·부실·자산 인수 후보가 분리되어 있다.
- `simul` 참고 내역이 기록되고 `simul` 자체는 수정되지 않는다.
- validator가 오류 0으로 끝나며 불확실성은 숨기지 않고 보고한다.
- Unity 런타임은 한 줄도 수정하지 않는다.
