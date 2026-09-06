# CRT·키보드 책상 정렬 패치

2026-09-06 요청: 네 방향 CRT가 책상에 비해 휘고 기울어 보이는 부분만 고친다.
사용자는 완료 후 기존 메인 실행파일을 직접 열어 업데이트를 확인할 예정이다.
수정된 네 방향 sheet를 표시한 뒤 사용자 "1" / "이 모습으로 배포" 승인을 받았다.

## 수정 경계

`Family3DWorkstation`은 두 표현 경로를 공유한다. sprite authoring은 orthogonal X/Z와 30도 카메라를
쓰고, 실제 캐릭터와 함께 그리는 runtime은 이미 승인된 45도 overlay 카메라에 tile 좌표를 투영한다.
후자의 desk axes는 mapped coordinates다. 이때 CRT/keyboard만 폭 축을 별도로 직교화하면 나머지
책상과 19.4712도 달라진다. 독립적으로 계산한 직각 normal과 타일-facing vector도 같은 개념이 아니다.

이번 수정은 CRT/keyboard/details에만 따로 적용하던 축 예외를 제거한다. 책상과 같은 투영을 쓰되
canonical 물리 authoring의 90도 회전은 유지한다. 모델 치수/중심/높이/재질/카메라는 바꾸지 않는다.
공유된 8 corner normal로 CRT 면 전체를 둥글게 만들던 shading은 CRT 면 정점을 분리해서 수정한다.
의자와 다른 가구의 shading은 그대로다. 새 3D 모델이나 유료 생성은 없다.

보존: 책상·의자 3칸 footprint, 4방향 rotation/preview/점유, 의자 타일 중앙, 캐릭터/양팔/다리/착석,
손목 목표, 보행/충돌/금액/세이브, 패치 worker. 상점의 네 desk PNG는 같은 수정으로 rebake하며
chair PNG 네 장과 importer 설정은 바꾸지 않는다.

## 독립 검증

- `WorkstationTileCentreRegression.RunBatch`: orthogonal authoring + mapped overlay 각각 네 방향.
  CRT/keyboard actual mesh edges vs actual desk edges, screen front vs desk-front plane, CRT triangle
  lighting normals, chair/stem tile centre, keyboard/screen centreline. 위치 gate 0.0001, 각도 gate 0.1도.
  수정 전 axis 19.4712도 / CRT normal 77.0653도 FAIL; 수정 후 ≤0.028도 / 0도 PASS.
- 원본 red/green: `Artifacts/MonitorAlignment20260906/{red,green}/geometry.json`.
- FastQA actual render와 264 typing/seat poses PASS, 근거 `Artifacts/MonitorAlignment20260906/fast-chair`.
  pose injection 검사이며 실제 normal 출근/보행 검증과 혼동하지 않는다.
- 사용자의 수정 외형 승인, exact clean Release, 실제 normal 네 가족 gate 이후만 공개한다.
  과거 native purchase 입력 증거를 새 클릭으로 다시 명명하지 않는다.

## 패치 확인 계약

고정 메인은 `%USERPROFILE%\Downloads\FamilyCompany_Playtest\FamilyCompany.exe` 그대로다.
소스 commit/push만으로 배포됐다고 하지 않는다. 검증된 새 Release를 이전 manifest 기반 delta로 공개한다.
이전 `.2`의 재사용 asset은 새 manifest가 참조하므로 임의 삭제하지 않는다. 해당 최초 메인은 패치 진입점이며
이전 게임으로 우회하는 fallback이 아니다. 저장은 별도 보존한다.

사용자가 직접 수신 화면을 확인할 수 있도록 **사용자의 AppData snapshot/current pointer를 미리
업데이트하지 않는다**. 독립 patch 시험은 별도 QA 설치 root에서 수행하고 실제 production 서버/바이트
수신 여부와 Unity 재시작 시험 여부를 구분한다. 최종 공개 version·SHA·수신량은 PROJECT_STATE에 기록한다.
