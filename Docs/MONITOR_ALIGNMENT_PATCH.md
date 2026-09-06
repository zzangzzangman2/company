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

## 완료 결과 / 회사에서 이어받기

- 최신 공개판: [fc-win-20260906.3](https://github.com/zzangzzangman2/company/releases/tag/fc-win-20260906.3).
  게임 source `4b06247ea2c4652fc320fa13c141f3501e3b5cae`, Release ID 383586367, sequence 3.
  문서 후속 commit SHA와 게임 빌드 SHA를 혼동하지 않는다.
- 정확한 Release에서 normal navigation 8,104 samples, live settled Working 2,970 samples,
  next-day 네 가족 출근·업무, mute, 충돌/런타임 오류 0을 확인했다. 별도 controlled typing/seat
  264 poses 통과. 실제 Release seated 이미지 8개는 사용자가 승인한 이미지와 해시까지 같다.
- 수정 전/후 actual mesh red→green은 유지한다. 과거 centreline 검사가 CRT의 잘못된 폭 축과
  둥글게 보이는 normal까지 통과시킨 것은 아니었다. 승인된 기존 native 구매 입력은 변경 없는
  transaction 코드에만 연결하며 새 클릭 검사라고 쓰지 않는다. source/digest 바인딩을 증거에 포함했다.
- 실제 public GitHub v2→v3 전송: **6개 / 159,476,005 bytes / 152.1 MiB**, progress 150건,
  실제 수신 100%, unchanged 163개 재사용, 총 169개 원본 SHA-256 검증 통과.
  큰 `resources.assets.resS`/`resources.assets`가 파일 단위로 바뀌므로 작은 PNG 수정도
  이 묶음을 다시 받는다. 모델별 Addressables 분할을 새로 구현한 것은 아니다.
- 실제 배포 worker를 별도 QA root로 실행했다. PrepareOnly→ready까지만 검사했으며, 이번에
  사용자 main에서 Unity 자동 재시작/화면을 직접 보았다고 주장하지 않는다. 해당 코드와 worker는
  v2에서 이미 검사한 그대로이고 로컬 회귀 81/81도 새로 통과했다.
- 사용자 main 169개 파일, v2 cache/current pointer, 세이브/백업 5개 모두 전후 해시가 같다.
  **지금 같은 메인 파일을 직접 열면 게임 내부 로딩의 `패치 중입니다 · 다운로드`에서 실제
  퍼센트와 MiB를 확인할 수 있다.** 다운로드 100% 뒤에는 해시 검증·정상 재시작이 이어진다.
- 회사 플레이: 이미 설치돼 있으면 같은 메인 EXE만 실행. 최초 설치는 [MAIN_GAME_ENTRY.md](MAIN_GAME_ENTRY.md).
  개발을 이어받는 경우 clean main에서 `git pull --ff-only origin main`, AGENTS/PROJECT_STATE부터 읽는다.
  집 작업 경로는 `C:\Users\godho\Documents\Codex\fc_agents\integration_p0`다. 예전 8월 작업 폴더가 아니다.
- [불변 증거와 각 검증의 범위](Evidence/MonitorAlignmentPatch20260906/README.md).
  v2 재사용 공개 assets와 현재 고정 메인을 구버전 정리 대상으로 삭제하지 않는다.
