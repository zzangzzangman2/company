# ASSET MANIFEST

최종 갱신: 2026-08-13

## Starter office entrance door (2026-08-13)

- Source: `Assets/Art/Office/Tiles/Furniture/Source/office_entrance_door_alpha_v1.png`
- Runtime: `Assets/Art/Office/Tiles/Furniture/Runtime/office_entrance_door_v1.png`
- Status: CANONICAL GENERATED OFFICE FURNITURE ASSET
- Creation: OpenAI built-in ImageGen, generated specifically for this project as a transparent-background isometric pixel-art wooden office entrance door with frosted glass and warm tycoon-game rendering, then edited with the same tool into a clearly open 70-degree state so actors traverse a visible doorway.
- Source SHA-256: `963F3866977198FDE51544A710337A0A3A96BE280E6376FE3C6C02526F011BF2`
- Runtime SHA-256: `E5AB08357E9211B3A446E814D69BCE5820298569ED3FED3A86F6A37D5D8C04F1`
- Runtime ownership: `OfficeFurnitureAssetBuilder` scales the source to the semantic 1x1 footprint, preserves alpha, and writes the Unity sprite metadata. The door is nonblocking because the attendance actor traverses its single canonical cell `(8,1)`.
- Rights: project-owned generated asset; no third-party source material.

## 권리 선언

사용자는 아래 기존/파생 이미지가 GPT 생성 에셋이며 자신이 사용 권리를 보유한다고 명시했다.

## 공식 화풍 앵커

- 경로: Assets/Art/StyleTargets/References/simul_polished_soft_render_vn_style_anchor_v3.png
- 상태: CANONICAL STYLE-ONLY REFERENCE
- 용도: `SIMUL polished soft-render VN anime v3`의 선·명암·피부·홍채·머리·의상 재질 마감 기준
- 금지: 앵커 속 한수아의 얼굴·머리·체형·교복·포즈를 다른 인물에게 복제하지 않는다.
- SHA-256: 7C07FD2DCA957375B21DAC887CFBBF8865AA36AE6A07E1A09B6919FFCE64866A
- 출처: 사용자 소유 `simul` 프로젝트의 승인 화풍 앵커를 복사해 프로젝트 안에 영구 보존

## 플레이어

### 레거시 도트 이동 시트

- 경로: Assets/Art/Characters/Player/Pixel/player_pixel_walk4x2_v1.png
- 상태: LEGACY IDENTITY REFERENCE
- 용도: 구형 14살 플레이어의 정체성·카메라 참조. 현재 런타임에는 사용하지 않는다.
- 규격: 1536x1024 RGBA, 4열x2행, 알파 0/255, 네 모서리 투명
- 방향: 정면, 왼쪽, 뒤, 오른쪽
- 외형: 빨간 뉴스보이캡, 짧은 짙은 갈색 머리, 갈색 눈, 흰 후드 윈드브레이커, 줄무늬 티셔츠, 짙은 남색 바지, 흰색·남색 운동화
- SHA-256: 0C23A5D9594FFED9E8263938A11F6268F133B09ECDFFC90BAD4E2545179BC4EB
- 제작: OpenAI 내장 imagegen. SIMUL v3 공식 앵커는 화풍, 기존 `simul` 타이틀은 14살 디자인, 누나 4x2 시트는 배치·픽셀 밀도 참조로 분리 사용
- 투명화: 마젠타 크로마를 픽셀용 하드 키로 제거해 빨간 모자와 피부색을 보존

### 생성 원본

- 경로: Assets/Art/Characters/Player/Pixel/Source/player_pixel_walk4x2_chroma_v1.png
- 상태: SOURCE
- SHA-256: 59EBC47052CA37D5C8EE930D6868ECA388664E4758D3F50F4C6DA9D6F1EBE728

### 레거시 Unity 생성 프레임

- 경로: Assets/Art/Characters/Player/Pixel/Frames/
- 상태: LEGACY GENERATED ASSETS
- 제작: 구형 빌더가 4x2 시트를 8개 단일 Sprite PNG로 분리했다.

## 누나

### 정본

- 경로: Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png
- 상태: CANONICAL
- 용도: 20살 누나 전신 중립 원화
- 복장: 어두운 나시티, 흰 파이핑 남색 돌핀팬츠, 맨발
- 규격: 1024x1536 RGBA, 투명 배경
- SHA-256: 4335F2025D6FA3AC7145FBA93B4447CC934D85ABFD38452A2A8F3E977A7EA0B5
- 제작: OpenAI 이미지 생성/편집 도구로 경마장 표 판매원 캐릭터 정체성을 보존해 편집, 크로마 제거 도구로 투명화

### 생성 원본

- 경로: Assets/Art/Characters/OlderSister/Source/older_sister_casual_neutral_chroma_v2.png
- 상태: SOURCE
- SHA-256: DA59648080EF37A77D2C812EC5266655E10741F7B32C0BB250F42274F14F88BE

### 비정본 시안

- 경로: Assets/Art/Characters/OlderSister/older_sister_office_neutral_v1.png
- 상태: DEPRECATED CONCEPT, 런타임 사용 금지
- 이유: 사용자가 나시티·돌핀팬츠·맨발로 의상을 교체했다.
- SHA-256: 52C428A0D64F92BF609ABEF78870E63500F9C2E8B96B97DFC859D8F447564C28

### 캐릭터 정체성 참고 원화

- Assets/Art/Characters/OlderSister/References/teller_window_welcome_age20_v1.png
- Assets/Art/Characters/OlderSister/References/teller_window_bet_guide_age20_v1.png
- Assets/Art/Characters/OlderSister/References/teller_window_bet_accept_age20_v1.png
- Assets/Art/Characters/OlderSister/References/teller_window_ticket_handover_age20_v1.png

### 레거시 도트 이동 시트

- 경로: Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v2.png
- 상태: LEGACY IDENTITY REFERENCE
- 용도: 20살 누나의 정체성·카메라 참조. 현재 런타임에는 사용하지 않는다.
- 규격: 1536x1024 RGBA, 4열x2행
- 방향: 정면, 왼쪽, 뒤, 오른쪽
- 복장·정체성: 긴 검은 양갈래, 검은 리본, 청록색 눈, 나시티, 돌핀팬츠, 맨발
- SHA-256: FFC6B721043C51A069DCDD4BC7137DC402B6239C244ABD56A1512FBE4F3C5F7C
- 제작: OpenAI 내장 이미지 생성/편집 도구로 전신 정본의 정체성을 보존해 도트화
- 투명화: 설치된 chroma-key 제거 도구를 사용해 v2에서 어두운 녹색 잔상을 제거

### 도트 생성 원본

- 경로: Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_chroma_v1.png
- 상태: SOURCE
- SHA-256: 49CE3AB3194A0646BEF3ABFDBC3765F7DF6DD9F657249025ED05A69A6D526E3B

### 도트 비정본

- 경로: Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v1.png
- 상태: DEPRECATED, 런타임 사용 금지
- 이유: 생성 크로마의 어두운 녹색 잔상이 남아 v2로 교체했다.

### 레거시 Unity 생성 프레임

- 경로: Assets/Art/Characters/OlderSister/Pixel/Frames/
- 상태: LEGACY GENERATED ASSETS
- 제작: 구형 Office V0.2 빌더가 4x2 시트를 8개 단일 Sprite PNG로 기계적으로 분리했다.
- 규칙: 직접 수정하지 않고 정본 시트나 빌더를 수정한 뒤 재생성한다.

## 사무실 비주얼 타깃

- 경로: Assets/Art/StyleTargets/office_isometric_pixel_target_v1.png
- 상태: STYLE TARGET, 런타임 배경 사용 금지
- 용도: 귀여운 2.5D 도트 사무실의 팔레트, 가구 밀도, 통로, 구역 구성 기준
- SHA-256: 8BCFC0D6B32A03324697346390F839654EEF262A5EB126F887E09760F80DB901
- 제작: 사용자가 제공한 두 사무실 화면은 공간 구성 참고로만 사용하고, OpenAI 내장 이미지 생성 도구로 독자적인 2000년대 초반 가족회사 사무실을 생성했다.

## 사무실 도트 모듈

### 정본 아틀라스

- 경로: Assets/Art/Office/Pixel/office_module_atlas_4x3_v1.png
- 상태: CANONICAL PROP ATLAS V1
- 용도: 3D 충돌 모듈 위에 교체·배치할 2000년 한국풍 등각 사무실 소품
- 규격: 2048x1024 RGBA, 4열x3행, 알파 0/255, 네 모서리 투명
- 내용: CRT 업무책상, 민트 회전의자, 접수대, 4인 회의탁자, 서류장, 팩스·복사기, 정수기, 복숭아색 소파, 커피탁자, 화분, 유리 파티션, 4단 캐비닛
- SHA-256: F03B7D7CFA6CB0BC51D7DCB4ADB2BFD5B455BC5FAD75D399FA1FF27EB6D62CB8
- 제작: OpenAI 내장 imagegen. SIMUL v3 앵커는 렌더링 문법, 사무실 도트 타깃은 등각 시점·팔레트, 가족회사 타이틀은 2000년 소품 참조로 분리 사용
- 투명화: 마젠타 크로마를 픽셀용 하드 키로 제거

### 생성 원본

- 경로: Assets/Art/Office/Pixel/Source/office_module_atlas_4x3_chroma_v1.png
- 상태: SOURCE
- SHA-256: 6EB68B29C967DA52C321DA942AF96FF4DB53B4912FFBCEE95C7CFD5A9153722B

### 개별 Unity Sprite

- 경로: Assets/Art/Office/Pixel/Modules/
- 상태: GENERATED CANONICAL MODULES
- 개수: 12
- 제작: 빌더가 4x3 아틀라스를 셀별 PNG로 분리한다. 원본 높이 1024가 3으로 나누어지지 않으므로 행 경계를 비율 반올림해 모든 픽셀을 손실 없이 분배한다.

## 메인 타이틀

- 경로: Assets/Art/UI/Resources/Title/family_company_title_hero_v1.png
- 상태: CANONICAL TITLE HERO V1
- 용도: 1920×1080 가로 시작 화면의 aspect-fill 배경
- 규격: 1672×941 RGB
- SHA-256: 501DBCAF14CFE4F6677E8D9D022645F606AD0227CFFA8FD9CBD3A460A8F35B26
- 내용: 2000년 한국풍 소형 사무실, 20살 누나, CRT·유선 전화·팩스·플로피·종이 계약서
- 정본: 누나의 긴 검은 양갈래, 검은 리본, 청록색 눈, 나시티, 돌핀팬츠, 맨발을 유지한다.
- 제작: OpenAI 내장 imagegen. 기존 `simul` 타이틀 원화는 화면 에너지와 완성도 참고, 누나 정본 원화는 캐릭터 정체성 참고로 사용했다.
- 프롬프트 핵심: 왼쪽 38%를 어두운 UI 안전 영역으로 비우고 오른쪽에 누나와 사무실을 배치하며, 이미지 안 글자·로고·버튼·워터마크를 금지했다.

### Money Rain 돈다발 레이어

- 상태: **ACTIVE OVERLAY · 폐기된 무인 사무실 배경은 제거**
- 루트: `Assets/Art/UI/Resources/Title/MoneyRain/`
- 정리: 새 게임 전환 중 노출되던 미사용 `money_rain_office_background_v1.png`와 `.meta`는 2026-08-13 삭제했다. 런타임·씬·빌드 참조는 0이다.
- 투명 돈다발: `money_bundle_mint_v1.png`, `money_bundle_coral_v1.png`, `money_bundle_sky_v1.png`; 각 1024×1024 RGBA, 실제 알파 0~255
- 돈다발 SHA-256: 민트 `DCF0F18332E94849C159CF50FB924441B894BCF0F5E992AAA5AEBCDF2CC05A79`, 코랄 `13FD656B1C4192D2E99A86A984DE4BC7CC7506DE1151B605662505F7D534880F`, 하늘 `A555448A25BA7E873E07E68E0BB625DF6F84BF2F11196B8031C29D3C647789EB`
- 시각 확인 GIF: `family_company_title_money_rain_v1.gif`, 960×540, 28프레임×100ms, 2.8초 무한 루프, SHA-256 `3F4630BCBC51740368E62B67C5E5F10A00058AC92AA57FC8311657B6B96F4AF4`
- 제작: OpenAI 내장 ImageGen으로 사람 없는 2000년 한국 소형 사무실과 가상 원화 돈다발 3종을 생성했다. 지폐는 실제 초상·액면·문구·국가 상징·로고를 복제하지 않은 안전한 게임 일러스트다.
- 후처리: 배경은 중앙 16:9 크롭·Lanczos로 1920×1080 정규화했다. 돈다발은 생성 이미지에서 가장자리 연결 배경만 분리해 실제 투명 알파로 만들었다.
- 구도·UI: 왼쪽 40%와 좌하단은 기존 Unity 제목·버튼용 저복잡도 안전 영역이다. 이미지 안 제목·숫자·버튼·로고·워터마크는 없다.
- 애니메이션: 하늘색은 뒤, 코랄은 중간, 민트는 앞 깊이로 크기·그림자를 달리하고, 닫힌 대각 타원 경로와 사인 회전으로 시작/끝 속도를 연속시켰다. GIF는 시각 확인용이며 Unity에서는 배경과 투명 PNG 3장을 저비용으로 별도 애니메이션한다.
- QA: 사람·찌그러진 누나·인물 잔상 0, 읽을 수 있거나 글자처럼 뭉친 표기 0, 카지노/검정·금색 고급 분위기 0. 루프 경계 프레임 RMS 17.910, 일반 인접 프레임 중앙값 16.845로 경계 점프가 일반 이동량과 같은 수준이다.

### Money Rain Tycoon 타이틀 V2

- 상태: **ACTIVE TITLE BACKGROUND · 돈다발 원본 보존**
- 경로: `Assets/Art/UI/Resources/Title/MoneyRain/money_rain_tycoon_background_v2.png`
- 규격: 1672×941 RGB, 16:9
- SHA-256: `3F65163581951FB92DB72C13CE0850AF37F7E2CC526B4390F3AC7A0BD82B9933`
- 내용: 왼쪽 42% 저복잡도 짙은 청록 메뉴 영역, 오른쪽 주인공·누나·아빠·엄마가 CRT 조사·서류 운반·전화 응대·회계를 수행하는 2000년 등각 도트 가족 사무실
- 정본: 가족 4인의 승인 도트 외형과 연령·복장을 유지한다. 누나는 맨발이며 주인공·엄마 CRT의 화면과 키보드는 앉은 사람을 향한다.
- 제작: OpenAI 내장 ImageGen. Image 1은 SIMUL v3 화풍 앵커, Image 2는 등각 사무실 도트 타깃, Image 3은 가족 4인 승인 이동 Sprite contact sheet를 각각 화풍·공간·정체성 참조로 분리했다. 최초 생성 뒤 누나 신발 제거와 주인공·엄마 CRT 방향 교정을 `precise-object-edit`로 수행했다.
- 후처리: 최종 편집 출력의 오른쪽 바깥 1px을 동일한 최외곽 열로 연장해 원래 타이틀 규격 1672×941 RGB로 비파괴 정규화했다.
- 프롬프트 핵심: 정확한 16:9, 오른쪽 58%의 실제 업무 장면, 왼쪽 42% UI 안전 영역, CRT·유선 전화·팩스·바인더·종이 서류, 글자·로고·버튼·돈·워터마크 금지.
- 런타임: `TitleMoneyRainRenderer`가 이 배경을 aspect-fill한 뒤 기존 민트·코랄·하늘 돈다발 12개를 `Time.unscaledTime` 2.8초 폐루프로 그린다. 제목·메뉴·저장 상태는 Unity IMGUI가 별도 렌더한다.

### Money Rain Tycoon 세로 확장 V3

- 상태: **ACTIVE COMPACT TITLE BACKGROUND · V2 가로 배경 보존**
- 경로: `Assets/Art/UI/Resources/Title/MoneyRain/money_rain_tycoon_background_portrait_v3.png`
- 규격: 1195×1316 RGB, compact 10:11 대응
- SHA-256: `EA5E55A29A9F7B4B159E32F035CA8DBC2C8AEF23B07BFA58459F62E1998895D7`
- 제작: OpenAI 내장 ImageGen `precise-object-edit`. V2를 편집 대상으로 사용해 중앙 가족 사무실은 유지하고 위쪽 벽·창문과 아래쪽 바닥·식물 영역만 자연스럽게 확장했다.
- 정본: 가족 4인, 누나 맨발, 주인공·엄마의 사람 방향 CRT/키보드를 유지한다. 왼쪽은 작은 세로 메뉴용 저복잡도 영역이며 이미지 안 UI·문구·돈다발은 없다.
- 런타임: 가로세로비 1.35 미만에서 `TitleMoneyRainRenderer`가 이 이미지를 aspect-fill한다. 440×481에서 위아래 검은 레터박스 없이 화면 전체를 채운다.

## 부모 캐릭터

### 아빠

- 정본 원화: Assets/Art/Characters/Father/father_office_neutral_v1.png
- 상태: CANONICAL FULL-SCREEN CHARACTER
- 규격: 1024×1536 RGBA, 투명 배경
- SHA-256: B7E881209750A4136BC8BC6E17B0B5E62987B0A92BB831B4FF7EFA9CA84C8ED9
- 원화 생성 원본: Assets/Art/Characters/Father/Source/father_office_neutral_chroma_v1.png
- 원화 생성 원본 SHA-256: BF9E16DB54D6BD70B21B6FCBC55D04F9DBA1FD80D092691858E589C00790EBB0
- 런타임 도트: Assets/Art/Characters/Father/Pixel/father_pixel_walk4x2_v1.png
- 런타임 도트 SHA-256: C27C4E033570DDA9D97882779FDB1EBD3ADC79CBBDFDE5086C27E02AD431700C
- 도트 생성 원본: Assets/Art/Characters/Father/Pixel/Source/father_pixel_walk4x2_chroma_v1.png
- 도트 생성 원본 SHA-256: 5852AB77028D00F4A767DEE3D36A6532CC54F9F0995800A1E13AEFEBA0F4DF1B
- 제작: OpenAI 내장 imagegen으로 46살 역할·연령과 SIMUL v3 화풍을 반영하고, 크로마 제거 도구로 원화와 도트를 투명화했다.

### 엄마

- 정본 원화: Assets/Art/Characters/Mother/mother_office_neutral_v1.png
- 상태: CANONICAL FULL-SCREEN CHARACTER
- 규격: 1024×1536 RGBA, 투명 배경
- SHA-256: A92FDABF1ABE5ECC6ACF9E0FC8149F084170B5F3A3BE853F837C1AAEB40C4843
- 원화 생성 원본: Assets/Art/Characters/Mother/Source/mother_office_neutral_chroma_v1.png
- 원화 생성 원본 SHA-256: 882FBDE9A4FD50BB9011A4B05BB2C341F76EA604981BCAFEC19B34EED8C7CAC6
- 런타임 도트: Assets/Art/Characters/Mother/Pixel/mother_pixel_walk4x2_v1.png
- 런타임 도트 SHA-256: F1B39CB2F5BD1BB0E464F0F31B46AD72224DCDD30BEFCBDB34A6D3DAF5912845
- 도트 생성 원본: Assets/Art/Characters/Mother/Pixel/Source/mother_pixel_walk4x2_chroma_v1.png
- 도트 생성 원본 SHA-256: 8AF204E2290267C9C554193FFF8D125DBC334BA9556C10E1C59F6F70FB1D7E6D
- 제작: OpenAI 내장 imagegen으로 44살 역할·연령과 SIMUL v3 화풍을 반영하고, 크로마 제거 도구로 원화와 도트를 투명화했다.

### 부모 Unity 프레임

- 경로: Assets/Art/Characters/Father/Pixel/Frames/ 및 Assets/Art/Characters/Mother/Pixel/Frames/
- 상태: GENERATED RUNTIME ASSETS
- 개수: 16
- 규격: 인물별 정면·왼쪽·뒤·오른쪽 × 걷기 A·B

## 향후 직원 후보 8인

- 루트: Assets/Art/Characters/Employees/
- 상태: CANONICAL FUTURE EMPLOYEE ART POOL
- 인물: 김서아, 이지안, 최이서, 정아린, 박하은, 한수아, 오지우, 윤채아
- 전신 원화: 인물별 `Portraits/` 9종, 총 72종. `simul/flutter_app/assets/images/production_soft_painted/` 정본과 SHA-256 72/72 일치하는 무변형 복사본.
- 정체성 참고: 인물별 `References/` 1~2종, 총 11종. `simul/art_references/` 정본과 SHA-256 11/11 일치하는 무변형 복사본.
- Unity 프레임: 인물별 `Pixel/Frames/` 8종, 총 64종.

| 인물 ID | 정본 도트 시트 SHA-256 | 크로마 생성 원본 SHA-256 |
|---|---|---|
| `kim_seoa` | 4973466BB2C17C809FDFF9BF7E78DED74CDEAAA1CEE371F4B1F66C3C38D99756 | B3F4F89501259E67F9972DAF0DE5980D86B74942A2A262829AD1677D20A60BFA |
| `lee_jian` | F52395241D4D3823D4CA1ED60A1EA4F73941AA7551BAA66A9FAAC11A8CF1167E | 9E39B284A4BDF128DC5D2421792327B1710979ECFA4BB0F11B64978C78EAE1BB |
| `choi_iseo` | FB0E1F8280AD262BE44FDA95E45E244247EC2A88E6F1C173E6F17BF0541CAEE9 | 33D2EB9DE64D20758F9160CA691324ECA405CF54716AAD9D97C0A76DB8476A72 |
| `jung_arin` | DA7856A3C2BE6A4E9BADD45A2288BF5A2569A9451991507986963E4D0ED3F7B3 | 549D7E756F4C2CC5552E16A6F88425EB5972D91E033840333F147384F5955682 |
| `park_haeun` | 22E1309DDAB95C512C166F3CE3AB67F53E237119C98628B7FCD6BB2B755ED476 | 4A47E55617F819DE72E905A561DC661E6DEFA8B12CFE4C05093604AD8AE3FF52 |
| `han_sua` | 6948E03E3F678BE89BBCC92E89A610BE65F896E34689550E785F87C027B47656 | 8B69784126962B8AEDDD659A536A3B2C36D03B59F0BEFB1144518BBD8E7B97DD |
| `oh_jiwoo` | 6FC1EBF508241B6F7538AF391F8BF0BEEA8D88B8413F9968C742BA30C7DA76BF | DF630EA0A7CA80EA9DC351C4412FA71B8EB63C5ADD5674B836EA77EA37C59D27 |
| `yoon_chaea` | 408ADAB0F3D4529862DA5F702B0689F8C72246DFB654AB0F377D937F9CCF3643 | E0ADB576DD9D7D42FE11A9144934CFBDFD19B898009F13CEF179A9D909FADE2D |

- 도트 제작: OpenAI 내장 imagegen. 인물별 정본 원화와 정체성 앵커를 외형 참조, SIMUL v3 앵커를 화풍 참조, 누나 도트 시트를 배치·카메라 규격 참조로 분리해 사용했다.
- 투명화: 마젠타 크로마 원본을 하드 키로 제거해 최종 시트 알파를 0/255로 고정했다.

## 12인 고동작 이동 시트

- 루트: 각 인물의 `Pixel/HighMotion/`
- 대상: 플레이어·누나·아빠·엄마와 직원 후보 8인, 총 12인
- 정본: 인물별 `*_pixel_walk8dir6_a_v1.png`, `*_pixel_walk8dir6_b_v1.png` 2장
- 규격: 장당 1536×1024 RGBA, 6열×4행, 셀당 256×256, 알파 0/255
- 배치: A는 남·남서·서·북서, B는 북·북동·동·남동이며 각 방향 6프레임
- 총량: 정본 시트 24장, 이동 셀 576개
- 생성 원본: 인물별 `Pixel/HighMotion/Source/*_chroma_v1.png`
- Unity 런타임: 인물별 `Pixel/HighMotion/Frames/`의 256×256 단일 프레임 48개, Point·mipmap 없음·무압축·180 PPU·하단 중앙 피벗
- 검증: 24장 모두 1536×1024·하드 알파이며 각 장의 실제 실루엣 24개를 검출했다. 상체 중심과 발 기준선을 정규화한 576개 프레임을 원본 실루엣과 재대조했다.
- 시각 QA: 플레이어·누나·부모·직원 8인의 8방향 정체성 유지와 방향별 6단계 보행을 확인했다. 플레이어는 사용자 요청에 따라 빨간 뉴스보이캡을 제거한 **무모자 외형**이 정본이다.
- 마지막 완성분 SHA-256:
  - 한수아 A `380A46F15EB8605BE6295990BBFD05CC7A877B2DAD1CCE882AC55174F5EC8AE0`, B `5321543EB8E24D75049D6C4DD304C59637E9328D28CC129DEA9639FD1B5339E4`
  - 오지우 A `9C9A5AFAE75F1358B9B9B0A46B2DBDA6E194F75839F5CD3ED87010CF4A6BF6C4`, B `A9B5D8C1B8BBD2CCEB3FEC94B86D67815AF5A31DD76ACCF823BAF4E7843076F6`
  - 윤채아 A `06B35F154FEE2A9C3B857E3DD34BA9BC02BE564856FC10A320163CE88D0B12D8`, B `8C59A4826BEB25E9B8260EEA073E05F388B9648F9916CCEF177248B24A10DCE3`

## 가족 4인 사무실 착석 애니메이션 OfficeSeating V1

- 상태: **448/448 GENERATED · SOURCE/FRAME/META/VISUAL QA PASS · 런타임 미연결**
- 대상: 플레이어·누나·아빠·엄마, 총 4인
- 정본 루트:
  - `Assets/Art/Characters/Player/Pixel/OfficeSeatingV1/`
  - `Assets/Art/Characters/Family/OlderSister/Pixel/OfficeSeatingV1/`
  - `Assets/Art/Characters/Family/Father/Pixel/OfficeSeatingV1/`
  - `Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/`
- ImageGen source: 인물별 `Source/`의 transition A/B·work A/B 4장, 총 16장. OpenAI 내장 ImageGen으로 생성했으며 SIMUL-v3 앵커는 렌더링 문법만, 각 가족 정본과 기존 HighMotion은 정체성·카메라·픽셀 밀도·방향 전용 참조로 사용했다.
- 시트 계약: transition A/B는 각각 4열×4행, work A/B는 각각 6열×4행이다. A 행은 `남·남서·서·북서`, B 행은 `북·북동·동·남동` 순서다.
- 엄마 source 교정: 기존 `mother_office_seating_work_a_v1.png`가 3열×2행·실루엣 6개인 결함이라 억지 분할하지 않았다. 같은 엄마 정체성·의상·착석 작업 자세를 유지한 S/SW/W/NW 4행×6열·24실루엣 시트를 내장 ImageGen으로 한 장만 재생성하고 공식 chroma-key 제거 도구로 하드 알파화했다.
  - 최종 투명 시트 SHA-256: `64E1144E287107A6A839891F5C7E0DF545F3D4534DAD8B4EF1E156205D0ECD80`
  - 크로마 원본 SHA-256: `3B86E9C2C6DC899B8CD0A056C73D051AF30FD46B1C258A839E1B99509786DAD0`
  - 교정 프롬프트 핵심: 1536×1024, 셀 256×256, 정확히 4행×6열, 행 S/SW/W/NW, 열 sit_work 0~5, 의자·책상·소품·문자 없음, 균일 `#ff00ff` 배경, 엄마 외형·복장 불변.
- 개별 프레임: 인물별 8방향×`sit_down` 4 + `sit_work` 6 + `stand_up` 4 = 112장, 총 448장. `stand_up`은 승인된 `sit_down`의 정확한 역순이다.
- 출력 규격: 각 256×256 RGBA, 알파 0/255, 피사체 비어 있음·잘림 없음, 발 기준선 y=248. Unity 메타는 Sprite/Single, Point, mipmap 없음, 무압축, 180 PPU, 하단 중앙 커스텀 피벗 `(0.5, 0)`이다.
- 밀도 정규화: HighMotion 48프레임의 인물별 중앙 높이를 기준으로 한 가지 캐릭터 축척만 적용했다. work A/B 생성 시트의 밀도 차이는 큰 파트만 고정 비율로 축소해 맞췄으며 프레임별 임의 축척은 없다. 최종 캐릭터 축척은 플레이어 `1.041`, 누나 `1.038`, 아빠 `1.021`, 엄마 `1.029`; work A/B 보정은 각각 플레이어 `0.965/1.000`, 누나 `0.984/1.000`, 아빠 `0.995/1.000`, 엄마 `0.903/1.000`이다.
- 기계 분할·검증 도구: `Tools/split_office_seating_sheets.py`. 실제 실루엣을 행/열로 검출하고 상체 중앙·발 기준선을 정렬한다. 2026-08-12 이후 엄마 Northwest Work 6장은 잘린 source sheet를 다시 분할하지 않는 승인 override이므로 `--verify-only`의 source 바이트 재현 대상에서 제외하고 아래 최종 SHA로 검증한다.
- QA 산출물: 인물별 transition 8방향 접촉 시트와 work 8방향 접촉 시트 각 1장(총 8장), work 6프레임 GIF 각 1개(총 4개). 각 정본 루트의 `QA/`에 저장한다.
- 자동 QA: source 16장 크기·하드 알파·16/24 실루엣 계약, 프레임 448장·메타 448개, 비어 있음 0, 부분 알파 0, 잘림 0, 기본 동작 프레임 고유 해시 320개, 승인 역순 중복 128개, 전체 렌더 해시 320개, 프레임 GUID 448개 고유, Assets 전체 GUID 중복 0. GIF 루프 경계/일반 인접 RMS 최대 비율은 `1.442`다.
- 육안 QA: 8개 접촉 시트에서 남→남서→서→북서→북→북동→동→남동 방향, standing→seated→standing 순서, 손의 키보드·마우스 변화, 무모자 플레이어와 가족 정체성, 의자 없음, 머리·손·발 미절단을 확인했다.
- 통합 경계: 이번 작업은 아트·메타만 완성했다. 기존 HighMotion, Scene, 자율행동, Market, Save, Leisure, OfficeVisual 코드는 수정하지 않았으며 실제 좌석 상태기 연결은 후속 작업이다.

## SIMUL 오디오 무변형 이관

- 경로: `Assets/Audio/Resources/Audio/BGM/`, `Assets/Audio/Resources/Audio/SFX/`
- 상태: LICENSED RUNTIME AUDIO, BYTE-IDENTICAL IMPORT
- 수량: BGM 11개, SFX 39개, 총 50개 OGG
- 원본: 읽기 전용 `C:/Users/godho/Documents/Codex/simul/flutter_app/assets/audio/`
- 이관일: 2026-08-10
- 검증: 상대 경로별 SHA-256 50/50 일치
- 정렬 파일 집합 SHA-256: `1521D5FD63C7114C6E05368B67AC832EC0E5765525339705198AAD5F4E4CAE3C`
- BGM 권리: PeriTune / Sei Mutsuki, CC BY 4.0. 배포 표기 `Music: PeriTune <https://peritune.com/>`
- SFX 권리: Kenney CC0 1.0 및 OpenGameArt/Freesound의 개별 CC0 3종
- 상세 원장: `Docs/AUDIO_LICENSES.md`
- 편집 이력: 파일명·OGG 바이트를 변경하지 않고 복사. Unity importer와 런타임 볼륨·반복 재생만 적용한다.
- UI 경계: `simul`의 세로 모바일 UI·좌표·패널 비율은 이관하지 않았다. 기존 1920×1080 PC 가로 화면과 16:9 사무실 표시를 유지하고 오디오만 화면 상태에 연결한다.
- 크레딧 규칙: 게임 화면·타이틀·설정·엔딩에는 라이선스 문구를 노출하지 않고 릴리스 메타데이터에서 처리한다.

## SIMUL 폰트 무변형 이관

- 경로: `Assets/Fonts/Runtime/`, `Assets/Fonts/Licenses/`
- 상태: LICENSED FONT SOURCE, BYTE-IDENTICAL IMPORT
- 수량: TTF 3개, 원본 라이선스 2개
- 원본: 읽기 전용 `C:/Users/godho/Documents/Codex/simul/flutter_app/assets/fonts/`
- 이관일: 2026-08-10
- 검증: 상대 파일명별 SHA-256 5/5 일치
- 정렬 파일 집합 SHA-256: `1017B5F9AF25595E322EDA85827BEBF32FCA7D20899AA32D2B9B08D06C76D22A`
- 권리: Maplestory typeface는 NEXON Korea Corporation 고지 동봉 조건의 개인·기업 무료 사용, Pretendard는 SIL OFL 1.1
- 상세 원장: `Docs/FONT_LICENSES.md`
- 편집 이력: TTF와 라이선스 파일의 이름·바이트를 변경하지 않고 복사했다. UI 코드·씬·기존 아트는 변경하지 않았다.
- UI 경계: 세로 모바일 레이아웃은 이관하지 않으며 후속 적용은 1920×1080/16:9 PC 가로 풀화면에서 새로 설계한다.

## Leisure 장면 비주얼

- 루트: `Assets/Art/Leisure/`
- 상태: **12/12 FINAL · VISUAL QA PASS**
- 용도: 가족회사식 회복·주말 활동의 실제 장소·가족 행동을 보여 주는 가로형 장면 카드/풀화면 배경. 버튼 목록·아이콘 세트가 아니다.
- 제작: OpenAI 내장 ImageGen. 사용자의 기존 GPT 생성 에셋 권리 선언이 적용된다.
- 정본 참조: `simul_polished_soft_render_vn_style_anchor_v3.png`는 화풍만, 플레이어 최신 HighMotion A와 누나·아빠·엄마 원화는 각 인물 정체성만 참조했다.
- 플레이어 정본: 과거 빨간 뉴스보이캡 결정보다 최신 문서와 HighMotion 완료본이 우선한다. 12종 모두 **모자 없음, 짧고 헝클어진 짙은 갈색 머리**로 최종 교정했다.
- 규격: ImageGen 회수 원본 1672×941 RGB를 중앙 16:9 크롭·Lanczos 정규화하여 1920×1080 RGBA, 알파 255로 저장했다.
- UI 경계: PNG 안에 읽을 수 있는 한글·영문·숫자, 버튼, 아이콘, 패널, 로고, 워터마크가 없다. `simul`의 세로 모바일 UI·화면 구조·좌표·패널 비율을 이식하지 않는다.
- 구도: 1920×1080 PC 가로 풀화면에서 안전하게 크롭 가능한 넓은 장소 묘사, 눈높이 카메라, 중앙 70% 인물·핵심 소품 안전 영역.

| 활동 | 파일 | SHA-256 |
|---|---|---|
| 편의점 간식 | `leisure_convenience_store_snack_run_v1.png` | `F1E6A54134171793C178C6A0E90C159D3E715EF7C593E38EA82486A4CF3883F8` |
| PC방 | `leisure_pc_bang_team_match_v1.png` | `240FBB94EEC65426C941B72764DAF5AEC63DDB77BB42BD45469EF86FD22791B2` |
| 비디오 대여 | `leisure_video_tape_rental_night_v1.png` | `A6479C7F283355A113E8A89FE1A28F5B745C43C863C1BCD087FC036404D34568` |
| 만화책 대여 | `leisure_comic_book_rental_stack_v1.png` | `49190B65DEA0B6BB93CCA021979F84AEB67A67C517352354C1EEE98785B3650D` |
| 동네 목욕탕 | `leisure_neighborhood_public_bath_v1.png` | `0FE4542A8B68054AB1035571677928B5435EB6834B2D18A18ECAB97B5E4AC346` |
| 가족 외식 | `leisure_family_restaurant_dinner_v1.png` | `E77639442F6A33AACA662B2915FE5F0529AA229D99D42087FBC783751BBDE509` |
| 저녁 산책 | `leisure_neighborhood_evening_walk_v1.png` | `E0E7254A26766FE7F4CF5150BFE2D631A8005408CBECFC1054560E299CB55435` |
| 강변 소풍 | `leisure_riverside_picnic_v1.png` | `8AA3AB045CBE9B31542DAC3842EC82D0EB53C602E3B597DBB69144DB0DDD13CF` |
| 문방구 오락기 | `leisure_stationery_arcade_break_v1.png` | `65FC85A475DA41BA987C61B71CF3C1266DB8D6946B5F0DD38EE142A5F681CB86` |
| 라디오 야식 | `leisure_home_radio_snack_chat_v1.png` | `9D9B368F36FBE7ACF8060FF5D37021BB6ABAA3119E8DA5354F2B7FD4C7DB9AF8` |
| 가족 노래방 | `leisure_family_singing_room_v1.png` | `2211A601EF9EAE823F45D12E269D650D8D0D9F65452AE70CBF633E045FFBD11B` |
| ADSL 협동 게임 | `leisure_adsl_coop_game_night_v1.png` | `8B00AF317B973A026444A9191E1F46CA1CF7C1098400B1D8AF06E791A7D913DD` |

- 교정 이력: 편의점·PC방·비디오 대여·가족 외식은 승인 구도와 시대 소품을 보존한 채 플레이어 머리만 ImageGen 무모자 편집했다. 만화책 대여는 작은 단행본·촘촘한 책등·낮은 책장·대여 묶음이 분명하고 LP류가 없도록, 목욕탕은 목욕 후 공용 대기 휴게실·단정한 휴게복·무노출이 되도록 전체 재생성했다.
- QA: 12종 모두 1920×1080 RGBA·알파 255 자동 검사 통과. 가족 4인 정체성, 무모자 플레이어, 시대 소품, 무문자, 넓은 16:9 구도를 접촉 시트와 개별 원본으로 육안 확인했다.
- 전체 프롬프트·참조 역할·후처리·활동별 QA 원장: `Docs/LEISURE_VISUAL_SPEC.md`

## 2026-08-11 Office 행동·UI 아트와 TMP 리소스 통합

- 가족 사무실 행동 아트: `Assets/Art/Characters/` 아래 727개 파일을 추가했다. 통합 검증에서 가족 4인 각 48프레임이 전부 고유하고 pivot이 bottom-center임을 확인했다(`FAMILY_SPRITES_PASS`).
- 계약 보드 UI 아트 키트: `Assets/Art/UI/Resources/ContractBoardV2/` 아래 32개 파일. 배경 `contract_board_background_2048x1152_v2.png`, 목업 A/B, 9-slice 버튼 4종(normal/hover/pressed/disabled), 접촉 시트 `contract_board_skin_kit_contact_v2.png`, 생성 원장 `contract_board_ui_art_ledger_v2.json`, 원본 보존 `SourceOriginal/`을 포함한다. `ContractBoardUiArtValidation`으로 프레임·버튼·배경을 검사해 통과했다.
- 관리 UI 스킨 카탈로그: `Assets/FamilyCompany/Presentation.Unity/Resources/ManagementUI/ManagementUiSkin_v1.asset`.
- 좌석 미세행동 프레임셋: `Assets/FamilyCompany/Content/OfficeWorkActions/`의 `player`·`father`·`mother`·`older_sister` 4종. 에셋은 존재하지만 런타임 훅은 아직 연결되지 않았다(`hook=fallback`).
- Unity 공식 TMP Essential Resources: `Assets/TextMesh Pro/` 37개 파일. 이 중 `Fonts/LiberationSans.ttf`는 Unity가 배포하는 서드파티 폰트이며 SIL Open Font License를 따른다. 사용자 생성 에셋이 아니므로 원본을 편집하지 않고 무변형으로 보존한다.
- 사용자가 권리를 보유하는 생성 에셋과 Unity 배포 리소스를 구분해 기록한다. 위 항목 중 TMP Essential Resources만 서드파티 배포물이다.

## 2026-08-11 Office Tile Migration T2 우드 바닥

- 상태: **IMAGEGEN SOURCE + 3/3 UNITY TILE ASSETS · T2/T3 QA PASS**
- 루트: `Assets/Art/Office/Tiles/`
- 제작: OpenAI 내장 ImageGen. `office_isometric_pixel_target_v1.png`의 팔레트·픽셀 밀도·등각 카메라와 SIMUL-v3의 렌더 마감만 참조했다. 인물·문자·가구·벽·UI는 생성하지 않았다. 폐기된 OfficeVisualV2 통짜 PNG는 더 이상 참조 에셋이나 빌드 리소스로 사용하지 않는다.
- 크로마 원본: `Source/office_floor_tiles_wood_chroma_v1.png`, SHA-256 `36E892D3478A4EB72765033B025ABF7F55730358D7DDF7321422BACC1C517A14`.
- 투명 원본: 공식 `remove_chroma_key.py`로 `#ff00ff`를 제거한 `Source/office_floor_tiles_wood_alpha_v1.png`, SHA-256 `044385A31955A2D70681B845EB6C85B8674BEBBDE0E4601B40838E5EFB936A4F`.
- 런타임 Tile PNG:
  - `Floor/office_floor_wood_a_v1.png` — `B2F3E265363D0EEB73059BF1F95CE85FD21F36B35944160906E7A8B51758FC39`
  - `Floor/office_floor_wood_b_v1.png` — `16051F325C6A4D9AE0D75A3C68C8E4C9C91C5EF7E908F9152C623D93C5ABDECE`
  - `Floor/office_floor_wood_c_v1.png` — `4CB57791B61E5B32EAC1234B6FE340C57B1B220D729353D4E0340A3E1A63CFF5`

## 2026-08-11 Office Tile Migration T4 가구 12종

- 생성 모드: OpenAI 내장 ImageGen. 사용자 보유 생성 에셋 권리 선언이 적용된다.
- 스타일 참조: `Assets/Art/StyleTargets/office_isometric_pixel_target_v1.png`, `Assets/Art/Office/Pixel/office_module_atlas_4x3_v1.png`, 새 workstation 시안을 화풍·카메라 참고로만 사용했다. 구형 아틀라스의 잘린 개별 조각은 사용하지 않았다.
- 공통 프롬프트 요약: “Family Company SIMUL-v3의 선명한 등각 픽셀 아트로 2000년대 한국 소형 사무실 소품 하나만 생성. 같은 2:1 카메라·좌상단 조명·크림/우드/민트/복숭아 팔레트, 평면 `#ff00ff` 배경, 12~18% 여백, 잘림·이웃 물체·바닥·그림자·사람·문자·로고 없음.” 각 생성에서는 소품명과 시대 세부 형태만 바꿨다.
- 원본 루트: `Assets/Art/Office/Tiles/Furniture/Source/`. 각 소품은 ImageGen 회수본 `_chroma_v2.png`와 공식 `remove_chroma_key.py --auto-key border --soft-matte --despill --edge-contract 1` 결과 `_alpha_v2.png`를 함께 보존한다.
- 투명 원본 SHA-256:
  - `office_coffee_table_alpha_v2.png` — `4B315502627C5AE40EDADB08E54629261A7A876E3612FF3133EDEABA8C19B2B0`
  - `office_document_bookcase_alpha_v2.png` — `50AA802510D4F2C002753AFFECDFF3574C8F5DF11B78FD83E06B264B3CDEC9F7`
  - `office_fax_copier_alpha_v2.png` — `69B6AAA9EC946DC29C896CABA367000ED4B03F4A2917DD0C10A58321A7A39860`
  - `office_filing_cabinet_alpha_v2.png` — `7C64C2870C5C7B58FE4C7F11FA43933541857C1860656E91C0DB0AA8151DCE08`
  - `office_meeting_table_alpha_v2.png` — `549C8A5D214205A4D26FFD5114D6CD75ED4D1931B51E5D0D8576414AC3F18D9A`
  - `office_partition_alpha_v2.png` — `934FD11E10AED46FC6932F2595ECD5857EE4E71C7530F4C3153AAF7C66A311E1`
  - `office_potted_plant_alpha_v2.png` — `51C950AE1135DAA75A612EA9C511A4CCD91BF725496DCB9E9D15539810128229`
  - `office_reception_counter_alpha_v2.png` — `A3E1171343CFE44B2A9C9886907A84905EA47EFCEBE00E839E80F09395456328`
  - `office_sofa_alpha_v2.png` — `4952CAAC215BA225B642D435B6FD5599A3F8FCD59BA64F6BD569C0070C650473`
  - `office_swivel_chair_alpha_v2.png` — `C233124DD83CAB7020DC305E2F185738A9B57841CC8278BD6128A18028276A18`
  - `office_water_dispenser_alpha_v2.png` — `1B0502317D3D9216ED725024F8F29B4D5D46F7FAAC9EF6F157D662B0323C0C29`
  - `office_workstation_alpha_v2.png` — `DE00FC8168130F596341D9DB240F0B399CEEDE7F7234177919E0B660278C4D66`
- 런타임 루트: `Assets/Art/Office/Tiles/Furniture/Runtime/`. `OfficeFurnitureAssetBuilder`가 visible bounds에 `min(maxWidth/sourceWidth, maxHeight/sourceHeight)` 단일 배율을 적용해 640×512 하드 알파·180 PPU·Point·mipmap 없음·무압축으로 재현한다. 종류별 실제 ground anchor가 pivot이며 반복 빌드 SHA가 같아야 한다. 착석 가림은 고정 Y 절단이 아니라 원본 좌표의 명시적 폴리곤 마스크 front Sprite다.
- 런타임 SHA-256:
  - `office_coffee_table_v2.png` — `B5948088A9E5BDABDD45F1AF1E745C8BC8CB2CDFF5EF00BC298DAD5561F2582C`
  - `office_document_bookcase_v2.png` — `665668993FEB321F7B508C65B9D4CAA538390BD5853E464895A150C2EB5ACD1C`
  - `office_fax_copier_v2.png` — `9388CD3D958386357192BA1677D4D720D640364A32A006E9D9421E2084032D73`
  - `office_filing_cabinet_v2.png` — `940CFE88C5AD0A1BCAB2ED4E7784ABF2604A14F386AA1DD94EACB3E75A7CBD1F`
  - `office_meeting_table_v2.png` — `C9AD8E6B383D9D846D0F311128E7DB818D63E2824A67C0B91F61CB0EB8E624F1`
  - `office_partition_v2.png` — `EC24F396BA2C50EB4011B5D09AF6D97860CEDFCAB9050BB6BC737EF30B3845DB`
  - `office_potted_plant_v2.png` — `DEB01641AB43CF50962C9664C8B04F88779935359B821D13371E077679624581`
  - `office_reception_counter_v2.png` — `668E5BB0F19E91D111E08B6624FBC0882718262B09FF161DE39F57E8EF477031`
  - `office_sofa_v2.png` — `09AD2A48A78A92E69C51461C0BCBEC4C49CDBE1DEEBB23B81E217EBC58DD0E9B`
  - `office_swivel_chair_v3.png` — `78E36D15B9940A808DD24C5D31D16A3F3D10037E6597391609D3350EA8A57BA4`
  - `office_water_dispenser_v2.png` — `283B6D26B1E0EE254FCFE3D8CBB7AB32A233924D3DB36BD5FEC3F0FDCAE705B1`
  - `office_workstation_v4.png` — `AAD471C666F84AED60008A5EADD9E6F8E86857A6E0C67B1ABF95070F9B2C1626`
  - `office_swivel_chair_front_v3.png` — `22D60EF8FD3A7A33CF8B3226B8B3ACF46AAA15A519ACE6E8B20BDFCF3D939E73`
  - `office_workstation_front_v4.png` — `762B63AF9A583EDF1F9243D7F182BFDA7155C362E5A25067F07F405708006B36`

### T4 사용자 캡처 교정 자산

- 생성 모드: OpenAI 내장 ImageGen. 기존 SIMUL-v3 팔레트와 2000년대 민트·우드 CRT 사무실 문법을 유지한 정밀 단일 소품 편집이다.
- 의자 프롬프트 핵심: 착석 인물이 좌상단 CRT를 보는 `NorthWest` 방향, 열린 좌석 앞은 좌상단, 등받이는 인물 뒤 우하단, 사람·책상·바닥·그림자·문자 없음, 균일 `#ff00ff` 배경.
- 책상 프롬프트 핵심: CRT·키보드·마우스·전화기·카메라·팔레트는 유지하고 바닥까지 닿는 넓은 옆판/앞판은 모두 제거, 서로 분리된 네 다리와 작은 발, 서랍장 아래 넓은 바닥 틈, 균일 `#ff00ff` 배경.
- 공식 후처리: `remove_chroma_key.py --auto-key border --soft-matte --transparent-threshold 18 --opaque-threshold 210 --despill --edge-contract 1`.
- 최종 원본 및 SHA-256:
  - `office_swivel_chair_northwest_chroma_v3.png` — `83CD917A61A943F6D1EAFFE2C35643DDE2CA4AF824AD3CE4820894477F7A950F`
  - `office_swivel_chair_northwest_alpha_v3.png` — `869B32F4A522099A2B52A4F0A9391C667565BB5E0E4D5F228C535FAA4C96FCC3`
  - `office_workstation_chroma_v4.png` — `84EFD59E4BCAD817064F8901E00153E8747FEB1F75F943DBBFD557576FA8BD46`
  - `office_workstation_alpha_v4.png` — `D913C6160C9AD32FA30618F796A7BB40C4A3EABC1AE02E337F8BFBC40D1ADD43`
- 최종 런타임 및 SHA-256:
  - `office_swivel_chair_v3.png`과 `office_workstation_v4.png`는 위 균일 스케일 SHA가 정본이다.
  - `office_swivel_chair_front_v3.png`과 `office_workstation_front_v4.png`는 base와 canvas·PPU·pivot이 같은 명시적 전경 마스크다.
  - 고정 Y cutoff로 만들었던 `office_swivel_chair_backrest_v3.png`는 폐기·제거했다.
- Visual authoring 정본: `OfficeFurnitureVisualCatalog.asset`(12종 ground/sort, chair seat, workstation work-surface)과 `OfficeCharacterSeatPoseCatalog.asset`(가족 4명 `NorthWest` pelvis/interaction)이다.
- 최종 Office Tycoon QA SHA-256:
  - `after-office-tile-tycoon-overview-1920x1080.png` — `7F2BEABA46E3F17772BE7320006001F1F98A62D4671831A5FDC089BC3B9056B8`
  - `after-office-tile-tycoon-seated-1920x1080.png` — `CBBDEF833119A8A0F16363020E539C2EDACAA013056B30FA8665AF1F5CF9079C`
  - `after-office-tile-tycoon-anchors-1920x1080.png` — `0417AC0C10C7F89112C1498108EF5762FF5DA4D0F8AFFC7A91C63790433362D4`
  - `after-office-tile-tycoon-occlusion-1920x1080.png` — `9BD71AE9E7B71A794F76FA034AC1382595D17BFF88AC71FB04E9650BF6B7D750`
  - `office-tile-tycoon-alignment-report.txt` — `3C08FB502057D89CAE59FF0B6AA8C68C49641C396E44F9C73272DA4F8797C069`
- 규격: 320×160 2:1 RGBA, 알파 0/255, 남은 마젠타 프린지 0, Sprite Single, 180 PPU, Point, mipmap 없음, 무압축. 같은 이름의 `.asset` 3개가 실제 Unity `Tile` 정본이다.
- 프롬프트 핵심: 밝고 캐주얼한 2000년대 한국 소형 사무실의 허니 오크 장판, 정확히 3개 동일 외곽 등각 다이아몬드, 미세한 판재 변형, 어둡거나 금색 위주의 고급 팔레트 금지, 텍스트·로고·워터마크 없음.

## 2026-08-11 Office Tycoon Alignment V2 calibration

- 정본 에셋: `Assets/FamilyCompany/Presentation.Unity/OfficeGrid/Authoring/OfficeFurnitureVisualCatalog.asset`, `OfficeCharacterSeatPoseCatalog.asset`.
- 버전: 가구 catalog는 `calibrationVersion: 2`, 캐릭터 pose catalog는 `calibrationVersion: 5`다. 캐릭터 catalog에는 사람 승인된 `NorthWest` SitDown 4 + Work 6 + StandUp 4를 네 가족별로 저장해 총 56개이며, 모든 항목이 source Sprite SHA-256을 가진다.
- 가구 데이터: 각 정의는 독립 네 점 ground footprint, 의미 footprint 폭/높이, ground/sort를 가진다. desk는 operator seat `(390.445, 49.329)`와 work socket, chair는 seat `(313.007, 153.549)`를 가진다.
- mask 판정: `office_workstation_front_v4.png`는 책상 앞 모서리·다리·서랍의 제한 전경으로 사용한다. `office_swivel_chair_front_v3.png`는 승인 catalog가 참조하며, 착석 중 등받이와 근접 팔걸이를 인물 위에 그리는 의자 전면 레이어다.
- 편집기: `OfficeTycoonAlignmentCalibrationWindow.cs`가 100/200/400% 픽셀 보기, 네 점·socket, clip/frame onion skin, workstation 합성을 제공한다. 합성 승인 전에는 값을 저장할 수 없다.
- 빌드 불변식: `OfficeFurnitureAssetBuilder`는 runtime PNG를 결정론적으로 재생성하며, 착석 v5 승격은 기존 v4 정적 4개 또는 완전한 v5 56개에서만 허용한다. 구형 v3의 scale/rotation 후보는 자동 이관하지 않는다. 승인 프로필은 빌드 때 원본 SHA를 다시 계산하고 scale `1`, rotation `0`을 강제한다.
- QA 산출물 루트: `Artifacts/OfficeTycoonAlignmentV2/`. 정본 검증기는 `OfficeTycoonAlignmentV2Qa.StartBatch`이며 Preview 45초와 Starter 60초를 분리 실행한다.
- 승인 SHA: player `D02E4A5E...59519D`, older_sister `1C7F25EC...FD92C3`, father `60B90628...A4C7E`, mother `1F8D8A29...E54FF7`.
- 승인 상태: authored Sprite 기준 네 명 모두 rotation `0°`, pose scale `1.000`, hand↔work `0.538px`, pelvis↔seat `0px`, chair↔desk `0px`로 PASS했다. 실제 Windows RenderTexture 기준 hand↔work는 `0.239px`이며 desk front의 얼굴 overlap은 네 명 모두 0, 하체 overlap은 모두 양수다. 실제 합성은 `Artifacts/SeatedSpriteRootCauseV3/starter-office-four-seat-work.png`와 가족별 `*-work-closeup.png`, 수치 보고서는 `seated-sprite-root-cause-v3-report.txt`다.

## Starter Office Runtime V1 semantic assets

- `Assets/FamilyCompany/Content/Resources/OfficeLayouts/StarterOfficeV1.asset`
  - 상태: CANONICAL SEMANTIC STARTER LAYOUT V1
  - 내용: 13×13 floor/walkability, 17 furniture records, placement subcell anchors, 네 workstation/seat/approach binding
  - 용도: 새 게임의 `GameState.OfficeGrid`, Runtime 렌더·충돌·좌석, Save layout hash의 공통 입력
- `Assets/FamilyCompany/Content/Resources/HighMotion/HighMotionDirectionManifest.asset`
  - 상태: CANONICAL DIRECTION IMPORT MANIFEST V1
  - 내용: 12 캐릭터 source→canonical 8방향 순열, 가족 네 명의 32개 사람 승인 플래그
  - Runtime member별 방향 예외는 금지하며 `HighMotionCharacterArtBuilder`가 이 manifest로 frame 이름을 정규화한다.
- `Artifacts/StarterOfficeDirectionQa/local-direction-contact-sheet.png`
  - 상태: LOCAL HUMAN-REVIEW EVIDENCE
  - 내용: player·older_sister·father·mother × 8 canonical directions. 화살표·이름·Sprite를 육안 대조해 32/32 승인했다.
- `Assets/FamilyCompany/Editor/OfficeLayout/OfficeLayoutEditorWindow.cs`
  - 상태: INTERNAL SEMANTIC AUTHORING TOOL
  - raw Scene Transform을 저장하지 않으며 `StarterOfficeLayoutAsset`만 수정한다.
- `Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/`
  - 상태: CANONICAL STARTER OFFICE PLAYER RUNTIME
  - Preview art source를 재사용하지만 하드코딩 Preview Actor/route는 생성하지 않는다.
- `Artifacts/StarterOfficeRuntimeQa/player-main-flow-17.log`
  - 상태: WINDOWS PLAYER MAIN-FLOW QA EVIDENCE
  - 단일 Actor, 충돌/회피, 런타임 가구 revision, 8방향, 4좌석 정렬, 계약, 저장/불러오기 연속 PASS 로그다.
- `Artifacts/StarterOfficeRuntimeQa/starter-office-four-seat-work.png`
  - 상태: FOUR-WORKSTATION HUMAN-REVIEW EVIDENCE
  - 1392×699 RenderTexture 캡처에서 가족 네 명의 실제 책상·의자·착석 Work 합성을 확인했다.
  - SHA-256: `92DB5F0D66158F30FEAB13672440096CCB79121BB1B97D2FE48B63FD39AFAAFE`

## 2026-08-12 Mother Northwest Work 하체 재생성

- 상태: **GENERATED 6/6 · IMPORT/PLAYER QA PASS**
- 대상: `Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames/mother_northwest_sit_work_0..5.png`.
- 생성 모드: OpenAI 내장 `imagegen`의 기존 이미지 편집을 프레임별 1회 사용했다. 각 대상 프레임을
  자세·손동작 기준, `mother_office_neutral_v1.png`를 얼굴·머리·복장 기준, 먼저 승인한 재생성 프레임을
  하체 비율·신발·scale·배치 일관성 기준으로 사용했다.
- 프롬프트 핵심: Northwest 착석 작업 상체와 피치 카디건·크림 블라우스·청록 스커트를 유지하고,
  누락된 무릎·종아리·양발·갈색 사무화를 전부 복원한다. 인물 한 명만, 가구·바닥·그림자·문자 없음,
  평면 `#00FF00` 배경, hard-edge pixel cluster, 프레임 0을 포함한 6장 동일 perceived scale.
- 후처리: 공식 `remove_chroma_key.py`로 chroma 제거, 녹색 spill 제거, nearest-neighbor로 visible height
  228px 통일, 우측 경계 x=172·하단 경계 y=249에 정렬했다. 최종은 256×256 RGBA, alpha 0/255,
  발바닥 하단 여백 7px다. 기존 `.meta`의 Sprite/Single·180 PPU·Point·mipmap 없음·bottom-center pivot은
  변경하지 않았다.
- frame 0 승인 좌판 등록점/손 앵커: `(131,62)` / `(90,120)`. 자동 해부학 후보 `(149,75)`는
  실제 chair sprite 합성에서 좌판을 벗어나므로 폐기했고, 승인 등록점과 확정 runtime scale `1.55`를
  `OfficeCharacterSeatPoseCatalog.asset`에 기록했다.
- 최종 SHA-256:
  - frame 0: `1F8D8A299555DD50A8ACE551B8627141CFD1C017DFD0B01FE01D57B559E54FF7`
  - frame 1: `0A2F1A778FE97246DE2B908BDF3FE7D6AC5DA2EBB27E522EC9D6F7C7CB204A00`
  - frame 2: `695FAFF1B75AA79E062690640FAE3B47C827297DD20C73131D2D843EA6A392F4`
  - frame 3: `63A06E819D07EFFFF9E8A2F06918494B05DE9CB1D96ECD8046A750ED3FA8B5EF`
  - frame 4: `85C8BDAE178B7EA0AEEE0EA3AF6FF10CC1D2A03D1E082E31E87AD7A427B99541`
  - frame 5: `BF481EDDB0FB2CF354A90D6666AB386BB7CC09AC2DE8C081B70C4002A6482986`
- 검증 증거: `Artifacts/MotherSeatedRegenQa/`의 before/after contact sheet, 엄마 closeup, 네 가족
  RenderTexture 합성, Unity 전체 검증 로그와 Windows player Main Flow 로그. 실제 플레이어에서 엄마
  seatContact `0.000px`, rotation `0°`, scale deviation `0%`, character sorting `1008`, chair base `1007`,
  desk `1005`를 확인했다. 의자 전면 레이어는 착석 인물 위에 유지된다.

## 2026-08-14 Starter Office perimeter wall regeneration

- 생성 모드: OpenAI 내장 ImageGen의 reference-guided precise object edit. 기존 갈색 wall 모듈은 방향·한 bay 구조 참고, 사용자 이미지는 밝은 재질·낮은 cutaway·열린 통로 인상만 참고했으며 UI/가구/인물은 복제하지 않았다.
- 승인 full-wall 프롬프트 핵심: 기존 SouthEast one-tile module과 transparent background를 유지하고 smooth light blue-gray plaster, slim matte-white top/base trim, pale panel seams, thin seamless ends로 교체한다. brown wood/fence/slats/rails/bulky pillars, floor, furniture, people, door, window, text는 금지한다.
- 승인 open-entrance 프롬프트 핵심: 같은 한 타일 span과 재질에서 중앙 wall face를 완전히 제거하고 two slim jambs + subtle threshold만 남긴다. door leaf/closed door/open swing/gate/glass/handle/header/background는 금지한다.
- cutaway는 승인 full source를 `Tools/prepare_office_perimeter_wall_sources.py --height-scale 0.58`로 isometric baseline 쪽에 nearest 압축해 파생했다. 생성된 checkerboard cutaway 후보는 투명 배경 계약 위반으로 폐기했다.
- 후처리 계약: 1536×1536 RGBA source, alpha `{0,255}`, transparent RGB 0, canonical endpoints `(316,160)`/`(796,400)` 반경 2px visible, 정확한 480×240 source basis, scanline edge pad 4. Runtime은 640×512/180 PPU/Point/no mipmap/uncompressed이며 175px bounds scale로 정확히 160×80 endpoint span을 만든다.
- source 정본:
  - `office_perimeter_wall_alpha_v1.png` — SHA-256 `E7BA97B890A33FDD731E64B085335D1F4270D4C945A84C957786E154C1257C1E`, GUID `786c6727de90f374aba5c1f14faadcdb`
  - `office_perimeter_cutaway_wall_alpha_v1.png` — SHA-256 `E563FC5258FD2363AE05814641703591E33DBE46EC860BC31317C94273021ED1`, GUID `c4dc68c2e72a02d4ba251a254c4144e8`
  - `office_entrance_door_alpha_v1.png` — SHA-256 `C70B8C5815EF152AE8A4F7C899E68991AA95585FD03AC944A5AC54E8EFE922B5`, GUID `a2d87f5f137975446a9532c0a42e1d9f`
- runtime 정본:
  - `office_perimeter_wall_v1.png` — SHA-256 `1B2374B5B9763D68097DB90A21CF1B73E5D2470A11F5C591F97CD47021F370A2`, GUID `95d951b2b1370524c886b2944a8828dc`
  - `office_perimeter_cutaway_wall_v1.png` — SHA-256 `2E5EA9E9C0B167BD8230D04987CC2D51E1025FD8E0852373DF2451824A5E6214`, GUID `01b50657111c72a46a58758231288e8f`
  - `office_entrance_door_v1.png` — SHA-256 `36B9E9E033A3CC9F101BE517B62D53F8D6706C169387AFC76F7668869BEF2C46`, GUID `bdb0baaefb381c84abbbb6802479ee22`
