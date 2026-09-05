# 2026-09-05 최신 충돌·가구 회피·타일 중심 산책 검증

정본 작업 경로: `C:/Users/godho/Documents/Codex/fc_agents/integration_p0`, `main`.
**개발 확인용 PASS. 공개 Release/Downloads 배포·최종 사용자 화면 승인·PC 종료는 미완료.**
원본 모델, 크기, 색, 보행 clip, V31 책상과 기존 의자, 좌석 socket은 바꾸지 않았다.
엄마·누나는 여전히 아빠·아들 외형을 각각 별도로 표시하는 임시 3D 배우다.

## 화면과 실측

- `tile-centres-overview.mp4`: 최신 실행본의 정상 새 게임 23.989초, 모든 367프레임.
- `four-actors-closeup.mp4`: 같은 시각의 네 배우 추적 확대. 두 영상 모두 실측 시간 유지.
  노랑=실제 타일 경계, 청록=이동 root, 분홍=바닥으로 투영한 양발 뼈 중점.
  19개 연속 sheet 전체를 순서대로 확인했다. 원본은 약 15.3fps이며 30fps MP4는 프레임 중복이다.
  영상 캡처 성능이나 신발 바닥 미끄러짐의 독립 검증 완료를 뜻하지 않는다.
- `analysis.json`, `walk-trace.csv`, `projection.csv`: 최대 중심선 오차 **0.000189px**.
  양발 뼈 중점 median/max(px): 아들 1.96/4.14, 누나 대역 1.95/4.30,
  아빠 1.66/3.32, 엄마 대역 1.58/3.29. 강제 경로/순간이동/고정 캡처 clock 0.
  보관된 원래 경로와의 거리 46.54px는 양보용 후퇴 구간이 원래 경로 밖이라는 뜻이다.
  실제 타일 중심선 이탈 수치가 아니므로 두 지표를 혼동하지 않는다.
- `normal-wander.csv`, `opening-shop-final.txt`: 별도 정상 60초 관측. 네 명 모두 약 50~54 world
  units 이동. 최대 연속 무진행은 아빠 1.9206초 / 엄마 대역 0.0890초 / 누나 대역 0.9011초 /
  아들 0.0889초. 관통 위반 0/0/0, 런타임 오류 0. 8초 이상 멈추면 실패하는 검사다.
- 4방향 세트 구매 40만 원씩, 500만→340만 원, 실제 3칸 점유, 겹침 배치 미차감 PASS.
  실제 controller 확인 호출이며 **native pointer 클릭/IMGUI 화면 검사가 아니다**.
- `player-father-3d-interaction-result.txt`: 정면 외곽 겹침 **0px**, 책상 우회 134프레임 중
  샘플링된 몸 vertex의 책상 부품 관통 **0/0프레임**, 둘 다 우회 목적지 도달과 각자 착석/업무.
  좌석 중심 오차 0/0. 무릎 아들 106.34/110.43°, 아빠 134.04/140.28°.
  가구 우회와 업무 이동은 fixture 목적지를 사용하므로 정상 coordinator 산책과 구분한다.
- `player-father-avoidance.png`, `player-father-working.png`: 실제 D3D11 캡처 원본.
  마지막 업무 화면의 추가 대역은 네 배우를 다시 생성하는 배치 리빌드 뒤의 정상 표시다.

## 원인과 수정

1. 발 중심 보정 후 기존 사람 충돌 반경이 좁아 84픽셀이 겹쳤다. 기본 아들/아빠 반경을
   0.445/0.415로 보정했다. 외형 스케일 변화가 아니다.
2. 몸 collider는 책상을 피해도 팔 mesh가 들어갔다. 기본 가구 패딩 0.18(합계 0.40)과
   기존 책상 인접 칸 비용 +2.5를 적용했다. 실제 구매 점유칸/자기 좌석 접근 예외는 유지한다.
3. 두 배우가 서로의 앞칸을 예약하면 50초 이상 멈췄다. 0속도 양보도 대기 시간에 포함하고,
   실제 예약 blocker를 고려해 낮은 우선순위 배우만 중심선 위 안전한 칸으로 물러난 뒤 재계산한다.
4. 기존 self-PASS가 가구 관통/긴 멈춤을 놓쳤다. 외부 pair 실행기는 관통 계측이 누락되거나
   0이 아니면 실패하며, 정상 산책은 8초 무진행 gate를 추가했다.

## 출처와 정리

Unity `6000.3.21f1`, dirty development base `b09e0451`; 소스 수정 파일은 이 증거와 함께 커밋한다.
확인용 build run `20260905-230213-598`: **22.814초 PASS**. 빌드 기록은
`development-build-result.json`; 코드/PrototypeValidation은 `editor-validation-result.json`, **14.720초 PASS**.
60초 상점 run `20260905-230327`, pair run `20260905-230502`, walk run `20260905-230848`는 같은 실행본이다.

**중요:** legacy pair 결과 파일의 `releasePlayer=true`와 `productionEligible=True`는 오래된 fixture
출력이며 승인서가 아니다. `external-runner.json`이 실제 FastQA 경로와 EXE/게임 DLL hash,
`releasePlayer=false`, `productionEligible=false`, `independentReleaseGate=false`를 명시한다.

사용자 `너가해` 재승인으로 원래 막혔던 exact cleanup을 수행했다. 이후 발견된 가구 관통과 교착
실패 identity도 각각 166개 파일을 전부 해시 확인하고 휴지통으로 옮겼다. 각 exact root가 없어진
것을 확인한 후 새 개발 빌드를 만들었다. `failed-payload-cleanups.json`에 네 기록이 있다.
원본 소스, Library/Bee, saves, sibling cache, 기존 untracked 누나 입력은 보존했다.
휴지통 복구는 가능하지만 실패한 실행본을 복원해 플레이하거나 배포해서는 안 된다.

## 이어서 할 일

현재 영상의 사용자 확인, 전체 4방향 착석/업무, 실제 상점 pointer 입력과 다음날 09:00~09:03
독립 출근, foot-slip/grounding/mute 검증이 남아 있다. 검사 스크립트는 아래와 같다.

```powershell
./Tools/Invoke-FamilyCompanyOpeningShopQa.ps1
./Tools/Invoke-FamilyCompanyPlayerFatherInteractionQa.ps1
./Tools/Invoke-FamilyCompanyOpeningWalkAudit.ps1
```

배포는 `Docs/GITHUB_PATCHING.md`의 clean committed Release/독립 receipt/명시적 사용자 승인 gate를
통과한 뒤 진행한다. 기존 updater의 로컬 36개 검사는 PASS지만 실제 GitHub 게임 Release는 아직 없다.
Source push를 게임 다운로드 배포 완료라고 말하지 않는다. 모든 요청 완료 전 PC 종료를 실행하지 않는다.
