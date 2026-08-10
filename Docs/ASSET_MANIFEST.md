# ASSET MANIFEST

최종 갱신: 2026-08-10

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

### Money Rain 무인 타이틀 대안

- 상태: **GENERATED TITLE ALTERNATIVE · 기존 hero 미교체**
- 루트: `Assets/Art/UI/Resources/Title/MoneyRain/`
- 배경: `money_rain_office_background_v1.png`, 1920×1080 RGBA, SHA-256 `C0431E765D90A78016B59A50531999783CC40A333E69EEB57A86538219C1ECC2`
- 투명 돈다발: `money_bundle_mint_v1.png`, `money_bundle_coral_v1.png`, `money_bundle_sky_v1.png`; 각 1024×1024 RGBA, 실제 알파 0~255
- 돈다발 SHA-256: 민트 `DCF0F18332E94849C159CF50FB924441B894BCF0F5E992AAA5AEBCDF2CC05A79`, 코랄 `13FD656B1C4192D2E99A86A984DE4BC7CC7506DE1151B605662505F7D534880F`, 하늘 `A555448A25BA7E873E07E68E0BB625DF6F84BF2F11196B8031C29D3C647789EB`
- 시각 확인 GIF: `family_company_title_money_rain_v1.gif`, 960×540, 28프레임×100ms, 2.8초 무한 루프, SHA-256 `3F4630BCBC51740368E62B67C5E5F10A00058AC92AA57FC8311657B6B96F4AF4`
- 제작: OpenAI 내장 ImageGen으로 사람 없는 2000년 한국 소형 사무실과 가상 원화 돈다발 3종을 생성했다. 지폐는 실제 초상·액면·문구·국가 상징·로고를 복제하지 않은 안전한 게임 일러스트다.
- 후처리: 배경은 중앙 16:9 크롭·Lanczos로 1920×1080 정규화했다. 돈다발은 생성 이미지에서 가장자리 연결 배경만 분리해 실제 투명 알파로 만들었다.
- 구도·UI: 왼쪽 40%와 좌하단은 기존 Unity 제목·버튼용 저복잡도 안전 영역이다. 이미지 안 제목·숫자·버튼·로고·워터마크는 없다.
- 애니메이션: 하늘색은 뒤, 코랄은 중간, 민트는 앞 깊이로 크기·그림자를 달리하고, 닫힌 대각 타원 경로와 사인 회전으로 시작/끝 속도를 연속시켰다. GIF는 시각 확인용이며 Unity에서는 배경과 투명 PNG 3장을 저비용으로 별도 애니메이션한다.
- QA: 사람·찌그러진 누나·인물 잔상 0, 읽을 수 있거나 글자처럼 뭉친 표기 0, 카지노/검정·금색 고급 분위기 0. 루프 경계 프레임 RMS 17.910, 일반 인접 프레임 중앙값 16.845로 경계 점프가 일반 이동량과 같은 수준이다.

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
