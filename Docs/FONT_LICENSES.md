# Font sources and licenses

이 문서는 가족회사 게임에 포함된 폰트 원본과 라이선스 조건을 보관하는 개발 원장이다.
폰트는 `simul`의 세로 모바일 UI·좌표·패널 비율과 분리해 이관했으며, 이 작업에서는 UI 코드나
씬에 폰트를 연결하지 않았다.

이관일: 2026-08-10

- 읽기 전용 원본: `C:/Users/godho/Documents/Codex/simul/flutter_app/assets/fonts/`
- Unity 폰트 경로: `Assets/Fonts/Runtime/`
- 원본 라이선스 경로: `Assets/Fonts/Licenses/`
- 이관 방식: TTF와 라이선스 파일의 이름과 바이트를 바꾸지 않은 무변형 복사
- 검증: TTF 3개와 라이선스 2개, 총 5개 SHA-256 원본 일치
- 정렬 파일 집합 SHA-256: `1017B5F9AF25595E322EDA85827BEBF32FCA7D20899AA32D2B9B08D06C76D22A`

## Maplestory typeface

- 파일: `MaplestoryBold.ttf`, `MaplestoryLight.ttf`
- 저작권자: NEXON Korea Corporation
- 공식 출처와 이용 조건: <https://maplestory.nexon.com/Media/Font>
- 허용: 개인·기업 사용, 애플리케이션과 함께 번들·임베드
- 조건: 동봉 저작권 고지를 유지한다.
- 금지: 배포 폰트 파일 수정, 폰트 파일 자체의 단독 판매
- 동봉 고지: `Assets/Fonts/Licenses/LICENSE-Maplestory.txt`

원본 고지의 핵심 문구:

> Copyright (c) NEXON Korea Corporation. All rights reserved.

> This application uses the Maplestory typeface provided by NEXON Korea.

## Pretendard

- 파일: `PretendardVariable.ttf`
- 저작권자: Kil Hyung-jin / orioncactus
- 공식 프로젝트: <https://github.com/orioncactus/pretendard>
- 라이선스: SIL Open Font License 1.1
- 허용: 사용, 연구, 복사, 임베드, 수정, 재배포와 소프트웨어 번들 판매
- 조건: 폰트 소프트웨어 재배포 시 저작권 고지와 OFL 1.1을 함께 제공한다.
- 제한: 폰트 자체 단독 판매 금지, 수정본에 Reserved Font Name `Pretendard` 사용 금지
- 전체 라이선스: `Assets/Fonts/Licenses/LICENSE-Pretendard.txt`

## 원본 무결성

| 파일 | SHA-256 |
| --- | --- |
| `LICENSE-Maplestory.txt` | `39781A6889C686A3D552BC63049596F6198DFD1572C7C96BA766DBAB7329AA10` |
| `LICENSE-Pretendard.txt` | `85FCE85E25260B03777BF10373D3BD9363B9DA96D9E0CA86A280DD37ED7667A0` |
| `MaplestoryBold.ttf` | `D57EAFF48A793FF872A0F33BBA2943D058D07C81ED64C68054858A287B85811A` |
| `MaplestoryLight.ttf` | `6D51D8E576F77B01914095AA1F69F9D37C16D93FE940D748962867F218442BA9` |
| `PretendardVariable.ttf` | `3090CCDE0442BB347AA7685D9BA8B17436A60682DF6E8F92A9A670DE14056E22` |

## UI Remaster V3 런타임 역할

- `MaplestoryBold`: 타이틀, 패널·카드 제목, 선택 탭, 주요 버튼.
- `MaplestoryLight`: 본문, 설명, 날짜·시간·금액·능력치 등 모든 일반 런타임 텍스트.
- `PretendardVariable`: 메이플 폰트에 없는 기호를 위한 fallback 전용. 화면별 기본 글꼴로 사용하지 않는다.
- 1280×720 최소 기준은 패널 제목 28px, 카드 제목 20px, 본문 16px, 상단 18px, 하단 17px, 버튼 16px이다. autosize로 이 값 아래로 축소하지 않는다.
- 작은 한글은 충분한 행 높이와 높은 명암 대비를 우선하고, `MaplestoryBold.ttf`에는 TMP 합성 Bold를 중복 적용하지 않는다.
- `simul`의 세로 모바일 줄바꿈·좌표·패널 비율은 재사용하지 않는다. 1920×1080/16:9 PC 가로 풀화면과 1280×720 최소 화면을 함께 검증한다.
- ImageGen bitmap에는 글자를 굽지 않고 Unity 런타임 TMP/IMGUI 텍스트를 분리해 렌더링한다.

## 2026-08-11 / TMP Essential Resources 동반 폰트

- `Assets/TextMesh Pro/Fonts/LiberationSans.ttf`는 Unity가 TextMesh Pro Essential Resources로 배포하는 서드파티 폰트이며 SIL Open Font License 1.1을 따른다. 저장소에 넣은 이유는 Docs/DECISIONS.md의 2026-08-11 항목에 있다.
- 이 폰트는 TMP 기본 폰트 에셋과 폴백 용도로만 존재하며 한글 UI 정본이 아니다. UI Remaster V3의 한글 정본은 Maplestory Bold/Light이고 Pretendard Variable은 기호 fallback이다.
- `ManagementUiV2Presenter`는 한글 카탈로그가 비었거나 불완전할 때만 Unity 내장 `LegacyRuntime.ttf`로 폴백하고 `MANAGEMENT_UI_FONT_FALLBACK` 오류를 남긴다. 이 오류가 보이면 한글 정본 폰트가 빠진 것이므로 폴백에 의존해 출시하지 않는다.
