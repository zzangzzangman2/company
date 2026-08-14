# ART STYLE

최종 갱신: 2026-08-14

## 정본 방향

가족회사는 순수 의미 `OfficeGrid`와 Unity Isometric Tilemap, 직교 등각 카메라, native-resolution Point 렌더, 2D 도트 캐릭터를 결합한 등각 도트 스타일을 사용한다.

## 최상위 화풍 정본

- 공식 원화·키아트 화풍명: **SIMUL polished soft-render VN anime v3**
- 한국어명: **고밀도 폴리시드 소프트 렌더 비주얼노벨 애니 v3**
- 프로젝트 내 영구 앵커: `Assets/Art/StyleTargets/References/simul_polished_soft_render_vn_style_anchor_v3.png`
- 원 출처: `C:/Users/godho/Documents/Codex/simul/art_references/simul_polished_soft_render_vn_style_anchor_v3.png`
- 원 명세: `C:/Users/godho/Documents/Codex/simul/ART_STYLE_GUIDE.md`, `C:/Users/godho/Documents/Codex/simul/IMAGE_GENERATION_STYLE_PROMPT.md`
- 앵커는 선, 명암 밀도, 피부·홍채·머리카락·의상 마감, 재질 대비만 전달한다. 앵커 속 한수아의 얼굴·검은 웨이브 머리·체형·교복·포즈는 다른 인물에게 복제하지 않는다.

원화와 키아트는 아래 렌더링 문법을 고정한다.

- 속눈썹·눈꺼풀·머리 다발·손가락·의상 외곽은 얇고 정돈된 짙은 유색선으로 또렷하게 잡고 피부 외곽은 더 부드럽게 연결한다.
- 얼굴과 눈에 가장 높은 디테일을 배정한다. 홍채는 어두운 링, 인물 고유색 그라데이션과 작고 정돈된 다층 하이라이트를 가진다.
- 피부는 밝고 깨끗한 바탕, 부드러운 웜 섀도, 절제된 홍조와 작은 반사광으로 볼륨을 만든다. 사진식 모공과 평평한 2단 셀 명암을 모두 금지한다.
- 머리카락은 읽기 쉬운 큰 다발, 어두운 깊이층, 가는 머릿결과 좁은 윤광 밴드를 함께 사용한다.
- 의상은 봉제선, 얕은 주름, 가장자리 명암과 재질 차이를 읽을 수 있게 마감한다.
- 사진 합성, 실사 피부, 3D 인형 렌더, 균일한 굵은 검정선, SD 치비, 하드 셀 2단 명암, 흐린 수채 번짐을 사용하지 않는다.

## 런타임 도트 번역 정본

공식명은 **Family Company SIMUL-v3 isometric pixel translation v1**이다. SIMUL v3 원화를 축소 복사하는 방식이 아니라, 같은 선·색층·재질 대비를 실제 이동과 사무실 배치에 읽히는 고해상도 도트 문법으로 번역한다.

- 캐릭터는 약 4~4.5등신의 귀여운 게임 비율을 쓰되 유아형 초치비로 만들지 않는다.
- 윤곽선은 순검정 한 색이 아니라 짙은 갈색·남색·청록 계열의 통제된 픽셀 선을 사용한다.
- 피부, 머리, 천, 플라스틱, 금속, 나무는 각각 다른 밝기층과 제한된 한 픽셀 하이라이트로 구분한다.
- 픽셀 군집이 선명해야 하며 블러, 반투명 에어브러시, 과도한 디더링, 생성형 잔노이즈를 금지한다.
- 고동작 캐릭터 정본은 인물별 A/B 2장이다. 각 장은 1536×1024, 6열×4행이며 A는 `남·남서·서·북서`, B는 `북·북동·동·남동`, 각 행은 접지·하강·통과를 좌우로 반복하는 걷기 6프레임이다.
- 생성 시트의 시각 간격은 셀 경계와 다를 수 있으므로 24개 실제 실루엣을 검출하고 상체 중심·발 기준선으로 정렬한 256×256 단일 프레임을 런타임에 사용한다. 구형 4×2 시트는 정체성·카메라 참조용 레거시로만 보존한다.
- 런타임 투명 PNG는 Point 필터, mipmap 없음, 무압축, 180 PPU를 기본으로 한다.
- 도트 크로마 원본은 피사체에 없는 마젠타를 쓰고, 픽셀 가장자리는 하드 키 제거로 알파 0/255를 유지한다.

사무실 모듈은 아래 규칙을 추가로 고정한다.

- 2:1 등각 3/4 탑다운 시점, 동일한 크기 감각, 동일한 좌상단 광원과 바닥 접점을 사용한다.
- 팔레트는 꿀빛 목재, 크림색 플라스틱·금속, 먼지 낀 민트, 절제된 청록 화면, 작은 복숭아색 포인트다.
- CRT, 유선 전화, 팩스·복사기, 서류철, 금속 캐비닛처럼 2000년 한국 소형 사무실 소품을 사용한다.
- 현대식 평면 모니터, 노트북, 스마트폰, LED 가구, 읽을 수 있는 브랜드·문서를 넣지 않는다.
- 모듈은 `OfficeGrid`의 의미 footprint와 placement anchor를 가져야 한다. 시각 Sprite, path/occupancy, 상호작용 socket과 저장이 같은 배치를 가리키며 겹침은 editor validation에서 거부한다.

### ImageGen 입력 순서

1. Image 1은 프로젝트 내 SIMUL v3 앵커를 **화풍 전용 참조**로 넣는다.
2. 인물은 Image 2에 승인 정체성 또는 디자인 참조를 넣는다.
3. 이동 시트는 Image 3에 현재 승인된 4×2 시트를 **배치·카메라·픽셀 밀도 전용 참조**로 넣는다.
4. 프롬프트에 `Image 1에서는 렌더링 문법만 가져오고 한수아의 얼굴·머리·체형·복장·포즈를 복제하지 않는다`를 항상 적는다.
5. 사무실은 SIMUL v3 앵커, 현재 사무실 도트 타깃, 가족회사 타이틀의 시대 소품을 각각 화풍·공간·소품 참조로 분리한다.

### 현재 타일 사무실 정본

- `Prototype01`은 `StarterOfficeV1`의 13×13 의미 격자를 Unity Isometric Tilemap으로 렌더한다.
- 바닥은 320×160·180 PPU, 가구와 벽은 hard alpha·Point·균등 scale을 사용한다.
- `OfficeVisualV2` 통짜 PNG와 3D collider/waypoint 화면은 폐기된 렌더 경로이며 fallback 정본이 아니다.
- 사무실 편집기는 Sprite Transform이 아니라 의미 footprint/anchor를 편집하고 같은 상태를 저장·경로·상호작용에 전달한다.

## 화면 규칙

- 카메라: 직교 투영, 약 45도 회전, 플레이어 추적
- 렌더: final backbuffer native resolution, Point, pixel snap. 저해상도 중간 버퍼 확대와 전체 화면 sharpening은 사용하지 않는다.
- 캐릭터: 8방향, 방향별 6프레임 걷기, 상체 중심과 발 위치를 기준으로 정렬
- 공간: 따뜻한 나무 바닥, 크림·민트 벽, 복숭아색과 청록 포인트
- 형태: 둥글고 귀엽지만 통로와 상호작용 지점은 명확하게 읽힌다.
- 시대감: 2000년대 초반 한국풍. 베이지 CRT, 유선 전화, 팩스·프린터, 종이 서류를 사용한다.
- UI와 월드 아트는 분리한다. 생성된 비주얼 타깃을 배경 한 장으로 깔지 않는다.

## 타이틀 화면 규칙

- 타이틀은 1920×1080 reference 키아트와 440×481 compact 키아트를 반응형으로 선택한다. 16:10을 포함한 다른 화면비에서도 안전 영역과 UI를 잘라내지 않는다.
- 키아트는 왼쪽에 제목과 세로 메뉴가 올라갈 어두운 안전 영역을 둔다.
- 타이틀 키아트는 런타임 월드 배경이 아니라 시작 화면에서만 사용한다.
- 이미지에 제목, 버튼, 저장 정보 같은 조작 UI를 굽지 않는다. 모든 글자와 입력 상태는 Unity UI가 그린다.
- 타이틀도 CRT, 유선 전화, 팩스, 플로피디스크 등 2000년 시대 소품과 크림·민트·청록·복숭아 팔레트를 따른다.

## 비주얼 타깃

- Assets/Art/StyleTargets/office_isometric_pixel_target_v1.png
- 용도: 배치 밀도, 팔레트, 가구 형태, 통로 폭의 기준
- 상태: STYLE TARGET. 런타임 배경으로 직접 사용하지 않는다.

## 타일 사무실 가구 T4

- `Assets/Art/Office/Tiles/Furniture/Source/`의 12종은 OpenAI 내장 ImageGen으로 각각 독립 생성한 단일 소품 원본이다. 구형 `office_module_atlas_4x3_v1.png`에서 잘라낸 조각은 런타임 정본으로 사용하지 않는다.
- 대상은 책상+CRT·회전의자·접수대·회의 탁자·문서 책장·팩스/복사기·정수기·소파·커피 테이블·화분·파티션·서류 캐비닛이다.
- 생성 규칙은 한 이미지에 한 물체, 2:1 등각 카메라, 좌상단 조명, 2000년대 한국 소형 사무실, 크림·우드·민트·복숭아 팔레트, 외곽 12~18% 안전 여백, 글자·로고·사람·바닥·이웃 소품 없음이다.
- 생성 원본의 `#ff00ff` 배경은 공식 크로마 제거 도구로 투명화한다. 런타임은 `Runtime/`의 640×512 RGBA 하드 알파, 180 PPU, Point, mipmap 없음, 무압축 Sprite를 사용한다. 원본 visible bounds에는 X/Y가 같은 최근접 이웃 배율만 적용하며, 종류별 실제 ground anchor를 Sprite pivot으로 사용한다.
- 회전의자 정본은 `office_swivel_chair_v3.png`다. 네 좌석이 향하는 `NorthWest` 기준으로 좌석의 열린 앞쪽은 CRT가 있는 좌상단, 등받이는 인물 뒤쪽인 우하단에 있어야 한다. 승인 좌판 중심은 runtime canvas `(313.007, 153.549)`이며 desk operator seat socket과 2px 이내로 합성한다. 좌판과 등받이 대부분은 캐릭터 뒤 base에 두고, `office_swivel_chair_front_v3.png`는 등받이의 제한된 전면 가장자리와 근접 팔걸이만 인물 위에 그린다. 고정 Y cutoff, 좌판·몸통을 덮는 넓은 전경, 의자 숨김은 금지한다.
- CRT 업무책상 정본은 `office_workstation_v4.png`다. 바닥까지 내려오는 넓은 막음판을 쓰지 않고, 분리된 네 다리와 서랍장 아래의 바닥 틈이 보여야 한다. 실제 바닥선에는 작은 발만 닿아야 하며 책상이 바닥에 박혀 보이는 실루엣은 실패다.
- 착석 중에는 `office_workstation_front_v4.png`가 하체 앞의 책상 모서리·앞판만 담당한다. 모니터·얼굴·머리를 덮거나 책상 전체를 항상 앞/뒤로 두면 실패다.
- runtime canvas의 footprint는 ground 한 점 추정이 아니라 승인된 네 점 폴리곤이다. 1×1·2×1 모두 320×160 등각 타일 투영 네 모서리와 각 점 2px 이내여야 하며, 비균등 확대나 종류별 런타임 위치 보정으로 맞추지 않는다.
- 가족의 착석 anchor는 256×256 프레임에서 실제 pelvis와 실제 손을 클릭해 member/direction/clip/frame별로 저장한다. 수치 통과를 위해 신체 밖의 가상 hand/pelvis 좌표를 쓰지 않는다. 승인 항목은 source Sprite SHA-256을 함께 저장하며, v5는 네 가족의 `NorthWest` SitDown 4 + Work 6 + StandUp 4, 총 56장을 승인한다.

## 플레이어 도트 정본

- `Assets/Art/Characters/Player/Pixel/HighMotion/player_pixel_walk8dir6_{a,b}_v1.png`
- 14살 플레이어의 짧고 헝클어진 짙은 갈색 머리, 갈색 눈, 흰색 후드 윈드브레이커, 남색·노랑·빨강 줄무늬 티셔츠, 짙은 남색 바지, 흰색·남색 운동화를 고정한다. 모자는 쓰지 않는다.
- 8방향×6프레임이며 파란 캡슐 placeholder와 구형 4방향 런타임을 대체한다.

## 누나 도트 정본

- `Assets/Art/Characters/OlderSister/Pixel/HighMotion/older_sister_pixel_walk8dir6_{a,b}_v1.png`
- 8방향, 방향별 걷기 6프레임
- 기존 20살 누나의 양갈래, 리본, 청록색 눈, 나시티, 돌핀팬츠, 맨발 정본을 유지한다.

## 부모 원화·도트 정본

- 아빠 원화: `Assets/Art/Characters/Father/father_office_neutral_v1.png`
- 아빠 도트: `Assets/Art/Characters/Father/Pixel/HighMotion/father_pixel_walk8dir6_{a,b}_v1.png`
- 엄마 원화: `Assets/Art/Characters/Mother/mother_office_neutral_v1.png`
- 엄마 도트: `Assets/Art/Characters/Mother/Pixel/HighMotion/mother_pixel_walk8dir6_{a,b}_v1.png`
- 부모는 플레이어·누나보다 성숙한 얼굴 비율과 체형을 유지하며, 아빠 46살·엄마 44살의 나이가 읽혀야 한다.

## 직원 후보 도트 정본

- 루트: `Assets/Art/Characters/Employees/`
- 대상: 김서아·이지안·최이서·정아린·박하은·한수아·오지우·윤채아
- 각 `Portraits/`의 `simul` 정본 원화 9종은 변경하지 않는다.
- 각 `Pixel/HighMotion/<id>_pixel_walk8dir6_{a,b}_v1.png`는 원화의 얼굴, 머리, 복장, 대표 소지품을 유지한 런타임 번역이다.
- 공통 셀 순서와 Point·180 PPU·하드 알파 규칙은 다른 가족 도트와 같다.

## 사무실 도트 모듈 레거시 입력

- 아틀라스: `Assets/Art/Office/Pixel/office_module_atlas_4x3_v1.png`
- 개별 Sprite: `Assets/Art/Office/Pixel/Modules/`
- 12종: CRT 업무책상, 회전의자, 접수대, 4인 회의탁자, 서류장, 팩스·복사기, 정수기, 2인 소파, 커피탁자, 화분, 유리 파티션, 4단 캐비닛
- 상태: LEGACY TOOLCHAIN INPUT, NOT RUNTIME CANON. 현재 runtime 가구 정본은 `Assets/Art/Office/Tiles/Furniture/Runtime/`과 `OfficeFurnitureVisualCatalog` v3이다.
- atlas와 `Modules/`는 Editor builder/검증 코드가 참조하므로 참조 제거와 도구 교체 전에는 삭제하지 않는다.

## Starter Office 이동 방향·배치 표현 규칙

- 정규 걷기 방향 순서는 South, SouthWest, West, NorthWest, North, NorthEast, East, SouthEast다. 화면 XY의 실제 displacement만 이 순서를 결정한다.
- 걷는 프레임의 방향은 실제 이동 벡터를 최근접 45도 방향으로 양자화한 결과만 사용한다. 의미 입력, 경로 lookahead, 충돌 전 원래 방향으로 실제 이동과 다른 앞·뒤·옆 Sprite를 유지하지 않는다.
- 장애물에 막히거나 상호작용 위치에 도착해 실제 변위가 0인 경우에는 보행 프레임을 진행하지 않는다. 요청 방향이 다르면 발을 고정한 제자리 pivot으로 몸 방향만 바꾸며 180°는 인접 45° 방향을 순서대로 거친다.
- 6프레임 walk cycle은 모든 가족과 직원이 공유하는 stride calibration과 누적 실제 이동거리로 구동한다. 속도, 배속, 렌더 FPS가 달라져도 같은 이동거리의 발 위상은 같고 member별 보폭 수치는 두지 않는다.
- 가족 네 명의 8방향 정본은 `HighMotionDirectionManifest.asset`의 source→canonical 순열과 사람 승인 체크로 관리한다. Runtime에서 memberId별 방향 교체 코드를 두지 않는다.
- 방향 승인판은 화살표·방향 이름·파일 이름·현재 Sprite를 한 칸에 함께 표시한다. 정본 산출물은 `Artifacts/StarterOfficeDirectionQa/office-character-direction-contact-sheet.png`다.
- 의자·책상 그림은 semantic placement anchor와 다른 위치로 자동 보정하지 않는다. 잘못된 chair-seat/desk-seat/pelvis/hand 정렬은 Editor에서 오류로 보여 주고 배치 또는 아트 캘리브레이션을 수정한다.
- 착석 자세 보정은 실제 pelvis/hand 점을 바꾸거나 신체 밖 가상 점을 만들지 않는다. `OfficeCharacterSeatPoseCatalog` v5는 translation만 허용하며 `VisualRoot.localRotation=identity`, pose scale `1.0`을 강제한다. Work pelvis는 cushion에 고정하고 SitDown/StandUp은 승인된 해부학 점을 사용해 서기↔좌판 구간을 보간한다.
- 실제 업무 화면은 사람 승인·SHA 일치가 확인된 `OfficeSeatingV1` `NorthWest` 14프레임을 사용한다. 방향이 좌석 facing과 불일치하는 Legacy micro-action, 미승인 프레임, 다른 프레임으로의 fallback을 Starter Runtime에 섞지 않는다. 56개 완전성이 깨지면 승인된 Work/0 정적 자세로만 닫힌다.
- 가구 footprint와 interaction cell은 보이는 Sprite의 장식이 아니라 게임 공간의 표현이다. 새 가구는 기본 차단이며 투명 픽셀이나 sorting order로 관통을 숨기지 않는다.

## Starter Office perimeter wall (2026-08-14)

- 재질은 밝은 저채도 회청색 plaster panel과 matte white top/base trim이다. 갈색 목재, 울타리 slat, 굵은 반복 post, gate, 금속 난간을 사용하지 않는다.
- far 두 면은 사무실 경계를 읽을 수 있는 full height, 카메라에 가까운 두 면은 캐릭터와 가구를 가리지 않는 낮은 cutaway height다. 두 높이는 같은 색·trim·얇은 panel seam 문법을 공유한다.
- 각 bay는 투명 배경의 정확한 한 타일 `(+160,+80)` screen span이며, SouthEast/SouthWest mirror가 네 L corner에서 틈이나 한 타일 overshoot 없이 만난다.
- 출입구는 한 bay 전체가 열린 negative space와 exterior-side thin threshold만 가진다. door leaf, jamb, header/lintel, 손잡이, swing arc, 닫힌 문 silhouette는 금지한다.
- runtime 규격은 640×512 RGBA, hard alpha, 180 PPU, Point/nearest, mipmap 없음, uncompressed다. 기존 source/runtime GUID와 `.meta`를 유지한다.

## 월드 도트 샘플링과 화면 선명도 (2026-08-14)

- 월드 도트는 최종 backbuffer의 native resolution에서 그린다. 고정 360/540p 중간 버퍼를 Point로
  확대하거나 전체 화면 sharpening으로 외곽을 인위적으로 키우지 않는다.
- 바닥·가구·벽·캐릭터는 Point, mipmap off, Standalone uncompressed, 원본 크기 보존, 180 PPU다.
  Painted/high-resolution UI와 키아트는 이 규칙의 대상이 아니며 Bilinear를 유지한다.
- 카메라와 이동 캐릭터는 presentation 단계에서 물리 픽셀 grid에 맞춘다. 같은 선이 이동 중 번갈아
  굵어지거나 얼굴·발이 반 픽셀에서 녹는 것이 실패 기준이다.
- 전체 사무실 fit에서는 1920×1080의 source pixel 대비 화면 비율이 약 0.426이므로 눈·전화선·키보드
  키처럼 원래 1 화면 픽셀 미만인 세부에는 소스/구도 한계가 남는다. 이를 이유로 PNG를 자동 업스케일·
  재생성하지 않고 필요한 경우 영향 자산을 별도 아트 작업으로 승인한다.
