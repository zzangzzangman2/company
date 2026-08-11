# 가족회사 (가제)

14살 플레이어가 엄마·아빠·누나와 2000년의 작은 오피스텔 사무실에서 시작해, 하청을 버티고 자체 사업을 세우며 실제 기업들과 경쟁해 세계적인 회사로 성장하는 싱글플레이 생활 경영 RPG다.

## 우리가 만드는 게임

- 시작은 `2000-01-03 08:00`, 가족 4명, 자본금 500만 원이다.
- 계약 게시판에서 요구 능력·마감·보상·위약금을 비교하고 실제 인력과 시간을 배치한다.
- 가족은 직원 슬롯이 아니다. 실제 사무실을 걷고, 앉아 일하고, 스트레스를 받으며, 힘들면 휴게실에서 회복하고, 학교·영업·가사·수면 일정에 따라 출퇴근한다.
- 하청 수익으로 R&D를 해금하고 시장을 조사해 웹·모바일·하드웨어·패션 등 자체 사업과 제품으로 확장한다.
- 2000~2026 국내 실제 회사·사건·상장 종목을 기준선으로 사용하되, 플레이어의 경쟁·인수·기술 선점으로 역사를 바꿀 수 있다.
- 주식은 별도 장난감이 아니라 회사 자금과 연결된 경영 리스크다. 실제 장 시간, 7+7 호가, 가격·시간 우선 FIFO, 수수료·세금과 저장 결정론을 유지한다.
- 화면은 1920×1080 PC 가로형이며, 밝고 캐주얼한 SIMUL-v3 화풍과 실제 한글 UI를 사용한다.

기능별 완료·진행·미완료와 정확한 다음 순서는 [PROJECT_STATE.md](Docs/PROJECT_STATE.md) 하나를 정본으로 본다.

## 절대 작업 규칙

> 정본 브랜치는 `main` 하나다. 새 브랜치, `agent/*`, 기능 브랜치, 임시 브랜치와 별도 worktree를 만들지 않는다.

- 회사 PC·집 PC·다른 Codex/Claude·다른 도구에서 작업하더라도 모두 같은 규칙을 따른다.
- 한 채팅에서 한 작업씩 순차 진행한다. 사용자가 다시 명시적으로 허용하지 않는 한 다른 채팅·에이전트에 위임하거나 새 작업을 만들지 않는다.
- 수정 전 `git status --short --branch`로 `main`과 변경 상태를 확인한다. clean일 때만 `git pull --ff-only origin main`을 실행한다.
- 예상하지 못한 변경이 있으면 삭제·덮어쓰기·일괄 stage하지 않고 출처와 소유 범위를 먼저 확인한다.
- `C:/Users/godho/Documents/Codex/simul`은 이관 참고용 읽기 전용 저장소다.
- `Library`, `Temp`, `Logs`, `work`, 빌드 산출물은 Git에 넣지 않고 `Assets`의 `.meta`는 반드시 추적한다.

## 어디서 작업하든 먼저 읽을 문서

| 순서·분야 | 문서 | 역할 |
| --- | --- | --- |
| 1 | [AGENTS.md](AGENTS.md) | 브랜치·작업·검증·파일 소유권의 절대 규칙 |
| 2 | [PROJECT_STATE.md](Docs/PROJECT_STATE.md) | 현재 완료/진행/미완료, 다음 순서, 검증 결과 |
| 3 | [CANON.md](Docs/CANON.md) | 가족·나이·외형·콘텐츠 정본 |
| 4 | [DECISIONS.md](Docs/DECISIONS.md) | 구조와 방향을 그렇게 정한 이유 |
| 5 | [ARCHITECTURE.md](Docs/ARCHITECTURE.md) | 순수 시뮬레이션·저장·Unity 프레젠테이션 경계 |
| 사무실·UI·이미지 | [ART_STYLE.md](Docs/ART_STYLE.md), [OFFICE_V0_2.md](Docs/OFFICE_V0_2.md) | 공식 화풍, 16:9 UI, 사무실 공간 규칙 |
| 계약·경영 재미 | [CONTRACTS_V0_3.md](Docs/CONTRACTS_V0_3.md), [GAMEPLAY_FUN_V1.md](Docs/GAMEPLAY_FUN_V1.md) | 계약 정본과 재미 제안서 구분 |
| 주식·SIMUL 이식 | [SIMUL_MARKET_PORT.md](Docs/SIMUL_MARKET_PORT.md), [STOCK_MARKET_LANDSCAPE_V1.md](Docs/STOCK_MARKET_LANDSCAPE_V1.md) | 시장 정본 경계와 가로형 UI/호가 검증 |
| 실제 회사 역사 | [CLAUDE_HANDOFF_HISTORY_DATA.md](Docs/CLAUDE_HANDOFF_HISTORY_DATA.md), [CLAUDE_HISTORY_PROGRESS.md](Docs/CLAUDE_HISTORY_PROGRESS.md) | History 전용 경로, 데이터 개수와 불확실성 |
| 집 PC 재개 | [HOME_PC_CONTINUATION_GUIDE.md](Docs/HOME_PC_CONTINUATION_GUIDE.md) | 설치·pull·Unity 최초 확인 순서 |
| Windows 실행본 | [PLAYTEST_BUILD.md](Docs/PLAYTEST_BUILD.md) | 최신 EXE 재빌드와 산출물 위치 |
| 금지 사례 | [DO_NOTS.md](Docs/DO_NOTS.md) | 미래 누설·가짜 직원·세로 UI 등 반복 금지 |

제안 문서는 아이디어 입력일 뿐 자동으로 정본이 아니다. 구현·검증 후에만 `PROJECT_STATE`와 `DECISIONS`에 반영한다.

## 시작하기

- Unity: `6000.3.21f1` 고정
- 시작 씬: `Assets/FamilyCompany/Scenes/Prototype01.unity`
- 원격 저장소: `https://github.com/zzangzzangman2/company.git`

```powershell
git switch main
git status --short --branch
git pull --ff-only origin main
```

## 회사 PC 검증 규칙

사용자가 회사에서 일하는 동안에는 Unity 창이나 플레이테스트 EXE를 앞에 띄우지 않는다.

- 컴파일·순수 로직·Editor 검증: Unity `-batchmode -nographics -quit`
- 실제 렌더·PlayMode 캡처: Unity `-batchmode` 사용, `Camera.Render`가 필요하면 `-nographics` 금지
- 장시간 검증: 백그라운드 프로세스로 실행하고 로그의 명시적 PASS/FAIL과 종료 코드를 함께 확인
- 사용자 입력이 필요한 EXE 육안 검증은 자동 실행하지 않고 먼저 알림

현재 참고 실행본은 `C:/Users/godho/Downloads/FamilyCompany_Playtest/FamilyCompany.exe`다. 정확한 빌드 출처와 해시는 [PLAYTEST_BUILD.md](Docs/PLAYTEST_BUILD.md)를 따른다.

## 기본 자동화

- `Tools/BuildPrototype.ps1`: `Prototype01` 재생성
- `Tools/ValidatePrototype.ps1`: 시간·RNG·가족·회계·저장·에셋 검증
- `Tools/Build-FamilyCompanyWindows.ps1`: Unity 없이 실행할 Windows 플레이테스트 빌드 생성
