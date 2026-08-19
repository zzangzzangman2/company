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
- 런타임 이동 후보: 실제 moving은 `Player{East,South,North,West}ContactV1`의 source-exact 2포즈를 쓰고,
  대각선은 west/east를 재사용한다. `HighMotion`은 idle/turn/착석/작업 fallback을 소유한다.
- 런타임 방향: 남·남서·서·북서·북·북동·동·남동. 실제 게임 정규화 결과는 사용자 최종 승인 대기다.

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

## 주인공 원본 접촉 보행 제작 정본

- 현재 실제 게임 후보는 `FC-PLAYER-NATURAL-WALK-V1`의 eight-pose V4다. source-exact contact 두 장
  사이를 각 반주기 `toe→pass→land`로 나눠 한 타일당 8단계를 사용한다. 각 자세는 실제 이동거리 12.5%만
  소유하고 왼발/오른발을 교대로 들어 접지발과 이동발을 구분한다. 상체와 정체성 픽셀, 원래 다리 굵기는
  contact가 계속 소유하며 ImageGen이나 legacy HighMotion 픽셀은 사용하지 않는다.
- 주인공 코너 회전은 0.18초 planted hold 동안 이전/중간 cardinal/목표 contact를 거친다. 이 시간에 logical
  root는 타일 중심에 고정되고 그 뒤에만 다음 center-to-center translation이 시작된다.
- `FC-PLAYER-EAST-CONTACT-V1`과 `FC-PLAYER-SOUTH-CONTACT-V1`은 단독 화면 승인을 받았고 north/west 및
  diagonal mapping도 Editor/Windows Player 실행 게이트를 통과했다. 다만 최초 실제 게임 PPU 180 통합은
  과대·부유·코너 scale pop으로 시각 실패했으므로, PPU 314/324 정규화 실제 게임 결과 전체가 사용자
  최종 화면 승인 대기다.
- source authority는 `Assets/Art/Characters/Player/Pixel/player_pixel_walk4x2_v1.png`이다.
  `Tools/extract_player_east_contacts_v1.py`가 east 열 두 접촉 포즈를 픽셀 생성/보간 없이 게시한다.
- 런타임 `PlayerEastContactPresenter`는 기존 이동의 `GaitPhase01`만 읽고 0.0/0.5 반주기에 두 포즈를
  선택한다. logical root, stride, collision, arrival, depth 식은 바꾸지 않는다.
- idle, 착석, 작업, 퇴장은 기존 `DirectionalSpriteAnimator`와 checked-in fallback이 소유한다.
- south 정본은 같은 source authority의 south 열 두 접촉 포즈만 픽셀 생성/보간 없이 게시한다. 런타임
  `PlayerSouthContactPresenter`는 south+moving에만 활성화되고 east presenter와 서로의 파일을 읽거나
  덮어쓰지 않는다. 통합 Player의 east 6장도 기존 승인 캡처와 바이트 단위로 같음을 확인했다.
- north/west 후보도 정본 시트의 해당 열 두 장씩만 독립 게시한다. source에 대각선이 없으므로 대각선
  moving은 west/east exact contact를 수평 우선으로 재사용한다. 신규 대각선 픽셀이나 legacy generated
  diagonal art를 주인공 production walk에 섞지 않는다. 이 6방향 후보는 사용자 최종 화면 승인 전이며
  다른 가족으로 확대하지 않는다.
- 이 presenter들은 시각 sprite만 교체한다. 실제 새 게임의 pathfinding, logical root, tile-center waypoint,
  collision, arrival은 기존 `OfficeRuntimeAgent`가 계속 소유한다. D3D11 실제 사무실 8개 인접 타일 loop에서
  center segment 최대 이탈 `0.00000053 world`, endpoint/visual-root/final-center 오차 0을 확인했다. contact는
  동/서 PPU 314, 남/북 PPU 324, bottom padding 0이며 수치 PASS는 사람 화면 승인을 대신하지 않는다.
- 과거 `FamilyWalkHalfCyclesV2`, `FamilyLocomotionRigV1`, `MotherSideWalkV3`, `MotherNorthWalkV2`,
  `CharacterLocomotionIdentityV1` source/writer/gate는 2026-08-19에 삭제했다. 현재 재현 경로가 아니다.
- 승인 layered PSB가 없으면 자동 분리 rigid cutout을 production에 게시하지 않는다. 사람이 원본을
  12~18개 rigid layer로 나눈 경우에만 Unity 2D Animation east-only 후보를 다시 열고, D3D11 Player
  사람 승인 전에는 범위를 넓히지 않는다.

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
