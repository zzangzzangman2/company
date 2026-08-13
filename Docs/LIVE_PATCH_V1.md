# LIVE PATCH V1 — exe를 매번 빌드하지 않고 실시간으로 보기

작성: 2026-08-10 / Claude
성격: **제안서 + 실행 방법**. Codex가 검토 후 정본에 합칠 항목을 고른다.

---

## 0. 결론부터

**"온라인게임처럼 자동 패치"는 절반만 가능하다.** Unity의 벽이 하나 있다.

| 바꾸는 것 | 리빌드 필요? | 실시간 가능? |
| --- | --- | --- |
| JSON 데이터 (History, 계약 카탈로그, 밸런스 수치) | **아니오** | **예 — 저장하고 F5** |
| 이미지·오디오 | 아니오 (Addressables 쓰면) | 예 |
| 씬 배치·프리팹 | 아니오 (Addressables 쓰면) | 예 |
| **C# 코드** | **예. 예외 없음** | 아니오 |

C# 스크립트는 플레이어에 컴파일되어 들어가므로 exe를 다시 만들지 않으면 절대 안 바뀐다.
IL2CPP는 물론이고 Mono 백엔드도 공식 지원이 없다. 이건 우회로가 없는 벽이다.

**그런데 이 프로젝트는 데이터 비중이 압도적으로 높다.** History JSON 7개 파일, 82개 회사,
계약 카탈로그 21종, 업종 4종, 밸런스 상수. 실제로 고치는 것의 대부분이 코드가 아니라 데이터다.
그래서 **데이터만 빌드 밖으로 빼면 리빌드 횟수가 크게 줄어든다.** 이게 1순위다.

---

## 1. 지금 있는 것과 빠진 것

`Tools/`를 보면 자동 빌드는 이미 만들어져 있다.

| 스크립트 | 하는 일 |
| --- | --- |
| `Watch-FamilyCompanyBuild.ps1` | 3초마다 프로젝트 지문 확인 → 12초 디바운스 → Unity 배치 빌드 |
| `Start-/Stop-FamilyCompanyBuildWatch.ps1` | 워처를 백그라운드로 켜고 끄기 |
| `Build-FamilyCompanyWindows.ps1` | 1회 빌드 |

산출물은 `C:\Users\godho\Downloads\FamilyCompany_Playtest`로 나간다.

**빠진 것 3개:**

1. **데이터가 빌드에 구워져 있다** — JSON 한 글자만 고쳐도 전체 리빌드
2. **빌드가 끝나도 게임이 모른다** — 사람이 직접 끄고 다시 켜야 함
3. **재시작하면 진행 상황이 날아간다** — 확인하던 지점으로 다시 가야 함

아래 3계층이 그 셋을 각각 메운다.

---

## 2. 계층 A — 데이터는 리빌드 0회 ★구현 완료 (2026-08-11)

### 원리

프로젝트의 콘텐츠 폴더를 플레이테스트 빌드 **옆에** 디렉터리 정션으로 연결한다.
게임은 시작할 때 그 폴더가 있으면 먼저 읽고, 없으면 빌드에 내장된 `TextAsset`을 쓴다.

```
Downloads\
├─ FamilyCompany_Playtest\          ← 빌드가 매번 통째로 교체
│   ├─ FamilyCompany.exe
│   └─ FamilyCompany_Data\
└─ FamilyCompany_LiveData\  ──────→ (정션) Assets\FamilyCompany\Content\
                                              └─ History\*.json
```

**링크를 빌드 출력 폴더 안이 아니라 옆에 두는 이유**: `Build-FamilyCompanyWindows.ps1`은
승격 단계에서 `Move-Item`으로 최종 출력 폴더를 통째로 갈아치운다. 안에 두면 빌드마다 사라진다.

정션을 쓰는 이유는 로컬 드라이브에서 **관리자 권한 없이** 만들 수 있기 때문이다.
심볼릭 링크는 관리자 권한이나 개발자 모드가 필요하다.

### 링크 걸기 — 한 번만

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Link-LiveContent.ps1
```

멱등하다. 이미 올바르게 걸려 있으면 그대로 두고, 다른 곳을 가리키면 다시 건다.
같은 자리에 실제 폴더가 있으면 **덮어쓰지 않고 멈춘다**. 제거는 `-Remove`.

### 구현된 파일

| 파일 | 역할 |
| --- | --- |
| `Assets/FamilyCompany/Infrastructure.Unity/LiveContentPath.cs` | 외부 콘텐츠 폴더 해석과 안전한 읽기 |
| `Assets/FamilyCompany/Infrastructure.Unity/KoreaHistoryV1RuntimeCatalog.cs` | 외부 파일 우선, 실패 시 내장 `TextAsset` |
| `Assets/FamilyCompany/Presentation.Unity/LiveContentReloader.cs` | F5 핫키. 씬 배치 없이 스스로 붙는다 |
| `Tools/Link-LiveContent.ps1` | 정션 생성·확인·제거 |

폴더 탐색 순서는 이렇다. 셋 다 없으면 기능이 꺼지고 이후 디스크를 읽지 않는다.

1. 환경 변수 `FAMILYCOMPANY_LIVE_CONTENT`가 가리키는 폴더
2. exe 폴더 안의 `LiveData`
3. exe 폴더의 부모에 있는 `FamilyCompany_LiveData` ← 기본값

세 후보 모두 안에 `History` 하위 폴더가 있어야 인정한다. 우연히 같은 이름의 폴더가 있어도 켜지지 않는다.

> **구현 중 발견 (2026-08-11)**: 플레이테스트 빌드는 의도적으로 **비-Development**다.
> `WindowsPlayerBuild.cs`가 `BuildOptions.Development`가 켜져 있으면 예외를 던진다.
> 따라서 `DEVELOPMENT_BUILD` 심볼로 감싼 코드는 **exe에서 아예 컴파일되지 않는다.**
>
> 그래서 컴파일 심볼 대신 **폴더 존재 자체를 opt-in 신호로** 쓴다.
> 배포 패키지에 그 폴더를 넣지 않으면 기능은 꺼진 채로 남는다.
> 더 엄격한 차단이 필요해지면 `WindowsPlayerBuild.cs`의 비-Development 원칙부터 검토해야 한다.

### 쓰는 법

1. JSON을 고치고 저장한다
2. 게임 화면에서 **F5**
3. 좌측 알림에 `콘텐츠 다시 읽음 · 회사 83행 · 외부 파일`이 뜬다

파싱에 실패하면 **이전 데이터를 그대로 두고** 실패 사유만 알린다.
실행 중에 데이터가 비는 것보다 옛 데이터가 남는 편이 안전하다.

### 어디까지 데이터로 뺄 수 있나

지금 C# 상수로 박혀 있는 것들도 JSON으로 빼면 같은 방식으로 실시간이 된다.

- `SmallTeamContractPolicy`의 상한값 (동시 2건, 80인시, 250만원, 주 16시간)
- `BootstrapContractCatalog`의 21종 하청 의뢰
- `ResearchTechnologyCatalog`의 R&D 3종 비용
- `BusinessIndustryCatalog`의 업종 4종

**밸런스 조정은 거의 전부 이쪽이다.** 여기를 빼는 게 실시간 확인의 체감을 가장 크게 바꾼다.

---

## 3. 계층 B — 새 빌드가 나오면 게임이 스스로 알린다

C#을 고쳤을 땐 리빌드가 불가피하다. 대신 **사람이 확인하고 조작하는 부분을 없앤다.**

### 1) 빌드가 스탬프를 남긴다

`Build-FamilyCompanyWindows.ps1` 마지막에 한 줄 추가.

```powershell
$stamp = [pscustomobject]@{
    builtUtc    = [DateTime]::UtcNow.ToString('o')
    fingerprint = $snapshot.Fingerprint
}
$stampPath = Join-Path $FinalOutputPath 'build-stamp.json'
$stamp | ConvertTo-Json -Compress |
    Out-File -LiteralPath $stampPath -Encoding utf8 -NoNewline
```

`build-stamp.json`도 `_Data` 밖이라 빌드가 덮어써도 매번 새로 쓰이면 된다.

### 2) 게임이 스탬프를 감시한다

```csharp
// Assets/FamilyCompany/Presentation.Unity/LivePatchWatcher.cs
#if DEVELOPMENT_BUILD
using System.Diagnostics;
using System.IO;
using UnityEngine;

public sealed class LivePatchWatcher : MonoBehaviour
{
    const float CheckIntervalSeconds = 5f;

    string _stampPath;
    string _bootStamp;
    float _nextCheck;

    public bool NewBuildAvailable { get; private set; }

    void Start()
    {
        _stampPath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "build-stamp.json");
        _bootStamp = ReadStamp();
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + CheckIntervalSeconds;

        var current = ReadStamp();
        if (!string.IsNullOrEmpty(current) && current != _bootStamp)
        {
            NewBuildAvailable = true;
        }

        if (NewBuildAvailable && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
        {
            RestartIntoNewBuild();
        }
    }

    string ReadStamp()
    {
        try { return File.Exists(_stampPath) ? File.ReadAllText(_stampPath) : null; }
        catch { return null; }   // 빌드가 쓰는 중이면 다음 주기에 다시 본다
    }

    void RestartIntoNewBuild()
    {
        // 자기 자신을 바로 다시 실행하면 파일이 잠겨 있다.
        // PowerShell에 잠깐 기다렸다 띄우게 맡기고 자기는 종료한다.
        var exe = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            Application.productName + ".exe");
        var args = $"-NoProfile -WindowStyle Hidden -Command " +
                   $"\"Start-Sleep -Milliseconds 900; Start-Process '{exe}'\"";
        Process.Start(new ProcessStartInfo("powershell.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Application.Quit();
    }
}
#endif
```

### 3) 화면 구석에 배너

`NewBuildAvailable`이 켜지면 HUD 우상단에 이렇게만 띄운다.

```
새 빌드 준비됨 · Ctrl+R
```

**플레이 중에 강제로 껐다 켜지 않는다.** 재시작 시점은 플레이어가 고른다.
`Time.timeScale = 0`인 메뉴 중에도 배너는 보여야 하므로 `Time.unscaledTime`을 쓴다.

---

## 4. 계층 C — 재시작해도 보던 자리로 돌아온다

재시작이 안 아프려면 **상태가 살아남아야** 한다. 저장 슬롯이 이미 3개 있으니 하나를 더 쓴다.

- 재시작 직전에 `__livepatch` 전용 슬롯으로 자동 저장 (기존 3슬롯을 건드리지 않는다)
- 다음 실행에서 개발 빌드이고 `__livepatch` 슬롯이 있으면 **타이틀을 건너뛰고 바로 이어하기**
- 이어받은 뒤 그 슬롯은 지운다 (한 번만 쓰는 왕복 티켓)

```csharp
const string LivePatchSlot = "__livepatch";

// 재시작 전
_saveRepository.Save(LivePatchSlot, _gameState);

// 다음 부팅 시
#if DEVELOPMENT_BUILD
if (_saveRepository.Exists(LivePatchSlot))
{
    var state = _saveRepository.Load(LivePatchSlot);
    _saveRepository.Delete(LivePatchSlot);
    EnterGameDirectly(state);   // 타이틀 건너뜀
    return;
}
#endif
```

여기까지 하면 흐름이 이렇게 된다.

```
C# 수정 → (워처가 알아서 빌드, 1~3분) → 게임 구석에 배너
      → Ctrl+R → 자동 저장 → 새 exe → 보던 자리에서 계속
```

**사람이 하는 조작은 Ctrl+R 하나뿐이다.**

---

## 5. 빌드 시간 줄이기

계층 B가 아무리 매끄러워도 빌드가 5분이면 소용없다. 설정으로 크게 줄일 수 있다.

| 설정 | 값 | 이유 |
| --- | --- | --- |
| Scripting Backend | **Mono** | IL2CPP는 C++ 변환·컴파일이 붙어 몇 배 느리다. 플레이테스트용이면 Mono |
| Development Build | 켬 | `DEVELOPMENT_BUILD` 심볼이 있어야 위 코드가 산다 |
| Script Debugging | **끔** | 안 붙일 거면 끄는 게 빠르다 |
| Compression Method | **없음 또는 LZ4** | LZ4HC는 압축에 시간을 많이 쓴다 |
| 출력 폴더 | **지우지 않는다** | Unity는 증분 빌드를 한다. 매번 지우면 전부 다시 만든다 |
| Managed Stripping Level | **Disabled** | 스트리핑은 시간을 먹고 개발 중엔 이득이 없다 |

마지막 항목이 특히 중요하다. 워처가 빌드 전에 출력 폴더를 비우고 있다면 **그것부터 없애야 한다.**
증분 빌드가 살아 있으면 C# 한 줄 수정은 보통 30초~1분대로 떨어진다.

---

## 6. 그런데 — C#을 자주 고칠 거면 Editor가 여전히 더 빠르다

정직하게 말할 부분이다.

| 방식 | C# 한 줄 수정 → 화면에서 확인 |
| --- | --- |
| Editor Play (도메인 리로드 끔) | **3~10초** |
| exe 자동 빌드 + Ctrl+R | 1~3분 |

"Unity 켜고 접속하는 게 번거롭다"는 건 대부분 **Play 진입이 느려서**다. 두 가지로 해결된다.

### 1) 도메인 리로드 끄기

`Edit → Project Settings → Editor → Enter Play Mode Settings`

- `Enter Play Mode Options` 체크
- `Reload Domain` 해제
- `Reload Scene` 해제

Play 진입이 십수 초에서 **1초 이내로** 떨어진다.

> **주의**: static 필드가 Play 사이에 초기화되지 않는다.
> 이 프로젝트는 `StableRandom`·카탈로그 같은 static이 있으므로,
> `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`로
> static 상태를 명시적으로 리셋하는 코드를 함께 넣어야 안전하다.
> 이걸 안 하면 두 번째 Play부터 결정론이 깨져서 **검증이 조용히 틀린 값을 통과시킨다.**

### 2) 바로가기 하나로 Unity + 씬 + Play까지

바탕화면에 `.bat` 하나:

```bat
@echo off
start "" "C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe" ^
  -projectPath "C:\Users\godho\Documents\Codex\family_company_unity" ^
  -openfile "Assets\FamilyCompany\Scenes\Prototype01.unity"
```

Unity를 한 번 켜두고 **닫지 않으면** 이후로는 Play 버튼만 누르면 된다.

### 정리

- **C# 반복 작업** → Editor. exe 자동패치로는 못 이긴다
- **데이터·밸런스 반복 작업** → 계층 A. exe에서 F5, Editor보다도 빠르다
- **긴 플레이테스트·다른 사람에게 보여주기** → exe + 계층 B·C

---

## 7. 계층 D — 아트·씬까지 패치하려면 (선택)

이미지·프리팹·씬까지 리빌드 없이 바꾸려면 **Addressables**를 쓴다.

- Addressable 그룹의 Build & Load Path를 로컬 폴더로 지정
- 빌드 산출물 옆 `Patch\` 폴더에 번들과 카탈로그를 둔다
- 게임은 시작할 때 카탈로그를 확인하고 바뀐 번들만 다시 읽는다
- 로컬이면 HTTP 서버도 필요 없다. 폴더 경로면 된다

**언제 할 가치가 있나**: 사무실 도트·캐릭터·UI 배치를 하루에 여러 번 갈아끼우기 시작하면.
지금은 아트 반복이 그 정도로 잦지 않으므로 **미룬다.** 계층 A·B가 먼저다.

### 유료 대안 하나

`Hot Reload` (에셋스토어) 는 개발 빌드에서 **메서드 본문 수준의 C# 핫리로드**를 지원한다.
새 필드·새 타입·시그니처 변경은 안 되고, 값 조정이나 로직 한 줄 수정 정도가 대상이다.
유료이고 제약이 뚜렷하지만, C# 상수 만지는 게 잦다면 값어치가 있을 수 있다.
다만 **계층 A로 그 상수들을 JSON으로 빼면 공짜로 같은 효과**가 나므로, A를 먼저 해보고 판단한다.

---

## 8. 권장 순서

| 순위 | 항목 | 크기 | 효과 |
| --- | --- | ---: | --- |
| 1 | **LiveData 심링크 + 로더 우선 경로 + F5** | 반나절 | JSON·밸런스 수정이 **리빌드 0회**가 된다 |
| 2 | **빌드 스탬프 + 배너 + Ctrl+R 재시작** | 2~3시간 | C# 수정 후 사람이 할 일이 키 하나로 준다 |
| 3 | **`__livepatch` 슬롯 자동 왕복** | 1~2시간 | 재시작이 안 아파진다 |
| 4 | **빌드 설정 최적화 + 출력 폴더 보존** | 30분 | 리빌드 1~3분 → 30초~1분 |
| 5 | **Editor 도메인 리로드 끄기 + static 리셋** | 1시간 | C# 반복은 결국 여기가 제일 빠르다 |
| 6 | 계약·R&D·업종 상수를 JSON으로 이관 | 하루 | 1번의 효과 범위가 밸런스 전체로 넓어진다 |
| 7 | Addressables | 여러 날 | 아트 반복이 잦아지면 |

**1번만 해도 체감의 절반 이상이 온다.** 심링크는 명령 한 줄이고, 로더 분기는 열 줄이다.

---

## 9. 주의할 것

- **릴리스 빌드에 `LiveData`·`build-stamp.json`·`LivePatchWatcher`가 들어가면 안 된다.**
  전부 `#if DEVELOPMENT_BUILD`로 감싸고, 배포 패키징에서 두 폴더를 제외한다
- **심링크는 백업·git에 넣지 않는다.** `.gitignore`에 빌드 출력 폴더가 이미 있는지 확인할 것
- **자동 저장 슬롯을 기존 3슬롯과 섞지 않는다.** `__livepatch`는 별도 파일명이어야 한다
- **도메인 리로드를 끈 상태에서 검증을 돌리지 않는다.** static이 남아 결과가 오염될 수 있다.
  `PrototypeValidation`·`ManagementLoopValidation`은 지금처럼 `-batchmode` 새 프로세스에서 돌린다
- **빌드 중에 `build-stamp.json`을 읽으면 깨진 내용이 나올 수 있다.** 위 코드처럼 예외를 삼키고
  다음 주기에 다시 읽는다. 또는 빌드가 임시 파일에 쓰고 마지막에 이름을 바꾸게 한다
