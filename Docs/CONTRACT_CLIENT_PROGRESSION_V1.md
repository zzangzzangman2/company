# 계약 고객 성장 V1

이 문서는 2000년 가족회사의 `첫 하청 → 실적 → 상위 고객 → 자체 제품` 규칙과 현재 통합 경계를 기록한다. 구현은 기존 `BootstrapContractCatalog` 21종, `ContractPortfolio`, `CompanyGrowthState`, Korea History V1 기업 등록부와 전체 Save v8을 보존한다.

## 현재 정본과 읽기 호환

- 새 게임은 2000-01-03 08:50, 가족 4인, 현금 5,000,000원으로 시작한다.
- 첫 계약은 자동 수락하지 않는다. 사업 허브의 첫 계약 추천 3개 중 하나를 플레이어가 고른다.
- `MainNavigationV2` 사업 허브의 `ContractBusinessRuntimeAdapter`가 계약/제품의 유일한 정본 진입점이다. 첫날에는 T0만 노출하며 삼성전자·LG전자·SK텔레콤을 고정 생성하는 구형 관리 UI를 다시 연결하지 않는다.
- 기존 실제 기업명과 Korea History V1 `companyId`는 변경하지 않는다. 과거 UI의 `samsung-electronics`, `lg-electronics`, `sk-telecom`은 각각 기존 등록부 ID로 읽기 호환한다.
- 저장소에는 실존 기업 로고 리소스가 없다. 계약 카드는 텍스트 회사명과 중립 업종 아이콘을 쓰며 실존 로고를 생성하거나 변형하지 않는다.

## 고객 단계

2000-01-03에 실제로 존재하는 등록부 기업만 제안 후보가 된다. 등록부에 개인사업자/동네 상점 단계가 없으므로 T0만 네 개의 중립 상호를 최소 보완했다.

- T0 동네 사업자: 신촌 사진관, 종로 타자학원, 마포 비디오·만화 대여점, 용산 휴대폰 수리점.
- T1 지역 소기업 25개: 액토즈소프트, 안철수연구소, CCR, 컴투스, 다날, 다나와, 더존디지털웨어, 드림위즈, 이스트소프트, 게임빌, 한빛소프트, 한글과컴퓨터, 핸디소프트, 한게임커뮤니케이션, 인크루트, 이니시스, 잡코리아, 한국사이버결제, 한국정보인증, 엠게임, 모빌리언스, 나우콤, 소프트포럼, 소프트맥스, 티맥스소프트.
- T2 성장 기업 15개: 거원시스템, 프리챌, 휴맥스, 아이디스, 주성엔지니어링, 엔씨소프트, 네오위즈, 넥슨코리아, 레인콤, 새롬기술, 서울반도체, 심텍, 브이케이, 예스24, 유일전자.
- T3 전문 발주사 12개: 옥션, 다우기술, 다음커뮤니케이션, 인터파크, LG-EDS시스템, 네이버컴, 온세통신, 팬택, 삼성SDS, 세원텔레콤, SK C&C, 텔슨전자.
- T4 전국 대기업 10개: 데이콤, 하나로통신, 한국통신, KTF, LG전자, LG텔레콤, 삼성전자, 신세기통신, SK텔레콤, 두루넷.

회사의 역사적 이름과 존속 기간은 등록부 날짜를 따른다. 이미 수락한 계약은 회사가 개명·합병된 뒤에도 저장된 client ID/표시명과 실적을 잃지 않는다.

## 해금 기준

각 상위 단계는 이전 단계가 열린 상태에서 아래 조건을 모두 만족해야 한다. 완료 건수만 채워서는 열리지 않는다.

| 단계 | 완료 | 정시율 | 품질 | 만족 | 평판 | 선택 업종 경험 | 회사 등급 | 역량 |
|---|---:|---:|---:|---:|---:|---:|---|---:|
| T1 | 3 | 70% | 55 | 55 | 4 | 35인시 | LocalProfessional | 45 |
| T2 | 8 | 80% | 65 | 62 | 12 | 120인시 | GrowthCompany | 55 |
| T3 | 16 | 85% | 72 | 70 | 28 | 300인시 | EstablishedVendor | 65 |
| T4 | 28 | 90% | 80 | 78 | 45 | 600인시 | PrimeReady | 75 |

실패는 정시율·만족·평판을 낮추며 이후 좋은 계약으로 회복할 수 있다. 일반 게시판의 첫 슬롯은 항상 T0 회복 계약이다. 상위 단계가 열려도 나머지 슬롯은 최고 단계 45%, 한 단계 아래 30%, T0 17%, 그 밖의 하위 단계 8%를 기본으로 섞는다.

## 첫 계약 추천

제안은 world seed와 stable ID가 같으면 동일하며 첫 계약을 수락할 때까지 날짜가 지나도 바뀌지 않는다.

1. 신촌 사진관 / 미니홈피용 64×64 아바타 도트: 14인시, 5일, 1명, 360,000원, 낮은 위험.
2. 종로 타자학원 / 타자 연습 프로그램 단어 DB 입력: 26인시, 7일, 2명, 착수비 40,000원, 720,000원, 보통 위험.
3. 마포 비디오·만화 대여점 / 연체 관리: 36인시, 9일, 2명, 착수비 100,000원, 1,100,000원, 보통 위험.

## 제안·정산·결정론

- 일반 제안은 `worldSeed + 달력 날짜 + 업종 + 슬롯 + clientId + templateId`로 생성한다. UI 재진입, 프레임 수, 일시정지는 reroll 입력이 아니다.
- 기존 21종은 `legacy-contract-template-v1:00`부터 `:20`까지 metadata만 덧붙이며 원본 수치·제목을 복제하거나 삭제하지 않는다.
- 보상은 인시, 단계별 시간 단가, 요구 역량, 품질, 납기 압력, 원본 보상 anchor를 합친 한 공식을 사용한다. 최종 범위는 T0 280,000~1,200,000원, T1 450,000~1,500,000원, T2 700,000~1,800,000원, T3 1,000,000~2,200,000원, T4 1,300,000~2,500,000원이다.
- 수락 계약은 기존 `ContractPortfolio`와 ledger transaction `contract:{offerId}:settlement`를 사용하므로 완료 정산은 정확히 한 번이다.
- `AuthoritativeContractWorkSession`은 증가한 GameTime 60분마다 1인시만 기록한다. 실시간 초, frame rate, UI 열기/닫기는 작업량을 만들지 않는다.

## Save v8과 자체 제품

- 전체 Save v8은 v1~v7을 읽어 이관한다. 첫 계약 수락 여부는 저장된 계약 존재 여부로, 실적 ledger는 resolved 계약의 stable offer ID/status/contributions/resolved minute에서 재구성하므로 별도 중복 진행도 payload를 만들지 않는다.
- 과도 상태인 게시판·route·작업 command session은 저장하지 않는다. 저장/로드 후 동일 seed/달력 날짜/실적이면 동일 제안이 나온다.
- 자체 제품 후보는 기존 `CompanyGrowthState`의 연구, 시장 보고서, 자체 사업, 제품 프로젝트를 사용한다. 현금·전체 완료 건수·관련 업종 인시·평판·필요 연구·해당 시장 보고서를 실제 진행도로 보여 준다.

## 중앙 UI 통합 경계

중앙 UI는 공유 HUD 스킨을 이 기능에서 수정하지 않고 `ContractBusinessRuntimeAdapter`만 호출한다.

1. 하단 `사업` 버튼 → `OpenBusinessHub()`.
2. 허브 `하청 계약` 카드 → `OpenContractBoard()`.
3. 허브 `자체 제품` 카드 → `OpenProductOpportunities()`.
4. 카드 수락 → `TryAcceptOffer(offerId)`.
5. 가족 배정 → `RequestFamilyAssignment(offerId, memberId)`.
6. 플레이어 작업 중 GameTime 변경 뒤 → `AdvancePlayerWorkFromGameTime()`.
7. 뒤로 → `TryBack()`, 사무실 복귀 → `ReturnToOffice()`.

`ContractGrowthQaPresenter`는 독립 검증용 임시 클릭 화면이며 최종 HUD 스킨이 아니다.

## 검증 상태

- 실제 등록부 기반 100 seed 순수 하네스에서 첫날 T0만 노출, T3/T4 0건, 추천 3개 결정론, 1회 정산, Save v8 왕복/구버전 이관, 다중 지표 순차 해금, 실패 회복, 해금 뒤 삼성전자 등장, GameTime 정지 시 작업 0, 제품 진행도와 route stack을 통과했다.
- Unity 6000.3.21f1 관리 어셈블리 기준 Simulation/Presentation/Editor 독립 컴파일은 경고 0·오류 0이다.
- `MainNavigationV2` 통합 D3D11 Player QA에서 계약/제품 route, loaded-state rebind, ESC/back이 PASS했다. 최종 seating/stamina 결합 SHA의 재검증은 [PROJECT_STATE.md](PROJECT_STATE.md)에 기록한다.
