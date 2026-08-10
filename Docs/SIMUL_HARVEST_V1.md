# SIMUL HARVEST V1 — simul에서 더 가져올 것

작성: 2026-08-10 / Claude
성격: **제안서**. 시장 이식 경계는 기존 `SIMUL_MARKET_PORT.md`가 정본이고, 이 문서는 그 밖의 재사용 후보를 정리한다.
원칙: `simul`은 읽기 전용 참고다. 한 줄도 수정하지 않는다.

---

## 0. 이미 가져온 것

중복 조사를 막기 위해 먼저 정리한다.

- 직원 후보 8인 전신 원화 72종, 정체성 앵커 11종 (SHA-256 무변형 확인 완료)
- 아빠·엄마 원화·도트, 화풍 `SIMUL polished soft-render VN anime v3`
- 시장 S1·S2: 6,545 거래일 달력, 호가·체결 규칙, order book 골든
- `DATA_SOURCES.md`의 1차 자료 링크(DART·KIND·KRX·한국은행·금융위)
- 시대별 사건 문법 매트릭스(2000~2026)

아래는 **아직 안 가져온 것** 중 값이 나가는 것들이다.

---

## 1. S급 — 지금 가져오면 바로 효과

### 1-1. 오디오 일습 ★가장 값싸고 효과 큰 항목

지금 family_company에는 **소리가 하나도 없다.** simul에는 라이선스 원장까지 완비된 세트가 있다.

| 항목 | 위치 | 개수 |
| --- | --- | ---: |
| BGM | `flutter_app/assets/audio/bgm/` | 11 |
| SFX | `flutter_app/assets/audio/sfx/` | 39 |
| 라이선스 원장 | `AUDIO_LICENSES.md` | — |
| 런타임 코디네이터 | `flutter_app/lib/game_audio.dart` | — |

BGM은 PeriTune CC BY 4.0이고 원곡·원본 페이지·표기 문구가 표로 정리돼 있다. **그대로 쓸 수 있다.**

가족회사에 바로 맞는 것들:

- `title_gentle_theme.ogg` → 타이틀
- `hub_gentle_brew.ogg` → 사무실 일상 (이미 "로비·일상 허브"용)
- `finance_sakuya.ogg` → 은행·정산·경영 화면
- `market_portside_cafe.ogg` → 주식 화면
- `story_piano_sad.ogg` → 계약 실패·부도
- `relationship_raindrop.ogg` → 가족 대화·관계 이벤트

SFX 중 이 게임 그 자체인 것들:

- `paper_place`, `paper_rustle`, `page_flip` → **계약서·팩스·서류**
- `crt_glitch` → **2000년 CRT 모니터**
- `door_open/close`, `footstep_1/2` → 사무실 이동 (이미 실제 이동이 구현돼 있다)
- `coins`, `coins_large` → 입금·어음 할인
- `notification`, `message_send` → 발주 전화·문자
- `ui_click/confirm/error/back/select/switch/tick` → HUD 일습

**반드시 같이 가져올 것**: `AUDIO_LICENSES.md`의 출처 원장과
"게임 화면·타이틀·설정·엔딩에 크레딧 문구를 노출하지 않는다"는 규칙.
크레딧은 릴리스 메타데이터로 처리한다.

**작업 크기**: 파일 복사 + `Docs/ASSET_MANIFEST.md`에 라이선스 이관 + Unity AudioSource 연결. 하루 안쪽.

---

### 1-2. 폰트 2종

`flutter_app/assets/fonts/`에 라이선스 파일과 함께 있다.

- `MaplestoryBold.ttf` / `MaplestoryLight.ttf` — 2000년대 한국 게임 감성. 제목·HUD용
- `PretendardVariable.ttf` — 본문·표·숫자용
- `LICENSE-Maplestory.txt`, `LICENSE-Pretendard.txt` 동봉

지금 Unity 기본 폰트로 한글을 찍고 있다면 이것만 바꿔도 화면 인상이 크게 달라진다.

---

### 1-3. `DO_NOTS.md` — 실패 목록 ★문서 중 최고가

`simul` 루트, 16KB, 12개 섹션. **같은 팀이 같은 함정에 두 번 빠지지 않게 하는 문서다.**

특히 그대로 유효한 섹션:

- **시장 시간·신문 금지사항** — 미래 누설 방지 규칙. family_company의 History 42개 사건이 정확히 같은 위험을 갖는다
- **로컬 뉴스 조합기 금지사항** — 뉴스가 미래를 흘리지 않게 하는 구체 규칙
- **데이터** — 파생값 저장 금지, 화면 계산값을 저장에 넣지 않기
- **기술과 성능** — 27년 장기 실행에서 실제로 터진 것들
- **작업 방식과 문서** — 완료 기능을 문서에 중복 서술하지 않기 등

**제안**: 그대로 복사하지 말고 family_company용으로 각색해 `Docs/DO_NOTS.md`를 신설한다.
데시멀·카지노·경마 항목은 빼고, 하청·가족·실제 회사 항목을 추가한다.

---

### 1-4. `banking_state.dart` — 은행·대출·신용 (610줄)

`flutter_app/lib/game/banking_state.dart`

**연도별 한국 금리 환경 테이블이 이미 만들어져 있다.** 이게 핵심이다.

```dart
bankRateEnvironmentAt(DateTime date)   // 연도별 예금·대출 금리
bankInterestWithholdingTaxRate = 0.154  // 이자소득세 15.4%
bankMaximumDsrRate = 0.40               // DSR 40%
bankMinimumCreditScore = 300, Max 900, 초기 650
bankDepositTermMonths = [6, 12, 24]
bankLoanTermMonths = [12, 24, 36]
```

2002년 이하 구간이 예금 5.5% / 당좌 1.2%로 잡혀 있다. 2000년 배경에 그대로 맞는다.

**왜 지금 필요한가**: `GAMEPLAY_FUN_V1.md`의 기둥 2(어음)를 넣으면 **어음 할인**이 필요해지고,
할인율은 금리에서 나온다. 신용점수는 협력사 등록 심사(기둥 4)와도 직결된다.

**각색 포인트**: simul은 개인 계좌 기준이다. family_company는 **법인 계좌 + 가계 계좌 두 개**가 필요하고,
14살은 본인 명의 계좌를 못 만든다는 제약이 오히려 재미가 된다.

---

### 1-5. `weekday_activity.dart` + `weekend_activity.dart` — 평일/주말 시간 구조

`flutter_app/lib/game/weekday_activity.dart` (285줄), `weekend_activity.dart` (577줄)

**지금 코덱스가 진행 중인 "가족별 시간대 슬롯"과 정확히 같은 문제를 이미 푼 코드다.**

가져올 구조:

- `weekendActionPointsPerDay = 2` — 주말은 행동 포인트제. 시간 단위가 아니라 **선택 횟수**로 관리
- 평일 오후 일정 + `skip` 선택지 — 아무것도 안 하는 것도 명시적 선택
- **시설 해금 게이트**: `bankAccessUnlocked`, `realEstateAccessUnlocked`, `businessOperationsUnlocked` 같은
  story flag로 기능을 순차 개방. `facilityStoryGatesEnabled` 하나로 전체를 끌 수 있게 돼 있다

마지막 항목이 특히 좋다. family_company도 **은행·주식·인수 화면을 처음부터 다 열면 안 된다.**
"아빠가 은행에 같이 가준 뒤에 은행 화면이 열린다" 같은 게이트를 같은 패턴으로 만들 수 있다.

관련 테스트도 참고할 것: `weekday_afternoon_schedule_test.dart`, `weekday_hub_test.dart`, `weekend_activity_test.dart`

---

## 2. A급 — 곧 필요해질 것

### 2-1. `organization_state.dart` — 직원 등급·직군 (588줄)

직원 후보 8인 **아트는 준비됐는데 고용 시스템이 없다.** 이게 그 빈칸을 메운다.

```dart
enum EmployeeGrade { s, a, b, c, d, f }
enum EmployeeRole { researcher, analyst, investmentManager, tradingOperations,
                    officeAccounting, mergersAcquisitions, legalCompliance, operatingPartner }
```

**직군만 갈아끼우면 된다.** family_company의 `BusinessIndustryCatalog`에 이미 업종별 직군이 정의돼 있다:
프로그래머, 웹 디자이너, 도트 아티스트, 서비스 기획자, 모바일 프로그래머, MIDI 작곡가, 단말 QA…

등급 S~F, 계약금, 월급, 사기 구조는 그대로 쓸 수 있다.
`talent_network` 스킬(신규 직원 계약금 10% 절감) 같은 연결도 이미 있다.

---

### 2-2. `personal_finance_state.dart` — 가계 지출 카탈로그 (846줄)

`Docs/COMPANY_SYSTEM.md`가 "회사 돈과 가계 돈을 직접 더하거나 빼지 않는다"고 못박아 뒀는데,
그 **가계 쪽 구현이 simul에 이미 있다.**

```dart
enum SpendingCategory { community, education, business, realEstate, social }
enum SpendingRepeat { once, monthly, yearly }
SpendingOption { unlockYear, requiresEmployee, requiresLegalCompany,
                 monthlyIncome, monthlyCost, reputationDelta, ... }
```

가장 값진 건 **`unlockYear`로 시대별 지출을 해금하는 구조**다.
2000년엔 못 사는 것이 2005년엔 살 수 있게 되는 게 27년 캠페인의 시간 감각을 만든다.

`requiresLegalCompany` 플래그도 그대로 쓸모가 있다 — 14살 플레이어가 사업자등록 전에는 못 하는 것들.

---

### 2-3. `news_combinator.dart` — 결정론적 신문 (215줄)

파일 첫 줄이 이미 정답이다.

> 네트워크나 외부 AI 없이 공개된 시장 정보만으로 신문 문장을 조합한다.
> 조합 결과는 시뮬레이션 시드와 날짜가 같으면 모든 기기에서 동일하다.
> 저장·테스트용 공개 정보 묶음. **플레이어와 미래 사건 정보는 받지 않는다.**

`GAMEPLAY_FUN_V1.md`에서 제안한 "뉴스가 내 통장과 연결" + "미래 누설 금지"를 이미 구현해 둔 것이다.
family_company는 여기에 **실제 회사 이름**을 넣을 수 있어서 simul보다 강해진다.
History의 42개 사건 + 82개 실제 회사명을 조합기에 물리면 진짜 2000년 신문이 나온다.

같이 볼 것: `market_news.dart` (16KB), `market_news_test.dart`

---

### 2-4. `life_calendar.dart` + `CALENDAR_SYSTEM.md` — 성장 달력 (404줄 + 문서)

```dart
enum LifeCalendarEventKind { milestone, relationship, outing, market, personal }
```

`CALENDAR_SYSTEM.md`의 목차가 그대로 필요한 것들이다:
`2. 직접 하루 보내기`, `3. 주말 자유 일정`, `4. 달력 화면`, `5. 사건과 그림 확장 계약`.

family_company는 2000~2026 27년 캠페인인데 **페이싱 장치가 아직 없다.**
시험기간·명절·결산월 같은 달력 사건을 이 구조에 얹으면 된다.
`monthly_unlock_chapter.dart`(48KB)도 월별 해금 페이싱의 참고 사례다.

---

### 2-5. 장기 검증 방법론 ★코드가 아니라 교훈

이건 파일을 가져오는 게 아니라 **방법을 가져오는 것**이다.

| 테스트 | 무엇을 검증하나 |
| --- | --- |
| `business_long_run_balance_test.dart` | 복수 시드·18업종·6입지·여러 개점 연도·4개 정책의 2000~2026 분포. **흑자·적자·자발 정리·강제폐업이 모두 발생하는지** |
| `business_ten_year_replay_test.dart` | 10년 재생 결정론 |
| `ten_year_playtest_test.dart`, `ten_year_simulation_test.dart` | 27년 세계의 장기 안정성 |
| `stock_governance_one_year_stress_test.dart` | 1년 스트레스 |
| `real_estate_version_migration_test.dart` | 저장 마이그레이션 회귀 |

`BALANCE_NOTES.md`에 기록된 교훈이 특히 중요하다:

> 그 감사는 시드 하나가 아니라 여러 시드를 합산해 검증한다. 예전에는 고정 시드 하나만 봤는데,
> 그 시드가 전형적인 시드보다 변동이 몇 배 큰 이상치여서 실제 플레이어 시장이 계약 미달인데도 통과했다.

family_company의 `PrototypeValidation`·`ManagementLoopValidation`은 지금 **단일 시드**다.
27년 캠페인을 열기 전에 복수 시드 합산 검증으로 바꿔야 같은 함정을 피한다.

**검증 계약의 좋은 예**: "흑자·적자·자발 정리·강제폐업이 *모두* 발생한다"는 조건.
한 방향으로만 굴러가지 않는지를 확인하는 방식이라 family_company의 하청 밸런스에 그대로 쓸 수 있다.

---

## 3. B급 — 나중에, 조건부

| 항목 | 위치 | 언제 |
| --- | --- | --- |
| `listed_company_management.dart` (30KB) + `SHAREHOLDER_GOVERNANCE_SYSTEM.md` (17KB) | 경영권·주총·지분·공개매수 | 시장 S4. family_company의 최종 목표가 여기다 |
| `corporate_disclosure.dart` (32KB) | 공시 타입 12종 분류 | 시장 S4. History 사건 타입과 겹침 |
| `real_estate_*.dart` + `REAL_ESTATE_SYSTEM.md` | 사무실 임대→매입 사다리 | 사무실 업그레이드를 실제 계약으로 만들 때. 아파트/상가 중심이라 사무실용 각색 필요 |
| `business_state/engine/simulation.dart` | **월 손익·평판·고객충성도·설비상태·직원사기·미지급금 구조** | 19개 동네 업종 *내용*은 DECISIONS에서 이미 배제. **구조만** 가져온다 |
| `dialogue/` + `DIALOGUE_EDITOR_GUIDE.md` + `generate-dialogue-editor-data.mjs` | 대사 저작 도구 | 가족 대사가 늘어나면. 누나 이름도 안 정해진 지금은 이르다 |
| `investor_flow.dart` (176줄) | 투자자별 매매동향 | 주식 화면 정보 밀도를 올릴 때 |
| `player_progression.dart` (98줄) | 스킬 8종 + 레벨 임계값 | 플레이어 성장을 넣기로 결정하면. 지금은 스코프 밖일 수 있다 |
| `home_improvement_state.dart` | 공간 개선 | 사무실 설비 업그레이드 참고 |

### 작지만 즉효인 것

- **HUD 아이콘 5종**: `hud_clean_hourglass`(시간), `hud_clean_letter`(계약서), `hud_clean_quest`,
  `hud_clean_fast`(배속), `hud_clean_moon`(밤), `hud_clean_arrow_right`.
  지금 HUD가 텍스트 위주라면 바로 붙는다.
- **이미지 파이프라인 스크립트**: `scripts/defringe_rgba.py`, `extract_character_alpha.py`,
  `normalize_character_sprites.py`, `batch_extract_character_alpha.py`.
  family_company는 이미 크로마 하드키 투명화를 쓰는데, **`defringe_rgba.py`는 가장자리 색번짐까지 제거**한다.
  imagegen 결과물을 계속 만들 거라면 이 쪽이 더 정교하다.
- **`ambient_*` 4종**: 골목 자전거·미니버스·행인·복도 고양이. 2000년 동네 배경 소품.

### 신문배달 미니게임 — 판단 필요

`NEWSPAPER_DELIVERY_MINIGAME.md` + `rider_mini_game.dart` + `action_strategy.ogg` +
`bg_newspaper_delivery_dawn_seoul_2000_v1.png`가 한 세트로 있다.

**찬성 근거**: 14살이 2000년 한국에서 새벽에 할 수 있는 일이고, `GAMEPLAY_FUN_V1.md`의
"나는 낮에 학교라 시간이 없다"와 정확히 맞물린다. 새벽 → 학교 → 저녁 작업이라는 하루가 만들어진다.
**반대 근거**: 시작 자본이 이미 500만원으로 고정돼 있어 종잣돈 벌이가 필요 없다.
미니게임이 본 루프에서 시선을 뺏을 수 있다.

→ 넣는다면 "돈"이 아니라 **"체력을 태워 관계·평판을 얻는 선택"**으로 각색해야 의미가 있다.

---

## 4. 가져오지 말 것

| 항목 | 이유 |
| --- | --- |
| `casino_state.dart`, `horse_racing.dart` | 14살 가족회사와 톤이 맞지 않는다. 경마장 에셋은 이미 누나 원화로만 재활용됨 |
| `cohort_*.dart` (7개 파일) | 데시멀 동기 10명 세계관 전용. family_company에는 대응 개념이 없다 |
| `fictional_market.dart` (137KB) | **정면 충돌**. simul은 가상기업 50개를 생성하고, family_company는 실제 회사 82개를 쓴다 |
| `phone_ai_service.dart` | Gemini 네트워크 의존. family_company의 오프라인 결정론 원칙과 충돌 |
| `relationship_state.dart`의 호감도·데이트 단계 | 8명 동기용 연애 수치다. **가족에게 그대로 쓸 수 없다.** 구조(다차원 수치, 하루 종료 정산)만 참고 |
| `market_corpus_daily_samples.dart` (415KB) | 이미 시장 이식에서 처리된 범위. 파생 수치는 복사하지 않는다는 기존 원칙 유지 |

---

## 5. 권장 순서

| 순위 | 항목 | 크기 | 왜 지금 |
| --- | --- | ---: | --- |
| 1 | **오디오 + 라이선스 원장** | 작음 | 소리가 하나도 없는 게 지금 가장 큰 체감 구멍. 복사로 끝난다 |
| 2 | **폰트 2종** | 아주 작음 | 화면 인상이 즉시 바뀐다 |
| 3 | **`Docs/DO_NOTS.md` 신설** | 작음 | History 42개 사건이 미래 누설 위험을 이미 안고 있다. 규칙을 먼저 박아야 한다 |
| 4 | **평일/주말 시간 구조 + 시설 게이트** | 중간 | 코덱스가 지금 하는 작업과 같은 문제다. 바퀴를 다시 만들 이유가 없다 |
| 5 | **은행·대출·신용** | 중간 | 어음 할인이 들어가는 순간 필수. 연도별 금리 테이블이 공짜 |
| 6 | **복수 시드 장기 검증으로 전환** | 중간 | 단일 시드 검증이 이상치를 통과시킨 사례가 이미 기록돼 있다 |
| 7 | 직원 등급·직군 | 중간 | 아트 8인분이 놀고 있다 |
| 8 | 뉴스 조합기 | 중간 | 실제 회사명이 붙으면 simul보다 강해진다 |
| 9 | HUD 아이콘 + defringe 스크립트 | 아주 작음 | 자투리 시간에 |

**1~3번은 반나절이면 끝나고 체감이 제일 크다.** 거기부터 하는 걸 권한다.

---

## 6. 이관 규칙

기존 원칙을 그대로 따른다.

- `simul`은 **한 줄도 수정하지 않는다**
- 에셋을 옮기면 `Docs/ASSET_MANIFEST.md`에 출처·라이선스·이관일을 기록한다
- 오디오는 **SHA-256 무변형 비교**로 확인한다 (직원 원화 72종을 그렇게 확인했다)
- 코드는 복사가 아니라 **구조 참고 후 순수 C#으로 재작성**한다. Dart 파일을 그대로 옮길 수는 없다
- 파생 수치 데이터는 옮기지 않는다. 규칙과 구조만 가져온다
- 라이선스 문구는 게임 화면에 노출하지 않고 릴리스 메타데이터로 처리한다
