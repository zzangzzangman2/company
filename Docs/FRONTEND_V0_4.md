# FRONTEND V0.4 — 현재 UI 정본

## 목표

가족회사의 프론트엔드는 2000년대 소형 가족회사라는 분위기를 살리면서, 현재 시뮬레이션 상태와 실행 가능한 경로만 보여 준다. PNG에는 조작 가능한 글자·가격·날짜를 굽지 않고 Unity UI가 모든 동적 정보를 그린다.

## 반응형 기준

- reference canvas는 `1920×1080`이다. 16:10을 포함한 넓은 창은 safe area와 균등 여백으로 대응한다.
- 화면비가 `1.35` 미만이면 compact 레이아웃과 `440×481` 타이틀 키아트를 사용한다.
- 폰트는 `ManagementUiFontCatalog_v1`의 TMP dynamic font/fallback 체인을 사용한다.
- 월드 Sprite는 Point/native resolution, painted UI와 키아트는 Bilinear를 유지한다.
- 한글, 숫자, 버튼 hitbox, focus/hover/pressed/disabled 상태는 모든 지원 화면비에서 잘리면 안 된다.

## 타이틀 화면

- 가로형은 V2 등각 사무실 키아트, 세로형은 V3 compact art, 왼쪽 세로 메뉴는 V6 구성이다.
- 메뉴는 새 게임, 이어하기, 설정, 종료를 제공하며 이어하기는 저장 슬롯 상태를 의미 데이터로 표시한다.
- 2.8초 돈다발 연출은 장식이며 입력·저장·시간 상태를 바꾸지 않는다.
- 배경 이미지 안에 제목, 메뉴 글자, 슬롯 정보, 가격을 포함하지 않는다.

## 게임 중 MainNavigationV2

사무실 기본 HUD의 정본은 `MainNavigationV2`다.

- 상단에는 회사명, 날짜/시간, `1× / 2× / 4×` 시간 제어만 유지한다.
- 하단에는 회사·인사·사업·연구·투자 다섯 탭만 둔다. 사무실 편집기를 여섯 번째 탭으로 추가하지 않는다.
- 회사 허브의 `건축·편집` 카드가 `OfficeBuildEditorNavigationAdapter`를 통해 사무실 편집기를 연다.
- 사업 허브는 `ContractBusinessRuntimeAdapter`를 통해 하청 계약과 자체 제품을 연다.
- 투자 허브는 stock landscape route를 연다.
- 인사·연구의 아직 구현되지 않은 카드는 `준비 중`을 명시하고 가짜 상태나 임시 화면을 만들지 않는다.
- `Esc`/뒤로는 현재 패널→허브→사무실 순으로 route stack을 닫는다.
- 사무실 편집 중에만 게임 시간이 멈춘다. 다른 패널은 시뮬레이션 정지 여부를 임의로 바꾸지 않는다.
- 기존 가족별 긴 상태줄, `LIVE`, 저장 완료 토스트 상시 노출, 단축키 도움, 구형 관리 화면 버튼은 기본 HUD에 복원하지 않는다.

자세한 시각·route 규칙은 [MAIN_NAVIGATION_HUD_V2.md](MAIN_NAVIGATION_HUD_V2.md), 편집기는 [OFFICE_BUILD_EDITOR_V1.md](OFFICE_BUILD_EDITOR_V1.md)를 따른다.

## 계약과 주식 화면

- 계약 화면은 day-one T0만 보여 주고 실적 조건을 충족할 때 `T1 → T2 → T3 → T4`를 순차 공개한다.
- 계약/제품 route는 기존 실제 회사 registry ID와 읽기 호환 alias를 유지하되 첫날 대기업을 하드코딩하지 않는다.
- 주식 화면은 세로 SIMUL 좌표를 복사하지 않고 가로형 landscape로 투영한다. 화면 호가는 최우선 7매도+7매수이며 내부 depth는 10단계를 유지한다.
- 모든 화면의 돈은 정수 원, 시간은 `GameTime`, 시장/계약 선택은 결정론적 코어의 결과를 표시한다.

## 저장 슬롯

- 현재 전체 저장 스키마는 `v9`이며 `v1`~`v8`을 읽어 이관한다.
- 슬롯에는 company/date/playtime/schema/build 출처처럼 실제 저장에서 읽은 정보만 보여 준다.
- Scene Transform, Sprite 위치, UI route/hover 같은 프레젠테이션 상태는 저장하지 않는다.
- 손상되거나 지원하지 않는 저장은 조용히 덮어쓰지 않고 오류와 복구 선택을 명시한다.

## 검증 기준

- `1920×1080`, `1600×1000`, compact 화면에서 텍스트·버튼·패널이 잘리지 않는다.
- 타이틀→새 게임/이어하기→사무실→각 허브→편집/계약/주식→뒤로 흐름이 끊기지 않는다.
- editor 열기/닫기, 시간 배속, save/load 뒤 semantic 상태가 유지된다.
- missing generated Sprite는 검은 fallback으로 숨기지 않고 명시적 QA 오류로 처리한다.
- 최종 통합 SHA의 상태와 실제 PASS는 [PROJECT_STATE.md](PROJECT_STATE.md)에 기록한다.
