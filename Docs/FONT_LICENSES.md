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

## 향후 1920×1080 가로 UI 역할 제안

이 항목은 후속 UI 작업을 위한 역할 제안이며 현재 UI·씬을 변경하지 않는다.

- `MaplestoryBold`: 타이틀, 큰 탭 제목, 주요 버튼. 1080p 기준 28px 이상, 타이틀은 40~64px 권장.
- `MaplestoryLight`: 보조 제목과 짧은 HUD 명칭. 20px 미만의 작은 본문에는 사용하지 않는다.
- `PretendardVariable`: 본문, 계약 설명, 표, 날짜·금액·수치. 1080p 기준 최소 18px, 일반 본문은 20~24px 권장.
- 작은 한글은 넓은 행간과 높은 명암 대비를 우선하고, 사무실 화면 위에 놓일 때 반투명 배경이나 외곽선을 별도로 설계한다.
- `simul`의 세로 모바일 줄바꿈·좌표·패널 비율은 재사용하지 않는다. 1920×1080/16:9 PC 가로 풀화면과 넓은 사무실 동시 표시를 기준으로 새로 배치한다.
- 후속 시각 작업이 필요하면 밝고 캐주얼한 가로 ImageGen 비주얼을 먼저 만들고 Unity 글자·버튼은 이미지와 분리한다.
