# CLAUDE HISTORY PROGRESS

최종 갱신: 2026-08-10

## 상태

KOREA HISTORY DATA V1 작업을 완료했다. 교체 전 국제 12개사 데이터 파일은 제거했고, 한국판 정본 파일명과 스키마·validator·문서를 일치시켰다.

최종 검증: **오류 0, 경고 0, 종료 코드 0**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File HistoryTools\validate_history_data.ps1
```

런타임 C#, 씬, Editor, ProjectSettings, Packages, `simul`, `Docs/PROJECT_STATE.md`, `Docs/DECISIONS.md`는 수정하지 않았다.

## 산출물

1. `Assets/FamilyCompany/Content/History/schema_version_1.json`
2. `Assets/FamilyCompany/Content/History/company_registry_korea_2000_2026.json`
3. `Assets/FamilyCompany/Content/History/company_events_korea_2000_2003.json`
4. `Assets/FamilyCompany/Content/History/company_entry_exit_korea_2004_2026_anchor.json`
5. `Assets/FamilyCompany/Content/History/macro_timeline_korea_2000_2026_anchor.json`
6. `Assets/FamilyCompany/Content/History/acquisition_evidence_korea.json`
7. `Assets/FamilyCompany/Content/History/sources.json`
8. `Assets/FamilyCompany/Content/History/README.md`
9. `HistoryTools/validate_history_data.ps1`
10. `Docs/CLAUDE_HISTORY_PROGRESS.md`

교체 전 파일 `company_registry_2000_2003.json`, `company_events_2000_2003.json`, `macro_timeline_2000_2026_anchor.json`과 각 `.meta`는 한국판 범위 밖이라 제거했다. 사용되지 않던 국제판 출처 77건도 `sources.json`에서 제거했다.

## 실제 개수

| 항목 | 개수 |
| --- | ---: |
| 실제 국내 회사 | 82 |
| 전체 등록부 행 | 83 |
| 2000~2003 상세 회사 | 25 |
| 1차 출처가 연결된 상세 회사 | 25 / 25 |
| 2000~2003 회사 사건 | 42 |
| 2004~2026 진입·퇴출 앵커 | 42 |
| 2000~2026 거시 앵커 | 14 |
| 인수 후보 | 20 |
| 초반/성장 후 도달 가능 인수 후보 | 13 |
| 자산 단위 인수 후보 | 10 |
| 출처 | 100 |
| `needsReview` 레코드 | 302 |

등록부의 비국내 행 1개는 `kr_nexon_jp`다. 넥슨코리아의 지배구조 연속성을 참조하기 위한 `anchor_only` 지원 행이며 국외 상세 사건은 만들지 않았다.

## 출처 현황

출처 등급: `primary_filing` 15, `primary_company` 24, `secondary_press` 23, `reference` 38.
확인 방법: `fetched_full_page` 12, `search_index_of_document` 79, `not_reached` 9.

이번 이어받기에서 DART 원문을 직접 찾아 다음을 보강·교정했다.

- 팬택앤큐리텔 제4기 사업보고서: 2001년 4월 현대큐리텔 설립, 2001-05-01 하이닉스 무선단말기 사업 양수, 2002년 3월·8월 사명 변경, 양수 자산 226,944백만원·부채 82,197백만원.
- 텔슨전자 제12기 사업보고서: 법인 설립일 1992-03-11, 2003년 9월 부속 토지 195억원 및 12월 사옥 990억원 매각을 통한 자산구조조정.
- 세원텔레콤 제11기 사업보고서: 1992-05-20 법인 전환, 1996-08-01 사명 변경, 2001~2002년 중국 업체 대상 GSM·CDMA 공급계약.
- 솔본 2011-03-11 공시: 프리챌 파산선고 결정일은 보도일이 아닌 2011-03-10.
- 기존 보강분: NAVER·옥션·엔씨소프트·한빛소프트·웹젠·위메이드·다날의 공식 연혁·사업보고서와 eBay 공식 발표.

또한 엔씨소프트 코스닥 상장일을 2000-06-14로 바로잡고, 텔슨전자 사건을 2003년 9월 자산구조조정으로 구체화했으며, 팬택 등록부에 반대로 들어가 있던 현대큐리텔의 팬택 지배 관계를 제거했다.

## `needsReview` 목록

validator가 집계하는 302건의 구성은 다음과 같다.

| 위치 | `needsReview=true` |
| --- | ---: |
| 회사 행 | 36 |
| 이름 이력 | 100 |
| 브랜드 이력 | 35 |
| 상장 이력 | 20 |
| 소유 이력 | 10 |
| 2000~2003 사건 | 28 |
| 2004~2026 진입·퇴출 앵커 | 37 |
| 거시 앵커 | 14 |
| 인수 후보 | 20 |
| 인수 규모 지표 | 2 |

우선 확인할 항목:

- `kr_nexon_kr`, `kr_nexon_jp`: 2000년 당시 실제 법인명과 2002~2005 지배구조 전환 시점.
- `evt_kr_pantech_curitel_acq_2001_11`: 보도된 1,600억원과 컨소시엄 실제 지급·지분 구조 및 거래 완료일.
- `evt_kr_freechal_paid_community_2002_11`: 유료화 시행의 정확한 일자. 파산일은 DART로 확정했다.
- `evt_kr_sewon_china_failure_2002`: 공급계약은 공시로 확인했지만 중국 사업 부진·차입 위기 인과는 2차 자료다.
- `acq_kr_telson_equipment_2003`: 부동산 처분은 공시로 확정했지만 장비 개별 매각은 미확인.
- `acq_kr_sewon_china_unit_2003`, `acq_kr_freechal_domain_2011`, `acq_kr_thrunet_ops_asset_2003`: 실제 분리 매각·처분 경로 확인 필요.
- `anc_kr_kosdaq_bubble_collapse_2000`, `anc_kr_broadband_buildout_2000_2005`, `anc_kr_mobile_data_2000_2004`, `anc_kr_card_crisis_2003`: 창의 양끝과 국내 파급 수치를 한국은행·정부 원문으로 확정할 필요가 있다.
- 거시 앵커 14건 전체: 산업 전환은 연속 구간이라 시작·종료 경계가 설계값인 경우가 많다. 회사 결과를 직접 만들지 않도록 유지해야 한다.

1차 출처 커버 25/25는 각 상세 회사가 최소 한 건의 공시·회사 원문에 연결된다는 뜻이다. 모든 사건의 일 단위 날짜와 인과가 확정되었다는 뜻은 아니며, 남은 불확실성은 위 레코드에 그대로 보존했다.

## `simul` 참고 내역

읽기 전용으로 다음을 조사했다.

- `Docs/DATA_SOURCES.md`: 2000~2026 연대별 사건 문법, 1차 자료 링크, 실제 거래일 캘린더.
- `Docs/SIMULATION_CORE.md`: 일별 상태 전이와 사건 인과 처리 원칙.
- `Docs/COMPANY_SYSTEM.md`: 회사·공시·기업행동 모델.
- `lib/data/market_era_events.dart`, `market_corpus_events.dart`, `market_corpus_calendar.dart`, `market_corpus_daily_samples.dart`, `market_arc_scenarios.dart`.
- `lib/models/corporate_disclosure.dart`, `lib/services/game_engine_corporate_actions.dart`.

재사용한 것은 사건의 원인→조건→효과 문법, 실제 거래일 캘린더 개념, 공시·기업행동의 조건부 처리 방식이다. 6,545개 거래일과 52,365줄 일별 표본을 포함한 파생 수치 데이터는 복사하지 않았다. `simul` 파일은 수정하지 않았다.

## 다음 확장 제안

1. DART·KIND 원문으로 `needsReview`가 많은 이름·상장·소유 이력을 우선 정리한다.
2. 코스닥 버블 붕괴, 초고속인터넷 가격전쟁, 이동통신 데이터, 카드사태를 월별 압력 곡선으로 상세화한다.
3. 2004~2008 진입·퇴출 앵커를 상세 사건으로 승격하고 실제 거래일에 맞춘다.
4. 인수 후보의 자산·부채·매출·거래금액을 같은 기준일의 공시 지표로 확장한다.
5. 런타임 작업은 별도 담당자가 스키마 DTO와 WorldState resolver를 구현할 때 시작한다.

## 커밋

작업 시작 시 기존의 미커밋·미추적 변경이 허용 경로 밖에 존재했다. 다른 작업자의 변경과 섞지 않기 위해 커밋하지 않았다.
