# PROJECT STATE

이 문서는 과거 작업 일지가 아니라 **현재 실행 가능한 상태, 아직 통합되지 않은 상태, 정확한 다음 작업**만 기록하는 정본이다. 날짜별 구현 증거는 `History/Reports/`에 보존하며 이 문서보다 우선하지 않는다.

## 2026-08-14 / Shared stamina + placed-facility recovery candidate

- All four family members start from one 10,000-unit profile. Integer GameTime drains 75% across a
  normal typing workday and creates recovery intent only at the 25% remaining threshold; profile
  overrides remain data-driven for later character differentiation.
- Recovery consumes the build editor's live capability query and existing runtime claim lifecycle.
  Only placed, reachable, available water/vending/lounge offers are selectable. Restroom remains
  fail closed because no definition/facility exists.
- A sticky stamina session gates routine autonomy refreshes while preserving attendance/mandatory
  and contract priority. Successful Performing completion releases the facility, returns to the
  exact assigned seat, and resumes the exact task/remaining minutes. Save schema v9 stores semantic
  stamina only and migrates v1-v8 legacy energy at the saved integer minute.
- Unity 6000.3.21f1 pure, 1x/2x/4x, save/load, prototype/micro-action, build capability, and PlayMode
  integration QA pass. `Artifacts/StaminaRuntimeQa/four-family-overhead-bars.png` captures all four
  bars and `summary.txt` records the completed runtime round trip.

## 기준선과 릴리스 판정

| 항목 | 현재 값 |
| --- | --- |
| 문서 정리 기준 코드 | local `main` `4cf6e50` |
| 문서 정리 당시 원격 기준 | `origin/main` `52a787f` |
| Unity | `6000.3.21f1` |
| 시작 씬 | `Assets/FamilyCompany/Scenes/Prototype01.unity` |
| 최종 통합 main SHA | `PENDING_ROOT_INTEGRATION` |
| 최종 Windows build SHA | `PENDING_ROOT_INTEGRATION` |
| 최종 통합 QA | `PENDING_ROOT_INTEGRATION` |

`4cf6e50`은 아래 “현재 통합됨”을 확인한 문서 기준선이다. seating/stamina 후보가 아직 최종 `main`에 합쳐지지 않았으므로 이 문서 후보만으로 최종 릴리스를 선언하지 않는다. 최종 통합 담당은 병합 후 위 세 placeholder와 검증 표의 최종 행만 채우면 된다.

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
| 저장 | 전체 `GameSaveDto v8`, `v1`~`v7` 읽기/이관; OfficeGrid 하위 스키마 `v4`, 가구 재고 하위 스키마 `v1` |
| 이동·애니메이션 | 공유 pivot/locomotion 규칙과 실제 frame displacement로 방향·걷기 판정 |
| 렌더 | 1920×1080 reference, native scale 1, pixel snap, 180 PPU, 캐릭터 scale 1.55 |
| Windows 실행 | 저장소 상대 경로 `BUILD_WINDOWS.cmd` / `RUN_WINDOWS.cmd`; `BUILD_INFO.txt`로 SHA 확인 |

근거 구현은 `GameTime`, `PrototypeStateFactory`, `OfficeAttendanceRules`, `StarterOfficeRuntimeBootstrap`, `MainNavigationHudPresenter`, `ContractGrowthValidation`, `GameSaveDto`, `OfficeNavigationMotionRules`, `DirectionalSpriteAnimator`, `PixelClarityProfile`에 있다.

## 현재 통합됨 (`4cf6e50` 기준)

- `MainNavigationV2`가 런타임에 연결되었고 거부된 V1 경로는 제거되었다. 회사 허브는 사무실 편집기, 사업은 계약/제품, 투자는 주식으로 연결된다.
- 계약 고객 성장은 day-one T0, T1~T4 순차 해금, 평판/실패 기반 하락과 T0 회복을 순수 시뮬레이션 규칙으로 처리한다.
- 사무실 편집기와 재고 저장이 전체 저장 스키마 v8에 통합되었다. 별도 여섯 번째 하단 탭은 만들지 않는다.
- 플레이어, 가족 출퇴근, 계약 이동이 공유 office locomotion 규칙과 실제 변위를 사용한다.
- native render/pixel snap/viewport clarity 기준과 캐릭터 scale 1.55가 적용되었다.
- 외곽 bay 52개, 단일 threshold, 가족 09:00~09:03/18:00 출퇴근 규칙이 적용되었다.
- 주식 코어의 시장 시간, 7+7 호가, FIFO, 수수료·세금, 저장 결정론과 투자 허브 진입점이 유지된다.
- 저장소 상대 경로 Windows 빌드/실행 스크립트와 `BUILD_INFO.txt` 생성 절차가 있다.

## 최종 통합 대기

아래는 후보 브랜치의 존재와 검토 대상을 기록한 것이며 **현재 main 완료 항목이 아니다**.

| 후보 | 후보 SHA | 현재 판정 | 최종 main SHA/QA |
| --- | --- | --- | --- |
| seating transition/depth | `codex/seating-transitions-depth` `96a921d` (foundation `24bae1a`) | main과 겹치는 파일을 해소해 통합·회귀 검증 필요 | `PENDING_ROOT_INTEGRATION` |
| stamina/needs simulation | `codex/stamina-needs-sim` `22644e2` (foundation `b2ee8f1`) | main과 겹치는 파일을 해소해 통합·회귀 검증 필요 | `PENDING_ROOT_INTEGRATION` |

통합 시 `GameSaveDto v8`, office occupancy/movement, MainNavigation route, 기존 계약/주식 저장이 보존되는지 함께 확인한다. 후보 SHA만 존재한다는 이유로 완료 처리하지 않는다.

## 열린 기술 부채와 제품 backlog

1. seating/stamina 후보를 현재 main 위에 통합하고 compile, pure harness, Unity D3D PlayMode, save migration을 다시 검증한다.
2. `OfficeRuntimeOccupancy`의 `OfficeFurnitureCollisionCatalog` 의존을 배치된 가구 geometry query로 교체해 편집 직후 path/occupancy가 같은 footprint를 사용하게 한다.
3. 통합 SHA에서 Windows 실행본을 새로 만들고 `BUILD_INFO.txt` SHA, 첫 화면, 타이틀→사무실, 계약, 주식, 편집 저장/불러오기를 확인한다.
4. 직원 후보 8인은 고용 시스템이 생긴 뒤에만 출근시킨다. 시작 roster나 09:00~09:03 가족 출근에 섞지 않는다.
5. 소파/다인 좌석은 group atomic claim, 짝 이동, 취소/퇴장 해제, non-NorthWest pose 승인과 idle/emote QA를 추가한다.
6. 오피스 확장은 현재 StarterOffice를 보존하며 단계별 면적/가구 해금으로 구현한다. 과거 요청서의 숫자를 검증 없이 새 정본으로 삼지 않는다.
7. 60일 외상 매출/지급, 경쟁 견적, 뉴스 조합은 `GAMEPLAY_FUN_V1.md`와 `DO_NOTS.md`의 재미·미래 누설 제한을 지키며 별도 설계/검증한다.
8. 주식은 전체 계좌/주문/체결/원장 persistence, S3/S4 시나리오, 외부 tape/호가 연결을 확장하되 기존 결정론을 보존한다.
9. Utility AI의 선택 규칙은 현재 `WeightedPick`이 정본이다. `ArgMax` 변경은 제안만으로 적용하지 않는다.

## 검증 상태

| 범위 | 기준 | 결과 |
| --- | --- | --- |
| MainNavigationV2 compiler/editor/player | `884c53f`, `bc19d0c`, `4cf6e50` | PASS 기록 있음 |
| 계약 T0~T4 pure harness/compiler | `a878ce1` | PASS 기록 있음 |
| 사무실 편집/저장 v8 | `7baac22` | compiler, logic, D3D PlayMode PASS 기록 있음 |
| 공유 이동/실제 변위 | `aeae43f` | strict harness와 D3D movement QA PASS 기록 있음 |
| native render clarity | `d235f41` | D3D render audit PASS 기록 있음 |
| 외곽/출퇴근 | `7954d42` 이후 기준선 | layout/PlayMode PASS 기록 있음 |
| seating + stamina + 위 전부의 최종 결합 | 최종 main | `PENDING_ROOT_INTEGRATION` |
| 최종 portable Windows build | 최종 main | `PENDING_ROOT_INTEGRATION` |

과거 개별 PASS는 해당 기능의 회귀 근거다. 최종 결합 SHA의 PASS를 대신하지 않는다.

## 최종 통합 담당 체크리스트

1. clean `main`에서 seating/stamina를 통합하고 충돌을 의미 기준으로 해소한다.
2. `git diff --check`, C# compiler, 순수 검증 harness, Unity D3D PlayMode/render QA를 실행한다.
3. 저장 v1~v7→v8, 새 게임 v8, 편집 재고, 계약 성장, 주식 계좌, 출퇴근, 실제 변위 회귀를 확인한다.
4. `BUILD_WINDOWS.cmd`로 새 실행본을 만들고 `BUILD_INFO.txt`가 최종 SHA인지 확인한다.
5. 이 문서의 `PENDING_ROOT_INTEGRATION`을 실제 main/build SHA와 PASS/FAIL로 교체한다.

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
