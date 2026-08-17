# 반복 개발 루프

이 문서는 **한 곳을 고치고 결과를 확인하는 짧은 루프**의 정본이다. 최종 릴리스 빌드와 배포 판정은
[PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)와 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)가 정본이며 이
문서가 그것을 대체하지 않는다. Fast QA 도구 자체의 상세 계약은 [FAST_QA_WINDOWS.md](FAST_QA_WINDOWS.md)에 있다.

## 느린 것은 빌드가 아니라 최초 임포트다

`Builds/Windows/Automation/logs/build-20260815-035423-*.log`(worktree `chair_seat_r5e_clean`, head `57fe6be4`)의 실측:

| 구간 | 실측 |
| --- | ---: |
| 자동화 전체 (`START` → `Unity exited with code 0`) | 133.4초 |
| 그중 `Asset Pipeline Refresh ... InitialRefreshV2(ForceSynchronousImport)` | 103.707초 |
| 나머지 (컴파일 + 플레이어 빌드 + 승격) | 약 30초 |

같은 로그 239행은 `Require frontend run. Library/Bee/1900b0aE.dag couldn't be loaded`를 남겼다. 즉 에셋
임포트뿐 아니라 스크립트 컴파일 그래프까지 캐시 미스였다.

`Library`/`Library/Bee`가 warm일 때의 실측은 [History/FAST_QA_WINDOWS_PIPELINE_2026-08-14.md](History/FAST_QA_WINDOWS_PIPELINE_2026-08-14.md)에 있다.

| 시나리오 | Run 1 | Run 2 | Run 3 |
| --- | ---: | ---: | ---: |
| Cold Editor import 단계만 | 85.005 | 80.421 | 81.487 |
| Normal incremental player build | 6.93 | 6.94 | 7.00 |
| Forced clean release-config player build | 16.00 | 19.58 | 19.44 |
| Pure Simulation Roslyn + 결정론 harness | 0.81 | 0.74 | 0.73 |
| Warm Editor validation 1건 | 8.47 | 8.91 | 8.79 |
| Scripts-only build + D3D11 capture | 22.65 | 15.14 | 15.02 |

**같은 변경이라도 `Library`가 warm이면 7~20초, cold면 100초 이상이다.** 강제 clean 빌드조차 warm
`Library/Bee`에서는 20초 이하다. 그러므로 반복 루프를 느리게 만드는 것은 빌드 옵션이 아니라 warm
`Library`를 버리는 행위다.

## 규칙 1 — worktree를 늘려서 `Library`를 버리지 않는다

worktree를 새로 만들면 그 경로의 `Library`는 비어 있고, 첫 Unity 실행이 80~104초짜리 최초 임포트를
처음부터 다시 낸다. 2026-08-17 정리 전에는 이 비용이 여러 worktree에서 반복됐고, 현재 기능 작업은
`fc_agents/integration_p0`의 warm `Library` 하나만 사용한다.

- 반복 작업은 **`Library`가 이미 warm인 `fc_agents/integration_p0` 한 곳**에서 순차 수행한다.
- `Library`, `Library/Bee`, `Artifacts/FastQa`의 플레이어 캐시는 일상 실행 사이에 삭제하지 않는다.
- 병합이 끝난 worktree는 `git worktree remove`로 정리해 디스크와 혼동을 함께 줄인다.
- 어쩔 수 없이 새 worktree가 필요하면, 그 첫 실행이 100초 이상 걸린다는 사실을 비용으로 인정하고
  시작한다. 그 100초를 빌드 스크립트 탓으로 진단하지 않는다.

## 규칙 2 — 변경 종류에 맞는 명령만 쓴다

기본은 `-Profile auto`다. 바뀐 파일을 보고 가장 싼 경로를 스스로 고른다.

```bat
FAST_QA_WINDOWS.cmd
```

프로필을 직접 지정할 때의 대응은 다음과 같다. 선택 계약의 정본은
`Tools/FastQa/fast-qa-manifest.json`과 [FAST_QA_WINDOWS.md](FAST_QA_WINDOWS.md)다.

| 고친 것 | 명령 | warm SLO |
| --- | --- | ---: |
| `Simulation` `.cs`만 | `FAST_QA_WINDOWS.cmd -Profile simulation-pure` | 15초, Unity 미기동 |
| Editor validation `.cs` | `FAST_QA_WINDOWS.cmd -Profile editor-validation` | 45초 |
| 런타임 `.cs`, 직렬화 레이아웃 불변 | `FAST_QA_WINDOWS.cmd -Profile player-scripts` | 60초 |
| 화면만 다시 확인 | `FAST_QA_WINDOWS.cmd -Profile d3d-capture` | 30초 |
| 실행 자체만 확인 | `FAST_QA_WINDOWS.cmd -Profile player-startup` | 15초 |
| 에셋·프리팹·씬·UI 콘텐츠 | `FAST_QA_WINDOWS.cmd -Profile asset-capture` | 없음, 실제 임포트 시간 |
| asmdef·패키지·ProjectSettings·직렬화 레이아웃 | `FAST_QA_WINDOWS.cmd -Profile editor-broad` | 없음 |

`slo=MISS`는 기능 실패가 아니라 성능 증거다. PASS/FAIL과 혼동하지 않는다.

Fast QA는 `Artifacts/FastQa`에만 쓴다. `Builds/Windows/FamilyCompany_Playtest`와 Downloads 배포본은
절대 건드리지 않으므로 반복 실행이 배포본을 오염시키지 않는다.

## 규칙 3 — `BUILD_WINDOWS.cmd`는 반복 확인용이 아니다

`BUILD_WINDOWS.cmd`는 릴리스 provenance 도구다. 매번 새 staging 폴더를 만들고
(`Tools/Build-FamilyCompanyWindows.ps1`), 전역 build lock을 잡고, 사전 validator와 catalog builder를 모두
돌리고, `BUILD_INFO.txt`와 배포 manifest를 생성한다. 이 비용은 배포 판정에는 필요하지만 한 줄 수정
확인에는 낭비다.

`BUILD_WINDOWS.cmd`와 `DEPLOY_WINDOWS.cmd`는 다음 경우에만 쓴다.

1. 배포 후보 HEAD가 확정되고 clean일 때
2. [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)의 네 가족 09:00/09:01/09:02/09:03 oracle과
   독립 gate를 통과시켜야 할 때

## 규칙 4 — 진단 전에 어느 캐시가 식었는지 먼저 본다

느리다고 느끼면 추측하지 말고 그 실행의 Unity 로그에서 다음 두 줄을 먼저 찾는다.

- `Asset Pipeline Refresh ... Total: <n> seconds` — 이 값이 크면 원인은 cold `Library`다.
- `Require frontend run. Library/Bee/*.dag couldn't be loaded` — 이 줄이 있으면 스크립트 컴파일 그래프도
  cold다.

두 줄이 모두 작거나 없는데도 느리면 그때 빌드 파이프라인을 의심한다.

```bat
FAST_QA_WINDOWS.cmd -Profile diagnose
```

## 아직 적용하지 않은 후보 — Enter Play Mode Options

`ProjectSettings/EditorSettings.asset`의 현재 값은 다음과 같다.

```
m_EnterPlayModeOptionsEnabled: 1
m_EnterPlayModeOptions: 0
```

`m_EnterPlayModeOptions: 0`은 `None`이므로 도메인 리로드와 씬 리로드가 모두 그대로 발생한다. 옵션이
켜져 있을 뿐 현재 얻는 이득은 없다. `3`(`DisableDomainReload | DisableSceneReload`)으로 바꾸면 Editor의
Play 진입이 빨라지지만, 그 전에 다음 런타임 가변 static이 Play 사이에 초기화되지 않는 문제를 먼저
해결해야 한다. 감사 결과 대상은 3개 파일이다.

| 위치 | 대상 |
| --- | --- |
| `Assets/FamilyCompany/Infrastructure.Unity/LiveContentPath.cs:37-38` | `_cachedRoot`, `_rootResolved` |
| `Assets/FamilyCompany/Presentation.Unity/GameAudioCoordinator.cs:39` | `_instance` |
| `Assets/FamilyCompany/Simulation/OfficeInteractions/OfficeInteractionSelectionTrace.cs:100` | `public static event TraceRecorded` |

`event`는 특히 위험하다. 도메인 리로드가 없으면 이전 Play의 구독이 남아 중복 호출된다. 적용하려면 각
지점에 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 리셋을 추가하고
Unity 실행 검증을 통과한 뒤 [DECISIONS.md](DECISIONS.md)와 [PROJECT_STATE.md](PROJECT_STATE.md)에
기록해야 한다. **검증 전까지 이 항목은 후보이며 현재 상태가 아니다.**

[History/FAST_QA_WINDOWS_PIPELINE_2026-08-14.md](History/FAST_QA_WINDOWS_PIPELINE_2026-08-14.md)의
"Do not use no-domain-reload claims for batch startup"은 배치 기동에 대한 결정이며, 위 항목은 대화형
Editor Play 진입에 대한 별개 후보다.

## 아직 적용하지 않은 후보 — Unity Accelerator

`ProjectSettings/EditorSettings.asset`의 `m_CacheServerMode: 0`은 프로젝트 차원에서 Cache Server를 켜지
않았다는 뜻이다. Accelerator를 켜면 임포트 산출물이 여러 worktree 사이에서 공유되어 규칙 1의 최초
임포트 비용이 줄어든다. 다만 별도 설치가 필요하고 이 저장소에 개인 절대 경로나 엔드포인트를 커밋하지
않는다는 기존 규칙을 지켜야 하므로, 도입 시 엔드포인트는 환경 변수나 로컬 설정으로 다룬다. 아직
설치·측정하지 않았으므로 후보다.
