# PROJECT STATE

이 문서는 과거 작업 일지가 아니라 **현재 실행 가능한 상태, 아직 통합되지 않은 상태, 정확한 다음 작업**만 기록하는 정본이다. 날짜별 구현 증거는 `History/Reports/`에 보존하며 이 문서보다 우선하지 않는다.

## 2026-08-14 / 공용 업무 능력과 인사 roster

- 가족과 향후 채용 완료 직원은 기술개발·기획·창작·사업·운영·협업 6능력과 잠재력을 같은
  `WorkforceCapabilityState`로 사용한다. 별도 Speed는 없으며 업무별 10,000bp 프로필이 진행·품질·학습을 정한다.
- MainNavigationV2 인사 탭은 현재 고용된 가족 4명만 데이터 기반으로 표시한다. 잠재력은 숫자가 아니라
  S/A/B/C/D/F 등급만, 현재 스트레스와 스트레스 저항은 서로 다른 현재 상태로 표시한다.
- Save schema v10은 능력·분야별 XP·fixed-point remainder·스트레스 증가 배율을 저장한다. v1~v9의
  Speed/Stamina/Mental은 이관에서만 운영/스트레스 저항 초기값으로 읽으며 신규 권위 계산에서는 사용하지 않는다.
- 세부 수치와 XP/이관 경계는 `Docs/WORKFORCE_CAPABILITIES_V1.md`가 정본이다.

## 2026-08-14 / Windows Fast QA candidate

- `FAST_QA_WINDOWS.cmd`는 변경 분류, project-local lock, validation manifest, cache fingerprint, 순수 Simulation harness, Editor validation, scripts-only/normal Fast QA player build, D3D11 capture 재사용을 제공한다.
- 출력은 `Artifacts/FastQa`로 격리되며 release build/deploy 경로는 변경하지 않는다.
- 60초는 기능 PASS와 분리된 SLO다. cold import와 clean release는 별도 측정/최종 gate다.
- gameplay, seating, UI, stamina, stats, Save 구현 파일 변경은 0이다.

## 2026-08-14 / Shared stamina + placed-facility recovery

- All four family members start from one 10,000-unit profile. Integer GameTime drains 75% across a
  normal typing workday and creates recovery intent only at the 25% remaining threshold; profile
  overrides remain data-driven for later character differentiation.
- Recovery consumes the build editor's live capability query and existing runtime claim lifecycle.
  Only placed, reachable, available water/vending/lounge offers are selectable. Restroom remains
  fail closed because no definition/facility exists.
- A sticky stamina session gates routine autonomy refreshes while preserving attendance/mandatory
  and contract priority. Successful Performing completion releases the facility, returns to the
  exact assigned seat, and resumes the exact task/remaining minutes. Save schema v10 stores semantic
  stamina and workforce capability state and migrates earlier legacy state at the saved integer minute.
- Unity 6000.3.21f1 pure, 1x/2x/4x, save/load, prototype/micro-action, build capability, and PlayMode
  integration QA pass. `Artifacts/StaminaRuntimeQa/four-family-overhead-bars.png` captures all four
  bars and `summary.txt` records the completed runtime round trip.

## 기준선과 릴리스 판정

| 항목 | 현재 값 |
| --- | --- |
| 문서 정리 기준 코드 | local `main` code integration `8d714c6` |
| 문서 정리 당시 원격 기준 | `origin/main` `52a787f` |
| Unity | `6000.3.21f1` |
| 시작 씬 | `Assets/FamilyCompany/Scenes/Prototype01.unity` |
| 최종 통합 main SHA | 이 문서를 포함한 clean `main` HEAD; 배포본의 `BUILD_INFO.txt`와 대조 |
| 최종 Windows build SHA | 최종 빌드의 `BUILD_INFO.txt`가 유일한 정본 |
| 최종 통합 QA | compiler, Unity static, D3D PlayMode PASS; Windows player는 배포본 `BUILD_INFO.txt`와 실행 로그로 판정 |

`8d714c6`은 stamina까지 합친 코드 기준선이다. 이후 문서·통합 QA 수정은 같은 main 위에서만 수행하며, 실제 배포 SHA는 실행본 옆 `BUILD_INFO.txt`와 `git rev-parse HEAD`가 같아야 한다.

## 현재 런타임 정본

| 영역 | 현재 동작 |
| --- | --- |
| 새 게임 | `2000-01-03 08:50`, 가족 4인, 자본금 5,000,000원 |
| 출퇴근 | 가족만 `09:00`~`09:03` 1분 간격 입장, `18:00`부터 퇴근 |
| 직원 8인 | 시작 인원이 아닌 향후 채용 후보. 고용 전 런타임 출근 금지 |
| 사무실 | `StarterOfficeV1`, 13×13, 실내 가구 17 + 외곽 bay 52, 가족 workstation 4 |
| 외곽 출입구 | `(8,0)` threshold 1칸. `entrance_door`는 호환 ID이며 door leaf/jamb/lintel 애니메이션이 아님 |
| 메인 UI | `MainNavigationV2`: 회사·인사·사업·연구·투자 5개 허브 |
| 계약 | 고객 등급 `T0 → T1 → T2 → T3 → T4`, 순차 해금과 하락/회복 |
| 사무실 편집 | 배치·회전·이동·회수·재고·저장. 회사 허브에서 진입 |
| 저장 | 전체 `GameSaveDto v10`, `v1`~`v9` 읽기/이관; OfficeGrid 하위 스키마 `v4`, 가구 재고 하위 스키마 `v1` |
| 이동·애니메이션 | 공유 pivot/locomotion 규칙과 실제 frame displacement로 방향·걷기 판정 |
| 렌더 | 1920×1080 reference, native scale 1, pixel snap, 180 PPU, 캐릭터 scale 1.55 |
| Windows 실행 | 저장소 상대 경로 `BUILD_WINDOWS.cmd` / `RUN_WINDOWS.cmd`; `BUILD_INFO.txt`로 SHA 확인 |

근거 구현은 `GameTime`, `PrototypeStateFactory`, `OfficeAttendanceRules`, `StarterOfficeRuntimeBootstrap`, `MainNavigationHudPresenter`, `ContractGrowthValidation`, `GameSaveDto`, `OfficeNavigationMotionRules`, `DirectionalSpriteAnimator`, `PixelClarityProfile`에 있다.

## 현재 통합됨 (`8d714c6` 코드 기준)

- `MainNavigationV2`가 런타임에 연결되었고 거부된 V1 경로는 제거되었다. 회사 허브는 사무실 편집기, 사업은 계약/제품, 투자는 주식으로 연결된다.
- 계약 고객 성장은 day-one T0, T1~T4 순차 해금, 평판/실패 기반 하락과 T0 회복을 순수 시뮬레이션 규칙으로 처리한다.
- 사무실 편집기와 재고 저장은 v8에서 도입되었고, 현재 전체 저장 스키마 v10에 그대로 통합되어 있다. 별도 여섯 번째 하단 탭은 만들지 않는다.
- 플레이어, 가족 출퇴근, 계약 이동이 공유 office locomotion 규칙과 실제 변위를 사용한다.
- native render/pixel snap/viewport clarity 기준과 캐릭터 scale 1.55가 적용되었다.
- 외곽 bay 52개, 단일 threshold, 가족 09:00~09:03/18:00 출퇴근 규칙이 적용되었다.
- 주식 코어의 시장 시간, 7+7 호가, FIFO, 수수료·세금, 저장 결정론과 투자 허브 진입점이 유지된다.
- 저장소 상대 경로 Windows 빌드/실행 스크립트와 `BUILD_INFO.txt` 생성 절차가 있다.
- 네 가족의 착석 방향 잠금, 키보드 손 접촉, 의자 하체 가림, 안전한 이석 경로가 통합되었다. 자유 보행만 실제 변위 방향을 사용하며 착석 중에는 좌석 방향 잠금이 최종 권한이다.
- 네 가족의 체력과 머리 위 바가 통합되었다. 25% 임계치 전에는 체력 때문에 일어나지 않고, 임계치 이후 실제 배치·접근·capacity가 유효한 회복 시설만 claim해 수행·해제·원래 업무 복귀한다.

## 열린 기술 부채와 제품 backlog

1. `OfficeRuntimeOccupancy`의 `OfficeFurnitureCollisionCatalog` 의존을 배치된 가구 geometry query로 교체해 편집 직후 path/occupancy가 같은 footprint를 사용하게 한다.
2. 직원 후보 8인은 고용 시스템이 생긴 뒤에만 출근시킨다. 시작 roster나 09:00~09:03 가족 출근에 섞지 않는다.
3. 소파/다인 좌석은 group atomic claim, 짝 이동, 취소/퇴장 해제, non-NorthWest pose 승인과 idle/emote QA를 추가한다.
4. 오피스 확장은 현재 StarterOffice를 보존하며 단계별 면적/가구 해금으로 구현한다. 과거 요청서의 숫자를 검증 없이 새 정본으로 삼지 않는다.
5. 60일 외상 매출/지급, 경쟁 견적, 뉴스 조합은 `GAMEPLAY_FUN_V1.md`와 `DO_NOTS.md`의 재미·미래 누설 제한을 지키며 별도 설계/검증한다.
6. 주식은 전체 계좌/주문/체결/원장 persistence, S3/S4 시나리오, 외부 tape/호가 연결을 확장하되 기존 결정론을 보존한다.
7. Utility AI의 선택 규칙은 현재 `WeightedPick`이 정본이다. `ArgMax` 변경은 제안만으로 적용하지 않는다.

## 검증 상태

| 범위 | 기준 | 결과 |
| --- | --- | --- |
| MainNavigationV2 compiler/editor/player | `884c53f`, `bc19d0c`, `4cf6e50` | PASS 기록 있음 |
| 계약 T0~T4 pure harness/compiler | `a878ce1` | PASS 기록 있음 |
| 사무실 편집/저장 v10 호환 | `7baac22`, `8d714c6` | 기존 v9 compiler, logic, D3D PlayMode 기록과 v10 migration 회귀를 함께 확인 |
| 공유 이동/실제 변위 | `aeae43f` | strict harness와 D3D movement QA PASS 기록 있음 |
| native render clarity | `d235f41` | D3D render audit PASS 기록 있음 |
| 외곽/출퇴근 | `7954d42` 이후 기준선 | layout/PlayMode PASS 기록 있음 |
| seating + stamina + 위 전부의 최종 결합 | `8d714c6` 이후 main | compiler PASS; navigation strict, typing 24/24, seating facing/egress/depth, stamina 1/2/4x 및 실제 recovery 왕복, build editor PlayMode PASS |
| 최종 portable Windows build | 최종 main | clean HEAD에서만 생성하고 `BUILD_INFO.txt` SHA 일치와 Windows D3D11 main-flow PASS로 판정 |

과거 개별 PASS는 해당 기능의 회귀 근거다. 최종 결합 SHA의 PASS를 대신하지 않는다.

## 최종 릴리스 체크리스트

1. `git diff --check`, C# compiler, 순수 검증 harness, Unity D3D PlayMode/render QA를 통과한다.
2. 저장 v1~v9→v10, 새 게임 v10, 편집 재고, 계약 성장, 주식 계좌, 출퇴근, 실제 변위 회귀를 확인한다.
3. `BUILD_WINDOWS.cmd`로 새 실행본을 만들고 `BUILD_INFO.txt`와 현재 HEAD가 같은지 확인한다.
4. 검증된 폴더 전체를 `C:\Users\godho\Downloads\Family\FamilyCompany_Playtest`에 배포한다.

## 다른 PC에서 이어하기

```powershell
git switch main
git status --short --branch
git pull --ff-only origin main
.\BUILD_WINDOWS.cmd
.\RUN_WINDOWS.cmd
```

빌드가 이미 있더라도 `Builds/Windows/FamilyCompany_Playtest/BUILD_INFO.txt`의 commit이 `git rev-parse HEAD`와 다르면 최신 실행본으로 간주하지 않는다. 상세 절차는 [HOME_PC_CONTINUATION_GUIDE.md](HOME_PC_CONTINUATION_GUIDE.md)와 [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)를 따른다.

## 정본 문서 경계

- 인물·출퇴근·사무실 시각: [CANON.md](CANON.md), [ART_STYLE.md](ART_STYLE.md)
- 구조·저장·Unity 경계: [ARCHITECTURE.md](ARCHITECTURE.md)
- 내비게이션·편집: [MAIN_NAVIGATION_HUD_V2.md](MAIN_NAVIGATION_HUD_V2.md), [OFFICE_BUILD_EDITOR_V1.md](OFFICE_BUILD_EDITOR_V1.md)
- 계약: [CONTRACTS_V0_3.md](CONTRACTS_V0_3.md), [CONTRACT_CLIENT_PROGRESSION_V1.md](CONTRACT_CLIENT_PROGRESSION_V1.md)
- 주식: [SIMUL_MARKET_PORT.md](SIMUL_MARKET_PORT.md), [STOCK_MARKET_LANDSCAPE_V1.md](STOCK_MARKET_LANDSCAPE_V1.md)
- 역사 구현 증거: `History/Reports/` — 현재 상태 정본 아님
