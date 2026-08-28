# ASSET MANIFEST

This manifest lists current experimental 3D inputs first, followed by canonical production assets. Retired Father iterations were removed from the current manifest; they are not reusable sources.

## Current Father V19 one-package character

- source: Higgsfield/Meshy job `865f2115-153d-41b6-84eb-d38ca106d45d`, `multi_image_to_3d`, four cleaned views, rigging/animation/PBR/remesh on, quad 60,000, A-pose 1.65 m, action 613; charged 38 credits.
- source GLB SHA-256: `210DC2E1160B3455CF599906721AE3698C789C6809AFBA6587C552742BB417F9`.
- FBX: `Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV19MeshyOnePackage613/father-v19-meshy-one-package-613.fbx`.
  - SHA-256: `479F883A8A3520FDF7A1DE500DEBCDFB059241D9A436B83A7938C14C4893AEB5`.
- albedo: `Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV19MeshyOnePackage613/father-v19-meshy-one-package-albedo.png`.
  - SHA-256: `8C1418E17002256C2393942067852B8BE484A2095AC85976DCC1CC2B70D9962B`.
- surface: `Assets/FamilyCompany/Experimental/Family3DPrototype/Materials/FatherV19MeshyOnePackageSurface.mat`; emission off, metallic 0, smoothness 0.22, source texture/UV preserved.
- clip: `FatherV19_Casual_Walk_inplace`, source frames `1..43`, 1.4 s, same FBX Avatar/skin/clip.
- status: walk/colour user approved as locked isolated input; production promotion is not implied.

## Current Father V27 full-3D workstation proof

- build: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapBuildV22NeutralChairNoLegacyOverlay`.
- runtime: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapRuntimeV27NeutralChairNoLegacyOverlay`.
- route: actual `seat_father` / `desk_father` / `chair_father`, `Idle>Navigating>ApproachingSeat>AligningSeat>RotatingToSeat>Working`.
- receipt: 1,051 samples, 361 work observations, 132 captures, `productionMutation=false`, `productionEligible=false`.
- full GIF SHA-256: `536C4605B778C1320AE4EC8E71DC2C4E7AC33B84543E55D293A124AF9D66A804`.
- close GIF SHA-256: `9F0BB210AA724B46A6B0C78E90D3ACFD6A473618D11DBF2B2C3C6DB01C52B18A`.
- tracked home-review copies: `Docs/Evidence/Family3DFatherV19V27/father-v19-v27-full.gif` and `father-v19-v27-close.gif`.
- V27 ownership: QA-only neutral 3D chair and continuous masking of the late-created legacy occupied-chair foreground renderer. Semantic furniture/route/blocking/save data are unchanged.
- exact next-character recipe: `Docs/FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md`.
- status: `USER_VISUAL_REVIEW_REQUIRED`; Higgsfield use for V27 is 0 credits.
## Main Navigation HUD V2 (2026-08-14)

- Runtime root: `Assets/Art/UI/Resources/MainNavigationV2/`
- Status: CANONICAL GENERATED UI SKIN; 이전 `MainNavigation/` V1은 거절되어 runtime, ledger, 문서와 함께 제거함
- Runtime set: `Frames` 22장, `Icons/Bottom` 5장, `Icons/Investment` 5장, `Markers` 2장, 합계 34장
- Reference: `Assets/Art/UI/Resources/MainNavigationV2/Reference/main_navigation_v2_visual_target.png`
- Provenance ledger: `Assets/Art/UI/Resources/MainNavigationV2/Generation/main_navigation_imagegen_ledger_v2.json`
- Creation: OpenAI built-in ImageGen으로 생성한 밝은 cream/coral/teal 캐주얼 경영게임 surface와 아이콘. 글자, 회사명, 날짜·시간, 숫자, 버튼 label은 이미지에 굽지 않고 Unity TMP/uGUI가 렌더함.
- Runtime ownership: frame은 Unity sliced Sprite, input state는 SpriteSwap으로 사용하며 PPU 100, Bilinear, Clamp, mipmap off, uncompressed import를 고정함.
- Rights: project-owned generated assets; no third-party source material.

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

### 동쪽 접촉 정본의 원본 도트 시트

- 경로: Assets/Art/Characters/Player/Pixel/player_pixel_walk4x2_v1.png
- 상태: CANONICAL IDENTITY SOURCE / PLAYER EAST CONTACT SOURCE
- 용도: 14살 플레이어 정체성·카메라 참조이며 현재 east-only 두 접촉 런타임의 직접 원본이다.
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

### 제거된 비정본 시안 tombstone (2026-08-14)

- 경로: `Assets/Art/Characters/OlderSister/older_sister_office_neutral_v1.png`
- 상태: REMOVED DEPRECATED CONCEPT
- 이유: 나시티·돌핀팬츠·맨발 정본으로 교체되었고 GUID `ca576568262b0064fb2ba43e634ab913`의 외부 참조가 0임을 삭제 직전에 확인했다.
- 삭제 전 SHA-256: `52C428A0D64F92BF609ABEF78870E63500F9C2E8B96B97DFC859D8F447564C28`

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

### 제거된 구형 누나 도트 tombstone (2026-08-14)

아래 v1 시트와 기계 분리 프레임은 v2 identity reference와 현재 HighMotion 8방향×6프레임으로 완전히 대체되었다. 각 GUID는 자기 `.meta` 외 참조 0을 삭제 직전에 확인했다. v2 시트, chroma source, HighMotion, office module atlas는 보존한다.

| 제거 경로 | GUID | 삭제 전 SHA-256 |
| --- | --- | --- |
| `Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v1.png` | `dddb366d877ce454d9f079b157c6a331` | `9C90EAED493A8EBC8746D2AEC275D3DF40B603722A17A821F7740C19D5A6B87A` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_east_a.png` | `8a4541a0fd1050c4d9011a4270543758` | `0684E09EB9EE21F798691F425F04F4EE16CA85DE3F6F576845E24B61835C9A5D` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_east_b.png` | `ba9b86ae2bdcefb4dbc18b91ddb3ff3a` | `AA319EF7A6C02D851C5CEEF6DA08C1990AC4AC9A542A8532CABE6F793F3E7BD0` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_north_a.png` | `2e650dcc40b91d0489816f9a53fc137d` | `FE0896CB95EB65E775686BD4155FCBBD33F3A92D4F8B606B1A6D4FBB5D5CFDAA` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_north_b.png` | `82f33dc2cfd17c84f8603aa0ad0129a2` | `C14D0F2D232E844CC1A5C1D18096517209381E030502F6A5CD5486C0316A129D` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_south_a.png` | `4b789643b32b8f84d8bd9681a307331e` | `27B58C5644E6944F8045A1F2BDE53CBA0FC5CE061A8A887D0BFDB2DE4C4A7761` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_south_b.png` | `016849479d8b7104489c1b0ac439ba9c` | `EB76CE94A2E6ADBAF2C724BDBF557AC48EC978BD547A3602F7CFAED967400CD2` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_west_a.png` | `73650d11323963a49916d656fe03c552` | `A5A343B1C5523A0BA2660E8E2C085170CA41B4558A3F67E8DCAD6C9180A5368E` |
| `Assets/Art/Characters/OlderSister/Pixel/Frames/sister_west_b.png` | `3198b1f4ade2d684a8a49fb60d5b7f08` | `6DF64837F53AA8284807DCFD2D621A84B9721DD89BED76A8213691DB6EB92623` |

제거된 폴더 `.meta` GUID는 `99884834cf3bde0419b11cc40259e127`이며 외부 참조 0이었다.

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

이 절은 최초 12인 에셋 제작 이력이다. 현재 가족 4명 shipping writer/gate나 플레이어 모자 정본을
설명하지 않는다. 플레이어의 현재 외형은 `CANON.md`의 빨간 뉴스보이 캡이며 위 Generation V1 절이 우선한다.

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

### 2026-08-16 가족 4인 전신 보행 복원

- 대상: 플레이어·누나·아빠·엄마의 runtime HighMotion 시트 8장과 frame 192장.
- 출처: `Assets/Art/Characters/BeforeCoherenceV1/{player,older_sister,father,mother}/`에 보존된 승인
  `*_pixel_walk8dir6_[ab]_v1.png`. 신규 ImageGen 생성 없이 이 PNG 바이트를 canonical HighMotion 시트로
  복원했다. 복원 전 팔 고정·하체 2포즈 runtime 시트는 repo 밖 작업 증거에 보존했다.
- 동작 계약: 방향별 6개 전신 고유 포즈에서 좌우 발과 반대 팔이 함께 교차한다. frame 0 상체를 전 사이클에
  덮거나 다리를 두 접촉 포즈로 축소하지 않는다.
- 분할: `Tools/split_high_motion_sheets.py --character <id> --assume-grid-layout`. 8-connected component row-run
  검출로 셀 경계를 넘은 팔·발도 한 실루엣으로 취급하고, 256×256 frame 안에서 발 하단을 y=247에 맞춰
  8px 하단 안전 여백을 보존한다. 알파는 0/255다.
- 검증: `Tools/measure_animation_coherence.py --motion walk --strict` 결과 12명·96 loops·576 frames PASS.
  silhouette median<=30%, worst<=40%, unique 6, foot drift<=1px, stable root drift<=4px, closure<=2px다.
  `Tools/test_animation_coherence_gate.py` 9/9 PASS이며 발·팔을 포함한 full-body silhouette pop 회귀를 검사한다.

### 2026-08-17 엄마 북쪽 보행 V2

- 기존 승인 원본도 엄마 북쪽에서는 충분하지 않았다. 0/3 접지 포즈의 상체 실루엣 차이가 5.6%이고 발 영역
  무게중심이 `128.17→128.11px`로 사실상 같아, 6개 파일이 있어도 팔·치마가 고정된 채 발만 끌려 보였다.
- ImageGen으로 북쪽 뒷모습 반 주기 `contact→recoil→passing` 3장을 포즈 가이드와 함께 새로 만들었다. 생성 원본과
  최종 프롬프트·SHA는 `ArtSources/MotherNorthWalkV2/`에 보존하며 Unity `Assets/` 밖이라 반복 임포트 비용이 없다.
- `Tools/build_mother_north_walk_v2.py`가 녹색 크로마를 제거하고 225px 전신·y=247 발 기준선으로 정규화한다.
  후반 3장은 전반 3장의 픽셀 정확 좌우 반전이므로 지지발과 반대 팔 위상이 틀어질 수 없다. 엄마 B 시트는
  기존 북동·동·남동 runtime frame을 그대로 사용해 marker-authored 4×6 그리드로 재조립했다.
- `Tools/test_mother_north_walk_v2.py` 5/5 PASS: 지지발 순서 `R,R,L,L,L,R`, 0/3 exact mirror, 0/3 상체 30.1%·
  치마 29.2%·발 78.2% 변화, 6개 고유 frame, y=247, sheet/frame 일치를 고정한다. 기존 animation gate 9/9,
  전체 walk 12명·96 loops·576 frames, mother 48-frame split verify도 모두 PASS다.
- Unity `6000.3.21f1` clean Release Player 실제 정북 이동 closeup에서 imported
  `mother_north_walk_0..5` 여섯 장을 모두 확인했다. 0/3 지지발·반대 팔 반전, 1/4 회수, 2/5 반대 통과,
  치마 밑단 하중 변화와 발 하단 잘림 0이 실제 렌더에서도 유지된다.

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
- 이 12종 VN 동작 시트 안에서는 **모자 없음, 짧고 헝클어진 짙은 갈색 머리**로 교정했다. 월드 조작 말의
  최신 외형 정본은 `CANON.md`의 빨간 뉴스보이 캡이며, VN 시트의 무모자 상태를 월드 보행에 전파하지 않는다.
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

### T4 건축·편집 자판기 4방향

- 생성 모드: OpenAI 내장 ImageGen. 실패한 fully-opaque 투명/체커 결과는 폐기하고, 같은 2000년형 크림·민트 음료·간식 자판기를 마젠타 배경에서 실제 SE/SW/NW/NE 회전으로 생성했다. SE/SW는 서로 반대 조작면, NW/NE는 서로 반대 후면+측면이 보이며 단순 좌우 flip을 사용하지 않았다.
- 원본 루트: `Assets/Art/Office/Tiles/Furniture/Source/office_drink_vending_machine_<direction>_{chroma|alpha}_v1.png`.
- 공식 후처리: `remove_chroma_key.py --auto-key border --soft-matte --transparent-threshold 18 --opaque-threshold 210 --despill --edge-contract 1`. 생성기가 명목상 `#FF00FF` 필드를 소폭 양자화했으므로 border key를 공식 도구가 직접 샘플했고, 생성형 원본에 임의 alpha를 추정하지 않았다.
- alpha source SHA-256: SE `3C358B25B554106AC3DBC9E44B614A6C1FB7582C4B66421519A96FA32DD2E650`, SW `D85BE78235BCC94BABC663BC82761B3B98A363A3AFFA2F67819B0DB3AFEA1F2D`, NW `FCB1C4E00D0750847D8FC6201BF6B6B595D8C22ADF2DA56F4147A0812B1DAD22`, NE `C487F4C67B4EDAD974CD697B689F5E768B8CB190215382D028090468032B33BB`.
- 런타임 루트: `Assets/FamilyCompany/Presentation.Unity/Resources/OfficeBuildFurniture/drink_vending_machine_<direction>.png`.
- runtime SHA-256: SE `8AE66D1F6269B8559E7E2C84D451760FA5319F8E4C9D09265F6A8D32EBE54462`, SW `BD487C055EB4F62F506D9094B8FDD4CE2670CE7AD01B3C6773899F9AD06F7A6E`, NW `8D0E486A264B3A724C3104C81F6B5F95630744B9109F2880401973D28ADA8C36`, NE `24A806A0C301FD30EF2D8E985A6FA3483BD279C0A658C0431AACA8B42DDBCA9A`.
- `OfficeBuildVendingArtBuilder` 정본 규격: 640×512 RGBA hard alpha, 180 PPU, Point, mipmap 없음, 무압축, ground pivot `(320,28)`, 24px 이상 safety margin. 반복 빌드 SHA, 방향별 고유성, front/rear 분류, Resources exact selection, visible magenta fringe 0을 Unity 6000.3.21f1에서 검증했다.

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
  - 상태: FURNISHED LEGACY/QA SEMANTIC FIXTURE V1
  - 내용: 13×13 floor/walkability, 17 furniture records, placement subcell anchors, 네 workstation/seat/approach binding
  - 용도: 기존 저장 호환과 출근·좌석 회귀 QA. 실제 새 게임은 코드의 `CreateNewGameEmptyOfficeV1()` 바닥·외곽 shell을 사용한다.
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
- 최종 open-entrance는 승인 벽 재질에서 중앙 wall face와 양쪽 jamb/lintel을 모두 제거하고 exterior-side thin threshold만 남긴 결정론적 파생본이다. door leaf/closed door/open swing/gate/glass/handle/header/background는 금지한다. 재작업 ImageGen threshold 후보는 checkerboard가 baked되어 폐기하고 정본 source의 hard-alpha mask를 사용했다.
- cutaway는 승인 full source를 `Tools/prepare_office_perimeter_wall_sources.py --height-scale 0.58`로 isometric baseline 쪽에 nearest 압축해 파생했다. 생성된 checkerboard cutaway 후보는 투명 배경 계약 위반으로 폐기했다.
- 후처리 계약: 1536×1536 RGBA source, alpha `{0,255}`, transparent RGB 0, canonical inner-edge endpoints `(316,172)`/`(796,412)` 반경 2px visible, 정확한 480×240 source basis. Runtime은 640×512/180 PPU/Point/no mipmap/uncompressed이며 고정 1/3 nearest bake로 정확히 160×80 endpoint span을 만든다. near cutaway/threshold 기단은 inner edge 바깥쪽만, far full wall은 edge 위로 솟는 face/cap만 유지한다.
- source 정본:
  - `office_perimeter_wall_alpha_v1.png` — SHA-256 `6F5A8C7313008DCF0A657101144E95A4A0EF63C63D7BD6730713C29CB24E4E82`, GUID `786c6727de90f374aba5c1f14faadcdb`
  - `office_perimeter_cutaway_wall_alpha_v1.png` — SHA-256 `E563FC5258FD2363AE05814641703591E33DBE46EC860BC31317C94273021ED1`, GUID `c4dc68c2e72a02d4ba251a254c4144e8`
  - `office_entrance_door_alpha_v1.png` — SHA-256 `0D3CC8B100546F4A25FB73015E56A53054D8A75A7C993A0C9614538DF8BEFD6B`, GUID `a2d87f5f137975446a9532c0a42e1d9f`
- runtime 정본:
  - `office_perimeter_wall_v1.png` — SHA-256 `4A9C6440EFCFBCEE4423531EBA797CAEA868D56107E29356CD631B32EE095FF2`, GUID `95d951b2b1370524c886b2944a8828dc`
  - `office_perimeter_cutaway_wall_v1.png` — SHA-256 `2E5EA9E9C0B167BD8230D04987CC2D51E1025FD8E0852373DF2451824A5E6214`, GUID `01b50657111c72a46a58758231288e8f`
  - `office_entrance_door_v1.png` — SHA-256 `869DB3D9DA8F98B50939D1F7EC917CF9C898B612CE9A95AE2A8A40540892B0F3`, GUID `bdb0baaefb381c84abbbb6802479ee22`

## 2026-08-20 2D player walk source revision v3 / alternating assembly v4 / east footwear v10

- 생성 도구: OpenAI ImageGen. 기존 정본 주인공의 빨간 뉴스보이 캡, 흰 후드 윈드브레이커, 줄무늬 셔츠,
  남색 바지와 운동화를 유지하면서 8방향×6포즈, 고정 머리/골반 높이, 차분한 보폭, 반대 팔·다리 교차,
  녹색 크로마 배경을 요청했다.
- `ArtSources/PlayerWalk2DGenerated/player_walk8dir6_a_chroma_v3.png`
  - south, southwest, west, northwest × 6포즈; 1,718,329 bytes
  - SHA-256: `DCEAFD6F431070CA7961C25130998C0294309D6965EBDDD28F3A1452BE2E6490`
- `ArtSources/PlayerWalk2DGenerated/player_walk8dir6_b_chroma_v3.png`
  - north, northeast, east, southeast × 6포즈; 1,642,880 bytes
  - SHA-256: `F0DD325FA70DF27E2A8173A70684D3184D189ED52ADB4ED38735B12270C6B476`
- 변환: `Tools/Build-Player2DWalkV2Candidate.ps1`이 녹색을 제거하고 256×256 hard-alpha 48장으로 분리,
  머리 꼭대기와 bottom-center를 정규화한다. `Tools/Build-Player2DWalkAlternatingV4.ps1`은 사용자가
  승인한 P0~P2와 P3~P5의 팔·상체를 유지하고 P3~P5 하체만 P0~P2의 골반축 반사로 교체한다.
  seam fraction은 0.70이며 런타임 root는 `Assets/Resources/FamilyCompany/Player2DWalkV2/`다.
- east footwear v10: v9도 swing 최하단이 support와 0~2px 차이인 포즈가 남아 동시 접지로 거부됐다.
  v10은 support bottom을 233으로 고정하고 swing bottom을 227/223/227로 강제한다. 양 반주기의
  접지 간격은 정확히 6/10/6px이며 운동화는 강체 평행 이동만 한다. 다른 7방향 42 PNG와 east
  상체·팔·교차 보폭은 v4와 동일하다.
- QA: Unity 6000.3.21f1 actual Windows Player D3D11, 8방향 loop/48 closeup/8 overview,
  한 타일 stride 0.99380799에서 cadence 1.9819~1.9979 steps/s. v4의 lower-body mirror mismatch 0은
  east 신발 방향 교정으로 더 이상 최종 48장의 불변식이 아니다. 기존 bottom-12px lead-shoe 검출은 원근 방향의 두 신발을
  분리하지 못해 3/8행만 판정 가능하므로 상태는 `PASS_NON_SHIPPING`; support-foot/contact-step은 미측정이다.
- 사람 승인: 2026-08-20 사용자가 기존 두 GIF의 다리 교차와 팔 자세를 승인하고, 오른 반주기 뒤 왼
  반주기가 오도록 순서만 교정하라고 확정했다. v4는 이 범위 밖의 상체 그림을 바꾸지 않는다.
- 권리/배포: 프로젝트용 생성 파생물. 추적 크로마 원본은 ArtSources에 보존하고 게임에는 처리된 PNG만 포함한다.

## 2026-08-20 2D player walk half-cycle source revision v5 (rejected research)

- 생성 도구: OpenAI ImageGen built-in. v3를 정체성/스타일 참고로 사용하고, 한 번에 6포즈 대신
  오른발 접지→오른발 지지→왼발 낮은 통과의 첫 반주기 3포즈만 4방향씩 생성했다.
- `ArtSources/PlayerWalk2DGenerated/player_walk8dir3half_a_chroma_v5.png`
  - south, southwest, west, northwest × 첫 반주기 3포즈; 1,478,559 bytes
  - SHA-256: `49E5C964751834B584636195CD27B536B74570CAF19FFEF6563460E650AB1EDE`
- `ArtSources/PlayerWalk2DGenerated/player_walk8dir3half_b_chroma_v5.png`
  - north, northeast, east, southeast × 첫 반주기 3포즈; 1,446,468 bytes
  - SHA-256: `7595FE81919AADC88978D3A4B8739A09B14BA94D60387F1F6AB9D0DB5F5A3A3E`
- 조립 연구: `Build-Player2DWalkHalfCycleV5Candidate.ps1`과
  `Build-Player2DWalkAlternatingV4.ps1`이 두 번째 반주기 하체를 골반축 반사한다.
- 상태: REJECTED RESEARCH. lower-body mirror는 만들었지만 two-step gate가 northeast/east 2/8행만 통과했다.
  Unity 후보/production 리소스에 복사하지 않는다.

## 2026-08-20 Mixamo humanoid walk authoring inputs (rejected visual research)

- 권리: Adobe Mixamo 캐릭터와 애니메이션은 게임을 포함한 개인·상업 프로젝트에서 royalty-free로 사용할
  수 있다. 원본 재판매/재배포가 아니라 게임 내부의 baked derivative PNG만 런타임에 포함한다.
  근거: <https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html>.
- `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidBase.fbx`
  - 출처/이름: Mixamo `X Bot`, T-Pose, FBX for Unity
  - 크기: 1,750,032 bytes
  - SHA-256: `BA1FBC01DF013A102363E88E698719176A4366CE6B3C01AB500319DF55C37BA1`
  - 상태: EDITOR-ONLY AVATAR/RETARGETING PROBE. X Bot 표면은 final art가 아니다.
- `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx`
  - 출처/이름: Mixamo `Unarmed Walk Forward`, FBX for Unity, root motion 포함
  - 크기: 417,392 bytes
  - SHA-256: `1E8A4F23148893DA5E63CF4C508C180464AB834BEB8A12A570CF1A044C2168ED`
  - 상태: EDITOR-ONLY CANONICAL WALK MOTION SOURCE.
- 외형 파생 규칙: `PlayerWalkCanonicalVisualBuilder.cs`의 `canonical-protagonist-v1`. 외부 3D character
  표면을 추가 반입하지 않고 Mixamo 뼈에 프로젝트 CANON 복장을 닫힌 primitive volume으로 결합했다.
  실제 화면에서 주인공 정체성·2D 합성·바운스 기준을 통과하지 못해 2026-08-20 거부했다.
- 출력 상태: `PlayerBakedWalkHumanoidV2Candidate`/`PlayerBakedWalkV2`는 연구 기록이며 production으로
  승격하지 않는다. `PlayerWalkHumanoidPromotion.json`도 출하 승인 근거로 사용하지 않는다.
- 당시 Humanoid 연구 결합 규격: speed `1.5`, acceleration `8.0`, cycle `0.8s`, stride `1.2`, 8 poses/cycle.
  Mixamo 원본 root travel은 포즈 위상과 support foot 검출에만 사용하고 최종 사람 크기나 logical root
  이동량으로 사용하지 않는다. 이 수치는 폐기된 Humanoid 연구 기록이며 현행 runtime에 적용하지 않는다.

## 2026-08-20 Player East Mixamo Trace V2 (motion PASS / isolated art candidate)

- 추적 패키지: `ArtSources/PlayerEastMixamoTraceV2/`.
- raw motion: `PlayerWalkMotionReferenceExporter`가 Mixamo `PlayerHumanoidWalk.fbx`를 east `+90°`로
  평가해 ignored `Artifacts/PlayerEastMixamoTraceCandidate/mixamo-east-6pose-joints.json`에 만든다. 원시
  export는 공개 Git에 넣지 않고 파생된 2D 계약만 추적한다.
- 2D contract: `target-joints.json`, `phase-contract.md`, `player-east-locked-skeleton-guide.png`.
  사용자 승인 long-stride 격리값 `1.49071199`, PPU 180, scale 1.55에서 root advance
  `28.852490px/pose`; 계산된 heel/toe target 최대 contact drift `0.295020px` (`<=1px`). 이 수치는
  production 전역 stride가 아니라 east 후보용이다.
- source/style: `SourceV3Frames/` 6장은 phase별 상체·외형 참고다. lower pose donor로 사용하지 않는다.
- rejected evidence: `RejectedImageGenInputs/` 3장, `RejectedResearch/`의 LockedArtV2 sheet/GIF/receipt,
  README에 SHA/prompt가 기록된 home-PC ImageGen 2회다. 모두 shipping 사용 false이며 `Assets`로 승격하지
  않는다.
- isolated lower candidate: `PlayerEastWalkLockedArtAuthoring`이 P0~P5를 target의 physical owner별
  pelvis→hip→knee→ankle→heel/toe chain에서 새로 rasterize해 ignored
  `Artifacts/PlayerEastMixamoLockedArtV3/`에만 만든다. source lower, mirror, fragment move/warp는 쓰지 않는다.
  `Test-PlayerEastWalkLockedArtV3.ps1` 기준 upper mismatch 0, hard alpha, joint coverage, 한 material component,
  east shoe 12/12, unique frame 6/6을 통과했다. 두 다리를 각각 검정 outline한 초기 후보는 교차부가 분리된
  조각처럼 보여 폐기했다. 수정본은 pelvis+두 leg의 실제 색 면을 pants core mask로 합친 뒤 mask 바깥에만
  1px 외곽선을 만든다. 실제 1.55배 맵에서 얇게 읽힌 관절 외곽은 center와 heel/toe를 유지한 채 허벅지/무릎/
  발목 full width를 약 `16.8/14.5/10.6px`로 보강하고 신발 collar도 넓혔다. 발목↔신발 내부의 둘러싸인
  exact-black은 주변 depth 색으로 닫되 외부 검정 실루엣은 보존한다. long-stride 재저작의 내부 검정 outline은
  포즈당 최대 `13px`(`<=60px`)이며, 별도 pelvis→thigh junction의
  완전히 둘러싸인 exact-black outline은 6포즈 모두 `0`이다. 긴 골반 가로 highlight와 중앙 주름도 쓰지
  않으며 motion-following hip bridge 뒤 최초 leg split은 pelvis 아래 최소 `17px`다(계약 `>=17px`).
  눌려 보인 신발은 heel/toe를 유지한 채 vamp/heel/앞코와 heel panel을 보강했다. long-stride 재저작 뒤
  12개 신발의 최소 색 높이는 `11px`, material 면적 `207px`, red `67px`, white `88px`이며 shoe overlap은 0이다.
- actual tile-map QA: `Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/Qa/PlayerEastLockedArtTileQa.cs`
  (meta GUID `e76b7300669847da8dd32e97006c0703`)가 normal new-game 13×13 맵과 실제 `OfficeRuntimeAgent`를 사용한다.
  production catalog를 바꾸지 않고 외부 후보 6장을 pose-major 48-frame 배열의 east row에만 주입하며 player 외
  가족 3명은 숨긴다. QA 소유 중에만 speed `1.5`, stride `1.490712`를 적용하고 종료 시 production
  speed `1.0`, stride `0.993808`로 복구한다. Windows D3D11, editable furniture 0, PPU 180, scale 1.55,
  cycle distance `1.498756`, VisualRoot offset 0, P0~P5 캡처를 PASS했다.
- final lower art: 격리 static/actual tile-map gate PASS, 사용자 타일 맵 화면 승인 대기. 승인 뒤에만 Assets
  candidate로 승격하며 production은 계속 `Legacy48`이다. 현재 배포 EXE는 바꾸지 않았다.
- tile-step review: production은 `2 steps/tile`, `120.75 steps/min`, visible-height 대비 step `27.5%`다.
  KShopGo 실제 영상·APK와 타일 RPG/실제 보행 비교 뒤 east 격리 후보에 speed `1.5`, stride `1.49071199`,
  `1.333 steps/tile`, step/height `41.2%`를 적용했다. target root advance와 foot-lock을 함께 다시 만들었고
  전역 상수는 바꾸지 않았다.
- 퇴역 재현 해시는 `ArtSources/PlayerEastMixamoTraceV2/SHA256SUMS.txt`에 남고 2D 편집 README는 삭제했다.
