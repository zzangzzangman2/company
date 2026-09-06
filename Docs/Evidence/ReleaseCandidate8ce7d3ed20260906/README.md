# 8ce7d3ed 실제 Release 검증 / 2026-09-06

검증한 게임 소스: **8ce7d3edee4b53477739e2a0bd848cc51c6b1e46**. 이 기록을 저장하는 후속 문서 commit과
게임 binary identity는 구분한다. Unity 6000.3.21f1 / c02631ffc030, clean main, non-Development Release.
[BUILD_INFO](BUILD_INFO.txt)의 input fingerprint와 날짜를 보존했다. 검증 후보는 아직 공개 게임이 아니다.

**productionEligible=false / 현재 시각 승인 대기 / public Release 없음 / 기존 메인과 세이브 변경 없음.**

## 현재 화면 검토 자료

- [네 사람 실제 속도 보행 영상](four-actors-closeup.mp4): 23.967초, 427개 실게임 프레임, 속도 보정 없음.
- [타일 중심선 표시 전체 영상](tile-centres-overview.mp4): 같은 실시간 캡처, 경로/좌표 분석 표시만 추가.
- [아들·아빠 각각 네 방향 착석](seated-four-directions-review.png): 8개 실제 캡처를 최근접 2배 확대.
  열 SE/SW/NW/NE는 **책상 방향**이며 캐릭터가 모니터를 보는 방향은 반대다.
- [실제 클릭으로 구매한 네 세트](native-four-sets.png), [겹침 거부 화면](native-overlap-rejected.png).

새 2D 자산이나 캐릭터 외형을 생성하지 않았다. 임시 엄마/누나는 요청대로 아들/아빠 3D body를 사용한다.
네 사람의 시뮬레이션 ID, 충돌, 좌석, 출근 시각은 각각 독립적이다.

## 독립 결과

| 검사 | 실제 결과와 범위 |
|---|---|
| 구매·네 방향 | 실제 Windows pointer로 4회 성공, 각 40만 원, 500만 → 340만 원 |
| 겹침 | 다섯 번째 시도 거부, 추가 결제/가구 없음; pointerCommits는 성공 횟수가 아닌 시도 횟수 |
| 타일 정렬 | 의자 중심 최대 0.484406 px, 기둥 중심 0.484436 px, 모니터/키보드 축 0도 |
| UI | 실제 2560×1440 게임의 이름·가격·잔액·도움말·버튼에 겹침/잘림 없음 |
| 정상 이동 | 8,100개 표본, 최대 rail fractional error 0.00002625, 물체/인물 관통 0 |
| 다음날 | 등장 Player 09:00, 누나 09:01, 아빠 09:02, 엄마 09:03; 09:04 진행 gate 통과 |
| 정상 업무 | 첫 Working 각각 09:09 / 09:06 / 09:17 / 09:19, 변경하지 않은 09:20 전 마감 통과 |
| 실제 착석 | 안정된 Working 3,229개, 손 오류 0, 최대 개별 손 거리 0.008899 world |
| 착석 fixture | 두 body × 네 방향 × 33 typing pose = 264개, 무릎 80–140도, 피부/의자 관통 0 |
| 보행 | 427개 전 프레임을 22개 contact sheet로 검토, 여러 주기·회전에서 앞발 교대 관찰 |
| 보행 중심 | 네 사람 모두 ground-projected foot midpoint median ≤4 px / max ≤8 px 통과 |
| 소리 | normal 검사 listener mute 상태 출력 0, native 검사 소리를 켠 상태 출력은 실제로 존재 |
| 오류/종료 | runtime 오류 0; normal/walk/chair 게임 exit 0, 강제 종료 없음 |
| 패치 회귀 | updater 51/51, latest-only 실패 차단 6/6, restart worker 10/10 |

[native-pointer.json](native-pointer.json), [runtime-summary.json](runtime-summary.json),
[chair-fit.csv](chair-fit.csv), [walk-analysis.json](walk-analysis.json)에서 수치와 검증 범위를 확인한다.
updater JSON 세 개는 로컬 inert fixture 결과이며 실제 GitHub 게임 다운로드의 증거가 아니다.
[evidence-inventory.json](evidence-inventory.json)은 수집 당시 개별 증거의 SHA-256/크기를 기록한다.

## 과장하지 않는 검증 경계

- normal 런은 route/pose 강제 주입·teleport 없이 실제 자율 intent/이동/업무를 관찰했다. 다만 네 세트
  설치는 진단 준비 단계에서 수행했으며 네이티브 구매 UI 검사는 별도 run으로 검증했다.
- 착석 264개 fixture는 pose를 주입한다. 이를 정상 자율 업무 PASS로 대신하지 않으며 normal run의
  live hand trace 3,229개가 별도다. 전환 중 손 오차는 숨기지 않고 원본에 보존했다.
- 발 midpoint/뼈 좌표는 수학적으로 피부 미끄러짐 0을 증명하지 않는다. lowest skin Y는 여섯 프레임마다
  계측했다. 전 프레임 육안 검사에서 반복 발 교대·무릎 굽힘·회전은 확인했으나 현재 외형 승인과
  최종 보행/접지 수용 판단을 자동 수치로 대체하지 않는다.
- 영상은 무음이다. mute 검사는 실제 렌더링 오디오 출력 관측이며 설정 UI/저장 지속성 검사는 아니다.
- 실제 클릭 예외 검사 이외에는 별도 Windows desktop에서 실행했으며 사용자 desktop을 전환하지
  않았다. 클릭 검사용 게임도 끝나고 정상 종료했다. 사용자의 다른 앱/메인/세이브는 변경하지 않았다.
- 이전 로컬 in-game 패치 화면의 실제 20.3% / 0.81 of 4.00 MiB 증거는 UI 기능에 한정한다. 해당
  과거 payload의 gameplay 실패는 그대로 보존되고 전체 실행물은 휴지통으로 정리됐다. 그 화면을
  현재 candidate의 GitHub end-to-end 다운로드 성공이라고 보고하지 않는다.

## 원본 위치와 재현

정본 작업 폴더: `C:\Users\godho\Documents\Codex\fc_agents\integration_p0`.

- candidate: `Artifacts/PatchCandidates/8ce7d3ed-5912b1b0536541e5b9e6a4694cbc2eb4/payload`
- normal 원본: `Artifacts/NormalAutonomy/8ce7d3ed-release-trace-only`
- native 원본: `Artifacts/NativePointer/8ce7d3ed-release`
- walk 모든 frame/trace/sheet: `Artifacts/ReleaseGameplay/8ce7d3ed-walk`
- chair 원본: `Artifacts/ReleaseGameplay/8ce7d3ed-chair`

위 Artifacts는 로컬 원본이며 Git에는 이 폴더의 선별된 비실행 증거만 저장한다. 회사에서 pull해도
로컬 Artifacts가 자동으로 생기지는 않는다. 바이너리·Unity 캐시·세이브는 source Git에 넣지 않는다.

normal 재현 명령(후보 경로를 먼저 실제 identity로 확인):

```powershell
pwsh -NoProfile -File Tools/Invoke-FamilyCompanyNormalAutonomyQa.ps1 `
  -NextDay -Player '<검증할 Release payload>\FamilyCompany.exe' `
  -EvidenceDirectory '<새 Artifacts 증거 폴더>'
```

사용자 화면에서 다시 클릭하지 않는다. 현재 허용된 마지막 클릭 검사는 이미 완료됐다.
최종 clean HEAD의 Release 후보 생성은 `Tools/Updater/Build-FamilyCompanyPatchCandidate.ps1
-ExpectedHead <full SHA>`를 사용하며 바로 Downloads로 옮기는 구형 배포 entry를 사용하지 않는다.

## 회사에서 이어갈 남은 일

1. 현재 영상/착석 sheet의 실제 사용자 승인 내용을 먼저 확인한다. 질문을 보낸 것 자체는 승인이 아니다.
2. 최종 소스 HEAD·BUILD_INFO·독립 receipt·개별 hash를 맞춘다. 이 문서-only commit 때문에 HEAD가
   달라지면 final identity로 build/검증한다. BUILD_INFO 재기록, 예전 PASS의 commit 갈아끼우기,
   main reset, publisher gate 완화는 금지한다.
3. 승인과 필수 gate를 충족한 경우에만 `Publish-FamilyCompanyPatch.ps1`로 첫 정식 Release를 만들고
   asset digest/원격 inventory를 확인한다. 공개되지 않은 draft를 성공이라고 보고하지 않는다.
4. 검증된 전체 ZIP을 고정 메인 폴더에 최초 한 번 설치한다. 옛 폴더 identity를 확인하고 복구 가능한
   방식으로 보존하며 세이브를 건드리지 않는다. 이후 실제 GitHub 최신 확인·바이트 진행률·검증·
   재시작을 확인한다. 현재는 이 단계들이 **아직 실행되지 않았다**.

고정 실행 위치와 회사 설치 계약은 [MAIN_GAME_ENTRY.md](../../MAIN_GAME_ENTRY.md)가 정본이다.
`%USERPROFILE%\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`만 메인 진입점이다.
플레이할 때 빌드하지 않는다. 개발 소스 컴파일과 최초 검증 패키지 설치가 없어지는 것은 아니다.
