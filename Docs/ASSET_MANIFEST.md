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

### 런타임 도트 이동 정본

- 경로: Assets/Art/Characters/Player/Pixel/player_pixel_walk4x2_v1.png
- 상태: CANONICAL RUNTIME SHEET
- 용도: 14살 플레이어의 직접 조작 4방향 2프레임 이동
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

### Unity 생성 프레임

- 경로: Assets/Art/Characters/Player/Pixel/Frames/
- 상태: GENERATED RUNTIME ASSETS
- 제작: 빌더가 정본 4x2 시트를 8개 단일 Sprite PNG로 분리한다.

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

### 런타임 도트 이동 정본

- 경로: Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v2.png
- 상태: CANONICAL RUNTIME SHEET
- 용도: 20살 누나의 4방향 2프레임 실제 이동
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

### Unity 생성 프레임

- 경로: Assets/Art/Characters/OlderSister/Pixel/Frames/
- 상태: GENERATED RUNTIME ASSETS
- 제작: Office V0.2 빌더가 정본 4x2 시트를 8개 단일 Sprite PNG로 기계적으로 분리한다.
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
