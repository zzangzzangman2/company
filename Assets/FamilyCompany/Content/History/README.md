# KOREA HISTORY DATA V1

실제 국내 회사의 역사 기준선과 대체역사 분기용 데이터다. 이 폴더는 읽기 전용 콘텐츠이며 저장 게임이 덮어쓰지 않는다.
규칙의 근거는 `Docs/REAL_COMPANY_ALT_HISTORY.md`, 작업 범위는 `Docs/CLAUDE_HANDOFF_HISTORY_DATA.md`다.

## 현재 범위

- 국내 실제 회사 82개와 넥슨의 국외 지배법인 연결용 지원 행 1개를 등록했다. 국외 회사 상세 사건은 없다.
- 2000-01-01~2003-12-31 상세 회사 25개와 사건 42개를 제공한다. 상세 25개사는 모두 최소 1개의 1차 출처에 연결된다.
- 2004~2026 회사 진입·퇴출 앵커 42개, 거시·산업 앵커 14개를 제공한다.
- 인수 후보 20개 중 초반 또는 성장 후 도달 가능 후보는 13개, 자산 단위 후보는 10개다.
- 출처는 현재 데이터가 실제로 참조하는 100건만 남겼다.

## 파일

| 파일 | 최상위 배열 | 역할 |
| --- | --- | --- |
| `schema_version_1.json` | `files`, `entities`, `enums`, `vocabularies` | DTO 필드·enum·불변식의 실행 정본 |
| `company_registry_korea_2000_2026.json` | `companies` | 불변 ID, 실제 날짜별 법인명, 브랜드, 상장·소유 이력 |
| `company_events_korea_2000_2003.json` | `events` | 상세 기간의 조건부 역사 사건 |
| `company_entry_exit_korea_2004_2026_anchor.json` | `anchors` | 후속 회사의 설립·상장·인수·합병·파산 등 확장 순서표 |
| `macro_timeline_korea_2000_2026_anchor.json` | `anchors` | 국내 거시 충격과 산업 전환의 얇은 뼈대 |
| `acquisition_evidence_korea.json` | `candidates` | 법인·자산 인수 후보와 공개된 규모 근거 |
| `sources.json` | `sources` | 모든 역사 주장에 연결되는 출처 레지스트리 |

## 이름과 ID 규칙

- `companyId`는 회사명과 무관한 영구 ID다. 저장·계약·소유관계는 ID를 참조한다.
- 화면 이름은 날짜에 맞는 `nameHistory[].displayNameKo`를 사용한다. 모두 실제 한글 이름이며 가명 필드는 없다.
- `legalNameKo`, `legalNameEn`, `displayNameKo`, `fromDate`, `toDate`, `sourceIds`가 이름 이력의 최소 단위다.
- 브랜드·서비스는 법인으로 만들지 않고 `actualBrands`에서 당시 운영 법인 ID에 연결한다.
- 열린 종료일은 `""`이다. 월·연 단위 사실의 날짜는 첫날 자리표시자일 수 있으므로 `needsReview`와 문구를 함께 확인해야 한다.

## 사건 해결 계약

사건은 날짜가 되었다고 강제 재생하지 않는다. 후보 날짜에 `prerequisites`를 WorldState로 평가한다.

- 조건 충족: `effects`를 적용하고 `newsKeyBaseline`을 사용한다.
- 조건 실패: `failurePolicy`의 `cancel`, `delay`, `substitute`, `transfer`로 분기하고 `newsKeyDiverged`를 사용한다.
- 플레이어가 회사를 먼저 인수하거나 기술을 선점하면 원래 회사의 사건은 그대로 발생하지 않는다.
- `minCashKrw`와 `magnitude`는 역사 수치가 아니라 시뮬레이션 임계값이다.

## 인수 근거 규칙

- 공개된 자산·부채·매출·시가총액·거래금액만 `evidenceMetrics`에 출처와 함께 기록한다.
- 게임 인수금액은 역사 사실처럼 저장하지 않는다. 공개 금액이 없으면 회차 상태가 계산한다.
- 4인 회사가 법인 전체를 살 수 없으면 사업부·소스코드·특허·도메인·장비·고객계약·핵심인력 후보로 분리한다.
- `playerAffordabilityHint`는 게임 설계 힌트이며 역사적 가치평가가 아니다.

## 출처와 불확실성

`sourceTier` 우선순위는 `primary_filing` > `primary_company` > `secondary_press` > `reference`다.

- `fetched_full_page`: 원문 본문을 직접 읽었다.
- `search_index_of_document`: 문서 검색 색인으로 내용을 확인했다.
- `not_reached`: 위치만 확보했고 본문을 읽지 못했다. 이 출처만 쓰는 항목은 반드시 `needsReview=true`다.
- `needsReview=true`면 `reviewNote`가 비어 있으면 안 된다. 불확실한 날짜·법인 관계·금액·인과관계를 확정값처럼 쓰지 않는다.

## Unity JSON 제약

- BOM 없는 UTF-8, 주석 없음, ISO `YYYY-MM-DD` 날짜
- 최상위 객체 아래 이름이 고정된 배열 사용
- object dictionary와 배열의 배열 사용 금지
- 빈 값은 `null` 대신 `""`, `0`, `[]` 사용
- `JsonUtility`가 모르는 추가 필드를 무시할 수 있도록 스키마를 전방 확장한다.

## 검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File HistoryTools\validate_history_data.ps1
```

현재 결과는 오류 0, 경고 0, 종료 코드 0이다. validator는 인코딩·파싱·스키마 필드·enum·ID 중복·모든 참조·날짜 범위·이름 구간 겹침·사건 분기 필드·가명 금지·회사 수·상세 수·1차 출처 커버·인수 후보 근거를 검사한다.

## 범위 밖

- 2004~2026 회사별 상세 사건과 일별 시장 수치
- WorldState, 재무·주가 계산, 저장 시스템
- 런타임 C#, Unity 씬, Editor 코드
- 로고·트레이드 드레스·광고물
