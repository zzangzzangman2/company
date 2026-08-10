# 가족회사 회복·주말 활동 장면 비주얼 사양

## 1. 목적과 상태

- 상태: **12/12 FINAL · VISUAL QA PASS**
- 제작일: 2026-08-10
- 제작 도구: OpenAI 내장 ImageGen (`imagegen` 스킬의 기본 생성·참조 이미지 편집 경로)
- 사용 위치: `Assets/Art/Leisure/`
- 최종 규격: 1920×1080, 16:9 가로, RGBA, 완전 불투명 알파
- 화면 원칙: 1920×1080 PC 가로 풀화면과 넓은 장면 표시를 전제로 한다. `simul`의 세로 모바일 UI, 패널, 좌표, 행동력 화면은 복사하지 않는다.
- Unity UI 원칙: 장면 PNG 안에는 글자·버튼·아이콘·패널을 넣지 않는다. 활동명, 비용, 효과, 선택 버튼은 Unity에서 별도 합성한다.

12종은 `LeisureActivityCatalog`의 의미 데이터와 연결할 수 있는 실제 장소·가족 행동 장면이다. 단순 버튼 목록이나 아이콘 세트가 아니다.

## 2. 정본 참조와 역할 분리

| 입력 | 정본 경로 | 역할 |
|---|---|---|
| Image 1 | `Assets/Art/StyleTargets/References/simul_polished_soft_render_vn_style_anchor_v3.png` | **화풍만** 참조. 앵커 인물의 얼굴·머리·체형·복장·포즈는 복사하지 않는다. |
| Image 2 | `Assets/Art/Characters/Player/Pixel/HighMotion/player_pixel_walk8dir6_a_v1.png` | 플레이어 최신 정체성 참조. 과거 빨간 모자 버전을 무효화하는 **무모자·짧고 헝클어진 짙은 갈색 머리** 정본이다. |
| Image 3 | `Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png` | 누나 얼굴·머리·복장·색 정체성만 참조한다. |
| Image 4 | `Assets/Art/Characters/Father/father_office_neutral_v1.png` | 아빠 얼굴·안경·머리·복장·색 정체성만 참조한다. |
| Image 5 | `Assets/Art/Characters/Mother/mother_office_neutral_v1.png` | 엄마 얼굴·머리·복장·색 정체성만 참조한다. |

우선순위는 `Docs/CANON.md`, `Docs/ART_STYLE.md`, `Docs/DECISIONS.md`의 최신 무모자 정본이 과거 빨간 뉴스보이캡 결정보다 높다. 모든 최종본에서 플레이어의 모자·캡·머리에 쓴 후드를 제거했다.

## 3. 공통 최종 프롬프트

아래 공통 블록에 4절의 활동별 블록 하나를 이어 붙인 문장을 해당 장면의 최종 프롬프트로 정의한다. 목욕탕 장면에서는 활동별 복장 지시가 평상복 지시보다 우선한다.

```text
Use case: historical-scene
Asset type: 16:9 landscape Unity game leisure scene card and full-screen background
Input images: Image 1 is canonical SIMUL polished soft-render VN anime v3 style-only; use rendering grammar only and never copy its person's identity. Image 2 is the newest no-hat identity-only player reference and overrides older versions. Images 3-5 are identity-only references for older sister, father and mother.
Subjects: exactly four canonical family members. Boy: age 14, ABSOLUTELY NO HAT OR HEADWEAR, short tousled dark-brown hair fully visible, brown eyes, white hooded windbreaker with navy trim, navy-yellow-red striped shirt, dark navy trousers, white/navy sneakers. Older sister: adult age 20, long black twin tails with black bows, teal eyes, dark sleeveless tank, navy dolphin shorts with white piping, barefoot. Father: age 46, charcoal side-part hair and gray temples, silver rectangular glasses, dusty teal rolled-sleeve shirt, charcoal trousers, brown belt/shoes, analog watch. Mother: age 44, dark chestnut low half-up hair, dusty peach cardigan, cream blouse, deep teal A-line skirt, brown loafers, pearl earrings, analog watch.
Style/medium: bright casual Korean visual-novel scene illustration, canonical SIMUL polished soft-render VN anime v3; thin colored lines, layered warm shading, high facial and material detail; no pixel art, photorealism, hard cel shading, chibi or 3D.
Composition/framing: same brightness and eye-level camera as approved convenience-store scene; wide establishing shot safe for centered 1920x1080 16:9 crop; all faces, hands and key props inside central 70 percent, environment extending to edges; no panels or UI frame.
Constraints: no readable Korean, English, numbers or writing; all signs, books, screens, packages and labels blank or abstract. No title, caption, button, icon, logo, trademark, watermark or border. Exactly four people, no extras/duplicates, no character redesign, no hat on boy, no modern smartphone, LCD or LED signage.
```

## 4. 활동별 최종 프롬프트와 파일

### 4.1 편의점 간식

- 파일: `leisure_convenience_store_snack_run_v1.png`

```text
Scene: a real neighborhood Korean convenience store around 2000. The family chooses and eats cup noodles, triangular snacks and small drinks with blank abstract packaging. Show fluorescent ceiling lights, dense snack shelves, wire baskets, a beige cash register and glass refrigerators. The people and the physical store must both read clearly; warm everyday family humor, not a product advertisement.
```

### 4.2 PC방

- 파일: `leisure_pc_bang_team_match_v1.png`

```text
Scene: a compact Korean PC bang around 2000. All four sit together at adjacent beige CRT monitors with wired ball mice, thick keyboards, beige towers and visible network cables, sharing an excited team match. Screens show only abstract strategy-map shapes. Include a small snack shelf and period furniture; no modern flat screens, gaming chairs or RGB lighting.
```

### 4.3 비디오 대여

- 파일: `leisure_video_tape_rental_night_v1.png`

```text
Scene: a Korean VHS video-rental shop around 2000. The family browses dense shelves of recognizably thick VHS clamshell cases and carries a wire rental basket. Show a VHS rewinder, CRT preview television and counter equipment. Covers are abstract and unreadable; no DVD/Blu-ray cases or modern displays.
```

### 4.4 만화책 대여

- 파일: `leisure_comic_book_rental_stack_v1.png`

```text
Scene: a Korean comic-book rental shop around 2000. Show only small standard paperback tankobon-size comic volumes, tightly packed narrow book spines, low shelves, floor cushions and several clearly visible bundles of six rental books tied or stacked together. ABSOLUTELY NO LP records, vinyl, record sleeves, square album covers, oversized art books, large magazines or poster racks. Keep the family identities, wide composition and bright everyday mood.
```

### 4.5 동네 목욕탕

- 파일: `leisure_neighborhood_public_bath_v1.png`

```text
Scene: after bathing in a Korean neighborhood public bathhouse around 2000, exactly four family members rest together in the common public waiting lounge outside all bath and changing areas, drinking banana milk from plain unlabeled bottles. Show wooden shoe lockers, old massage chairs, a milk refrigerator and separate closed corridor doors; any wash area remains behind frosted glass. OVERRIDE normal outfits: everyone wears fully modest, loose, comfortable bathhouse loungewear in their canonical colors. No nudity, cleavage, exposed torso, towels worn as clothing, towel-only figures, mixed bathing, bath pool, shower room or changing-room interior.
```

### 4.6 가족 외식

- 파일: `leisure_family_restaurant_dinner_v1.png`

```text
Scene: a modest Korean bunsik or drivers' diner around 2000. The four share soup, rice, dumplings, gimbap and side dishes at a metal table. Include an old refrigerator, vinyl stools and blank menu panels. Make the food, small restaurant and relaxed family conversation all visible; no upscale modern restaurant styling.
```

### 4.7 저녁 산책

- 파일: `leisure_neighborhood_evening_walk_v1.png`

```text
Scene: a calm blue-hour family walk through a Korean neighborhood around 2000. Show a low brick wall, pocket park, utility poles and wires, old cars with blank plates, a public payphone, balconies and laundry. The family walks and talks naturally as a group; keep faces readable without turning the scene into a posed portrait.
```

### 4.8 강변 소풍

- 파일: `leisure_riverside_picnic_v1.png`

```text
Scene: a bright family picnic on a Korean riverside lawn around 2000. The four gather on a checkered mat with gimbap, fruit, metal thermos and plain cups. Include a simple steel bridge, old bicycles, reeds and a broad riverbank. Keep the wide environment crop-safe and free of modern skyline landmarks or branded picnic goods.
```

### 4.9 문방구 오락기

- 파일: `leisure_stationery_arcade_break_v1.png`

```text
Scene: the family visits a small Korean stationery-store frontage around 2000. Show two waist-high CRT arcade cabinets, plastic stools, a capsule-toy machine, notebooks and pencils. The boy and sister play while the parents cheer. Screens contain only abstract fighter silhouettes; no readable cabinet marquees, modern consoles or full-size neon arcade hall.
```

### 4.10 라디오 야식

- 파일: `leisure_home_radio_snack_chat_v1.png`

```text
Scene: late-evening family snack chat in their small officetel living-office lounge around 2000. The four relax near a cassette radio with visible tapes, a corded phone, CRT television, tangerines, crackers and a kettle, using a peach sofa and floor cushions. Warm lamplight, intimate but still wide enough to establish the cramped shared home-office; no smartphone or modern streaming device.
```

### 4.11 가족 노래방

- 파일: `leisure_family_singing_room_v1.png`

```text
Scene: a small Korean family singing room around 2001. Exactly four family members sing and laugh with wired microphones, a CRT showing only abstract audio waves, a tambourine, chunky remote and padded bench. No lyrics, song title, score, number, readable control label, extra staff or patrons.
```

### 4.12 ADSL 협동 게임

- 파일: `leisure_adsl_coop_game_night_v1.png`

```text
Scene: a cooperative game night in the family's tiny office around 2002. All four use four beige CRT monitors and towers connected through an early broadband modem, Ethernet hub and thick visible cables; include an unused corded phone and floppy disks. Screens show one abstract shared cooperative adventure map with no text. No Wi-Fi router, flat panel, smartphone, RGB setup or modern headset.
```

## 5. 무모자 정본 교정 프롬프트

편의점·PC방·비디오 대여·가족 외식은 승인된 장면의 구도와 소품을 보존하기 위해 아래 ImageGen 편집 프롬프트로 플레이어 머리 영역만 교정했다. 만화책 대여와 목욕탕은 장소 정확성 보완과 무모자 정본 반영을 함께 하기 위해 전체 재생성했다. 나머지 6종은 처음부터 무모자 정본으로 생성했다.

```text
Use case: identity-preserve
Asset type: correction of an approved 16:9 Unity game scene
Primary request: Change only the 14-year-old boy player's head in Image 1 to the newest canonical appearance from Image 2: remove the red newsboy cap completely and reveal short tousled dark-brown hair. No hat, cap, hood over head or headwear. Keep brown eyes, age, white hooded windbreaker, striped shirt and pose.
Input images: Image 1 is the edit target. Image 2 is the newest no-hat player identity reference and overrides the old red-cap appearance. Images 3-5 are identity references for older sister, father and mother.
Invariants: preserve Image 1's exact 16:9 composition, camera, crop, brightness, background, period props, exactly-four family count, poses, hands, expressions, clothing and object placement. Preserve older sister, father and mother exactly. Change only the boy's cap/head-hair region. Maintain canonical SIMUL polished soft-render VN anime v3.
Constraints: no text, letters, numbers, logo, watermark, caption, border or UI. Add/remove nothing.
```

## 6. 후처리와 Unity 임포트

- ImageGen 회수 원본: 1672×941 RGB.
- 최종화: 16:9에서 벗어나는 한 행 수준을 중앙 크롭한 뒤 Lanczos로 1920×1080 리사이즈하고 RGBA로 변환했다.
- 알파: 모든 픽셀 A=255. 배경 제거용 이미지가 아니므로 반투명 가장자리를 만들지 않았다.
- Unity 메타: 기존 비 UI 장면용 TextureImporter 설정을 계승했다. sRGB, 최대 2048, mipmap 없음, 기본 텍스처 타입이다.
- 원본 보존: ImageGen 생성 원본은 Codex 생성 이미지 저장소에 남기고, 프로젝트에는 Unity 사용 최종본만 둔다.

## 7. 자동·육안 QA

| 파일 | 크기/모드 | 평균 밝기 | SHA-256 | 육안 결과 |
|---|---:|---:|---|---|
| `leisure_adsl_coop_game_night_v1.png` | 1920×1080 RGBA | 106.58 | `8B00AF317B973A026444A9191E1F46CA1CF7C1098400B1D8AF06E791A7D913DD` | 무모자 4인, CRT·모뎀·허브·유선망 확인 |
| `leisure_comic_book_rental_stack_v1.png` | 1920×1080 RGBA | 115.06 | `49190B65DEA0B6BB93CCA021979F84AEB67A67C517352354C1EEE98785B3650D` | 작은 단행본·촘촘한 책등·낮은 책장·묶음책, LP 없음 |
| `leisure_convenience_store_snack_run_v1.png` | 1920×1080 RGBA | 121.83 | `F1E6A54134171793C178C6A0E90C159D3E715EF7C593E38EA82486A4CF3883F8` | 무모자 교정, 승인 구도·시대 소품 유지 |
| `leisure_family_restaurant_dinner_v1.png` | 1920×1080 RGBA | 100.69 | `E77639442F6A33AACA662B2915FE5F0529AA229D99D42087FBC783751BBDE509` | 무모자 4인과 서민 식당·한식 상차림 확인 |
| `leisure_family_singing_room_v1.png` | 1920×1080 RGBA | 84.74 | `2211A601EF9EAE823F45D12E269D650D8D0D9F65452AE70CBF633E045FFBD11B` | 유선 마이크·CRT·탬버린, 가사/점수 없음 |
| `leisure_home_radio_snack_chat_v1.png` | 1920×1080 RGBA | 100.75 | `9D9B368F36FBE7ACF8060FF5D37021BB6ABAA3119E8DA5354F2B7FD4C7DB9AF8` | 카세트 라디오·유선전화·CRT·야식 확인 |
| `leisure_neighborhood_evening_walk_v1.png` | 1920×1080 RGBA | 83.36 | `E0E7254A26766FE7F4CF5150BFE2D631A8005408CBECFC1054560E299CB55435` | 무모자 4인, 2000년 동네 블루아워 확인 |
| `leisure_neighborhood_public_bath_v1.png` | 1920×1080 RGBA | 115.43 | `0FE4542A8B68054AB1035571677928B5435EB6834B2D18A18ECAB97B5E4AC346` | 공용 대기 휴게실, 전원 단정한 휴게복, 혼욕·노출 없음 |
| `leisure_pc_bang_team_match_v1.png` | 1920×1080 RGBA | 98.23 | `240FBB94EEC65426C941B72764DAF5AEC63DDB77BB42BD45469EF86FD22791B2` | 무모자 교정, CRT·볼마우스·유선 장비 확인 |
| `leisure_riverside_picnic_v1.png` | 1920×1080 RGBA | 151.31 | `8AA3AB045CBE9B31542DAC3842EC82D0EB53C602E3B597DBB69144DB0DDD13CF` | 무모자 4인, 강변·자전거·돗자리 소품 확인 |
| `leisure_stationery_arcade_break_v1.png` | 1920×1080 RGBA | 91.20 | `65FC85A475DA41BA987C61B71CF3C1266DB8D6946B5F0DD38EE142A5F681CB86` | 문방구 앞 소형 CRT 오락기·캡슐기 확인 |
| `leisure_video_tape_rental_night_v1.png` | 1920×1080 RGBA | 90.26 | `A6479C7F283355A113E8A89FE1A28F5B745C43C863C1BCD087FC036404D34568` | 무모자 교정, VHS 케이스·되감기·CRT 확인 |

공통 육안 판정:

- 전 장면에 정확히 가족 4인이 있고 플레이어에게 모자·캡·머리에 쓴 후드가 없다.
- 누나·아빠·엄마의 정본 외형과 가족 색 구분이 유지된다.
- 인물과 장소가 함께 읽히는 밝은 가로 구도이며 중앙 70% 안전 영역에 얼굴·손·핵심 소품이 들어온다.
- 이미지 안에 읽을 수 있는 한글·영문·숫자, 버튼, 아이콘, 로고, 워터마크가 없다.
- 현대 스마트폰·평면 모니터·RGB 장비·LED 간판 등 연도 누설 소품이 없다.
- 목욕탕은 혼탕 내부가 아닌 목욕 후 공용 대기 휴게실이고, 노출과 수건만 두른 인물이 없다.
- 자동 검사 결과: `LEISURE_IMAGE_QA: PASS (12/12)`.
