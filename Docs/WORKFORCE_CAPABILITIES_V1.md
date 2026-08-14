# WORKFORCE CAPABILITIES V1

이 문서는 가족 4인과 향후 채용 완료 직원이 공유하는 업무 능력 정본이다. 현재 상태인 체력, 스트레스,
신뢰·유대, 직원 사기·충성도는 업무 능력과 별도이며 이 문서의 수치로 대체하지 않는다.

## 영속 상태

- 업무 능력은 `engineering`(기술개발), `planning`(기획), `creative`(창작), `business`(사업),
  `operations`(운영), `collaboration`(협업) 6종이며 모두 0~100 정수다.
- `potential`은 성장 계산에 쓰는 0~100 정수로 저장한다. UI는 정확한 수치를 공개하지 않고 공용
  `WorkforcePotentialGradeRules`가 만든 문자만 표시한다: S 90~100, A 80~89, B 65~79,
  C 50~64, D 35~49, F 0~34.
- 각 능력은 현재 XP와 fixed-point remainder를 저장한다. XP 입력은 권위 있는 정수 GameTime에서
  완료·반영된 기여 분뿐이다. 이동, 애니메이션, 프레임 시간, UI 열기와 교육 준비 중 버튼은 XP를
  만들지 않는다.
- `stressGainBasisPoints`는 업무 능력이 아니라 스트레스 저항 프로필이다. 현재 스트레스와 별도로
  표시하며 계약 품질·진행·속도에는 직접 가산하지 않는다.

## 업무 계산

모든 `WorkTaskProfile`은 진행·품질·학습별 6능력 가중치 합이 각각 10,000bp여야 한다. 업무 점수는
가중 평균이며 별도 Speed 능력은 없다. 점수 0/50/100의 업무 속도는 각각 70%/100%/130%다.
1인시 확정에 필요한 정수 GameTime은 `ceil(60 × 10,000 / 업무속도bp)`로 계산한다. 따라서
0/50/100점은 각각 86/60/47분이 필요하며, E키 유지 시간·렌더 프레임·창을 연 실제 초는 작업량이나
XP가 될 수 없다.
계약 품질은 실제 기여자와 기여 인시로 가중하고, 제품 호환 경계는 요구분석·개발·디자인·출시영업·
운영 단계 프로필을 사용한다. 제품별 실제 기여 ledger가 추가될 때까지 제품 계산은 단계마다 적합한
상위 2인의 호환 점수를 사용하는 임시 어댑터다.

XP 분자는 `기여 분 × 학습 가중치 × (8,000 + potential × 40)`이며 분모 100,000,000과 나머지를
저장한다. 따라서 같은 총 기여 분을 1x/2x/4x 청크로 나눠도 결과가 같다. 다음 레벨 XP는
`600 + 현재 능력 × 30`이고 100에서 멈춘다.

## Save v10 이관

Save v10은 가족별 `WorkforceCapabilitySnapshotDto`를 저장하며 v1~v9는 한 번만 다음처럼 이관한다.

- engineering = Development
- planning = Planning
- creative = Art
- business = Sales
- operations = round((legacy Speed + Planning + legacy Stamina) / 3)
- collaboration = Teamwork
- potential = Potential
- stressGainBp = clamp(12,000 - legacy Mental × 40)

legacy Speed/Stamina/Mental은 이 이관 함수 밖의 신규 권위 계산에서 사용하지 않는다. 신규 게임은
이관 함수를 거치지 않고 같은 결과를 명시한 시작 능력 카탈로그를 사용한다.

| 구성원 | 기술개발 | 기획 | 창작 | 사업 | 운영 | 협업 | 잠재력 표시 | 스트레스 저항 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| 나 | 58 | 61 | 47 | 32 | 62 | 55 | A | 58% |
| 누나 | 37 | 52 | 44 | 55 | 62 | 72 | B | 65% |
| 아빠 | 24 | 45 | 23 | 68 | 54 | 61 | D | 72% |
| 엄마 | 32 | 55 | 35 | 46 | 60 | 70 | C | 76% |

## UI와 확장 경계

MainNavigationV2의 인사 탭은 현재 고용된 가족 4명만 `창업 가족`으로 표시한다. 미채용 후보 8인은
roster, 출근, Save에 넣지 않는다. 목록은 `WorkforceRosterViewModelRules`가 현재 고용 상태를 열거해
만들므로 향후 직원도 같은 `WorkforceCapabilityState`를 제공해야 한다. 능력 6종/XP/잠재력 등급과
현재 체력·스트레스·신뢰·스트레스 저항은 화면에서 분리한다. 교육은 권위 명령이 생길 때까지
`교육 준비 중`이며 XP를 변경하지 않는다.

직원 목록의 한글은 bitmap에 넣지 않고 런타임 TMP 폰트로만 그린다. 1280×720 실제 픽셀 기준으로
상세 제목 24px, 직원명 18px, 본문·능력치 14px 이상을 유지하며 autosize 축소는 사용하지 않는다.
행과 카드 높이가 글자 최소 크기를 수용하고 overflow가 없어야 한다. 1920×1080, 1392×768,
1280×720 Hidden D3D 캡처로 이를 검증한다. 배경·버튼·아이콘은 기존 skin sprite reference를 사용해
공용 UI 리마스터가 코드 구조를 바꾸지 않고 교체할 수 있어야 한다.
