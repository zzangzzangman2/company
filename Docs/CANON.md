# CANON

이 문서는 현재 콘텐츠 정본이다. 충돌하는 옛 문서나 에셋보다 이 문서가 우선한다.

## 게임 전제

- 장르: 싱글플레이 생활 경영 RPG
- 배경: 2000년대 초반 한국풍 가상 도시
- 시작 시각: 2000-01-03 월요일 08:50. 가족 4인만 09:00~09:03에 1분 간격으로 문을 통해 입장하고 18:00부터 퇴근한다. 직원 8인은 향후 채용 후보이며 고용되기 전에는 출근하지 않는다.
- 플레이어는 14살이므로 법률 계약과 은행 업무는 성인 가족의 도움을 받는다.
- 가족은 단순한 직원 슬롯이 아니라 관계, 피로, 시간, 회사 역할을 동시에 가진다.

## 가족

### 플레이어

- 내부 ID: player
- 시작 나이: 14
- 임시 생일: 1985-08-10
- 역할: 창업 아이디어, 제품 방향, 시장 조사, 현장 행동
- 런타임 외형 정본: 짧고 헝클어진 짙은 갈색 머리, 갈색 눈, 작은 금색 장식이 있는 **빨간 뉴스보이 캡**
- 런타임 의상 정본: 흰색 후드 윈드브레이커와 남색 트리밍, 남색·노랑·빨강 줄무늬 티셔츠, 짙은 남색 바지, 흰색·남색 운동화
- 정본 범위: 플레이어를 월드에서 식별하는 조작 말의 외형이다. 별도 VN 초상화나 실존 사용자 얼굴을 의미하지 않는다.
- 기반 디자인: 기존 `simul` 타이틀의 14살 플레이어 디자인
- 런타임 이동 정본: `Assets/Art/Characters/Player/Pixel/HighMotion/player_pixel_walk8dir6_{a,b}_v1.png`
- 런타임 방향: 남·남서·서·북서·북·북동·동·남동, 방향별 걷기 6프레임

### 누나

- 내부 ID: older_sister
- 이름: 미정
- 시작 나이: 20 (고정)
- 임시 생일: 1979-11-20
- 외형 정본: 긴 검은 양갈래, 검은 리본, 청록색 눈, 성인 20살
- 의상 정본: 몸에 무리 없이 맞는 어두운 민소매 나시티, 흰 파이핑이 있는 남색 돌핀팬츠
- 신발 정본: 맨발. 두 발이 보이는 전신 원화를 기본으로 한다.
- 초기 회사 역할: 운영, 고객 응대, 사무 지원. 세부 성격과 직책은 임시다.
- 기반 에셋: 기존 경마장 표 판매원
- 런타임 이동 정본: `Assets/Art/Characters/OlderSister/Pixel/HighMotion/older_sister_pixel_walk8dir6_{a,b}_v1.png`
- 런타임 방향: 남·남서·서·북서·북·북동·동·남동, 방향별 걷기 6프레임

### 아빠

- 내부 ID: father
- 시작 나이: 46 (가족 연령 관계를 맞춘 임시 확정)
- 임시 생일: 1953-06-15
- 역할: 법정대리, 대외 계약, 은행, 영업
- 외형 정본: 짧고 단정한 숯검정 가르마 머리와 관자놀이의 옅은 새치, 짙은 갈색 눈, 가는 은색 사각 안경, 넓은 어깨의 46살 성인
- 의상 정본: 소매를 걷은 탁한 청록 셔츠, 차콜 슬랙스, 갈색 벨트·구두, 아날로그 손목시계
- 정본 원화: Assets/Art/Characters/Father/father_office_neutral_v1.png
- 런타임 이동 정본: `Assets/Art/Characters/Father/Pixel/HighMotion/father_pixel_walk8dir6_{a,b}_v1.png`
- 런타임 방향: 남·남서·서·북서·북·북동·동·남동, 방향별 걷기 6프레임

### 엄마

- 내부 ID: mother
- 시작 나이: 44 (가족 연령 관계를 맞춘 임시 확정)
- 임시 생일: 1955-09-02
- 역할: 재무, 회계, 급여, 가계 조율
- 외형 정본: 부드러운 성인형 얼굴, 어깨 길이의 짙은 밤색 머리와 낮은 하프업 트위스트, 갈색 눈, 단단하고 부드러운 체형의 44살 성인
- 의상 정본: 더스티 피치 카디건, 크림 블라우스, 짙은 청록 A라인 스커트, 짙은 갈색 로퍼, 진주 귀걸이, 아날로그 손목시계
- 정본 원화: Assets/Art/Characters/Mother/mother_office_neutral_v1.png
- 런타임 이동 정본: `Assets/Art/Characters/Mother/Pixel/HighMotion/mother_pixel_walk8dir6_{a,b}_v1.png`
- 런타임 방향: 남·남서·서·북서·북·북동·동·남동, 방향별 걷기 6프레임

## 가족 4명 보행 제작 정본

- 최우선 계약은 `Docs/CHARACTER_LOCOMOTION_GENERATION_V1.md`의
  `FC-CHARACTER-LOCOMOTION-GENERATION-V1`, `FC-FAMILY-LOCOMOTION-RIG-V1`과
  `FC-CHARACTER-LOCOMOTION-FOOT-LOCK-QA-V1`이다.
- 현재 출하/gate 범위는 가족 4명×8방향×6위상=192 PNG다. 직원 후보 8명은 가족 4명의 실제 Player
  품질 승인 뒤 같은 정본을 확장한다. 제작/publish는 `Tools/generate_character_locomotion_v1.py`만 수행한다.
- `ArtSources/FamilyLocomotionRigV1`의 SHA 고정 분리 leg parts와
  `Tools/CharacterLocomotionIdentityV1`의 방향별 canonical upper가 source authority다. 완성 전신 6패널
  생성, 허리 cap과 이미 중앙 정렬된 runtime 재입력은 금지한다.
- 각 6프레임은 P0~P2 해부학적 left support/right swing, P3~P5 right support/left swing이다. PPU 180,
  scale 1.55, stride 0.99380799에서 지지발은 local 진행축 반대로 phase당 19.234993px 이동하며 projected
  drift가 1px 이하여야 한다. P1/P4의 canonical upper와 hip은 함께 1px 이동해 완전 고정 몸통과 seam
  분리를 동시에 막는다.
- 방향 승인은 row token이나 얼굴만 보지 않는다. 시선/머리→흉곽→골반→무릎·발목→양쪽 신발 앞코가
  실제 변위와 같은 방향이어야 한다. 뒤발의 앞코 반전, 정면으로 벌어진 발, 같은 swing leg의 두 번 반복은
  발 이동량·cadence가 정상이어도 실패다.
- `ArtSources/FamilyWalkHalfCyclesV2`, `IdentityModelV1`, `MarkerReviewV1`은 가족 제작 provenance와 좌우
  다리 증거로 보존하지만 shipping writer가 아니다. `BeforeCoherenceV1`과 `MotherSideWalkV3`도 현재
  motion source가 아닌 실패 분석/역사 자료다. 구 builder `--write`는 차단돼 있다.
- 정상 가족 런타임은 별도 `LocomotionTransitionsV1` 초상화를 섞지 않고 승인 walk/idle 계열만 사용한다.
  cardinal 타일 중심 구간은 다음 구간 방향으로 pivot을 끝낸 뒤 translation한다.

## 향후 직원 후보 8인

- `simul`의 김서아·이지안·최이서·정아린·박하은·한수아·오지우·윤채아를 향후 고용 가능한 직원 후보 에셋 풀로 사용한다.
- 각 인물의 정본 전신 원화 9종과 정체성 앵커는 외형·표정·복장을 바꾸지 않고 그대로 보존한다.
- 에셋 루트: Assets/Art/Characters/Employees/
- 인물별 런타임 도트는 8방향과 방향별 걷기 6프레임을 가진다. 정본 루트는 각 인물의 `Pixel/HighMotion/`이다.
- 현재 직원 보행 PNG는 이번 가족 4명 seam/모자 수정의 출하 승인이나 QA 범위에 포함되지 않는다.
- 이 8인은 시작 시점의 4인 가족 창업팀에 자동 합류하지 않으며, 이후 고용 시스템에서 해금·채용한다.

## 에셋 권리

사용자는 프로젝트의 기존 생성 에셋이 모두 GPT로 생성되었고 본인이 사용 권리를 보유한다고 명시했다. 외부 에셋을 새로 추가할 때는 별도 라이선스를 ASSET_MANIFEST에 기록한다.

## 초기 타일 사무실 정본

- 실제 새 게임은 `OfficeGridLayouts.CreateNewGameEmptyOfficeV1()`을 사용한다. 13×13 바닥과 외곽 52 bay만 있고 플레이어 배치 가구·좌석·워크스테이션은 0개다. 회사 허브의 `사무실 관리`에서 가구를 구매해 배치한다.
- 모든 구매 가구의 저장 원점은 정수 타일이며 1×1은 그 타일 중심, 다중 타일은 전체 footprint 중심을 의미·시각·충돌 공통 anchor로 사용한다. 포인터의 임의 world 좌표를 저장하지 않는다.
- `CreateStarterOfficeV1()`의 실내 가구 17개 + 외곽 52 bay(총 69), 가족 workstation 4개 구성은 기존 저장 호환과 출근·좌석 QA용 furnished fixture로 유지한다. 현재 전체 저장 스키마는 v10이며 기존 저장의 의미 `OfficeGrid`를 그대로 이관한다.
- 플레이테스트 런타임은 새 게임/불러오기 직후 해당 `GameState.OfficeGrid`를 타일 씬으로 렌더한다. 폐기된 OfficeVisualV2 통짜 PNG는 저장소와 빌드에 존재하지 않으며 `F9`로도 되돌리지 않는다.
- `CreateMigrationPreview()`의 가구 18개·12종·파티션 구성은 T1~T5 회귀 fixture 전용이다. 실제 게임 기본 사무실로 사용하지 않는다.
- workstation은 desk/chair/seat binding, seat/approach cell, NorthWest facing, 반 셀 operator anchor를 가진다. 네 가족의 의미 root는 좌석 셀 중심·scale 1이다.
- 시각 calibration의 유일한 저장 위치는 calibration version 3의 `OfficeFurnitureVisualCatalog.asset`과 version 5의 `OfficeCharacterSeatPoseCatalog.asset`이다. 의자 좌판 중심·책상 operator socket·가구 footprint·clip/frame별 실제 pelvis/hand를 수동 교정하고 전체 QA를 통과한 값만 승인 데이터로 유지한다.
- NorthWest 회전의자의 좌판과 등받이 대부분은 인물 뒤 base로 그린다. chair front overlay는 등받이의 제한된 전면 가장자리와 근접 팔걸이만 인물 위에 그리고 좌판·몸통을 덮지 않는다. 책상 front overlay는 하체 앞의 다리·서랍·앞 모서리만 담당한다.
- 외곽은 13×13 바닥 polygon의 네 외변을 따라 far full wall 26 + near cutaway 25 + `(8,0)` exterior threshold 1의 정확한 한 타일 bay 52개다. 벽 inner edge는 바닥 outer edge와 일치하고 모든 기단 픽셀은 바닥 밖에 있어야 한다. `entrance_door`는 저장 호환 ID일 뿐 door leaf/jamb/lintel/열림 애니메이션이 아니며, 가족은 기존 `(8,1)` entrance를 통해 09:00~09:03 순차 입장한다.
