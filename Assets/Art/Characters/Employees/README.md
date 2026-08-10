# Employee Character Asset Index

`simul`의 정본 8인 캐릭터를 가족회사의 향후 직원 후보 에셋으로 무변형 복사한 묶음이다.

## 구성

- `Portraits/`: 인물별 정본 전신 원화 9종. 원본 PNG와 바이트 단위로 동일하다.
- `References/`: 얼굴·복장 정체성 앵커.
- `Pixel/<id>_pixel_walk4x2_v1.png`: 정체성·카메라 참조용 레거시 4방향 2프레임 도트 시트.
- `Pixel/HighMotion/<id>_pixel_walk8dir6_{a,b}_v1.png`: 런타임 정본 8방향×6프레임 도트 시트.
- `Pixel/HighMotion/Frames/`: 상체 중심·발 기준선으로 정렬한 256×256 단일 Sprite 48개.
- `Pixel/Frames/`: Unity용 개별 방향 Sprite PNG 8종.
- `Pixel/Source/`: 투명화 전 마젠타 크로마 생성 원본.

## 인물

| 폴더 | 이름 | 성향 | 대표 원화 | 런타임 ID |
|---|---|---|---|---|
| `KimSeoa` | 김서아 | ISFJ | `01_neutral_notebook_v1.png` | `kim_seoa` |
| `LeeJian` | 이지안 | ISTP | `01_neutral_screwdriver_v2.png` | `lee_jian` |
| `ChoiIseo` | 최이서 | ISFP | `01_base_thread_v1.png` | `choi_iseo` |
| `JungArin` | 정아린 | ESTJ | `01_base_cheeky_v1.png` | `jung_arin` |
| `ParkHaeun` | 박하은 | ENFJ | `01_neutral_v3.png` | `park_haeun` |
| `HanSua` | 한수아 | ENFP | `01_neutral_wavy_v3.png` | `han_sua` |
| `OhJiwoo` | 오지우 | ENTP | `01_alert_neutral_v1.png` | `oh_jiwoo` |
| `YoonChaea` | 윤채아 | INTJ | `01_neutral_tie_v1.png` | `yoon_chaea` |

## 도트 시트 셀 순서

- HighMotion A 행: 남, 남서, 서, 북서. 각 행의 6열은 걷기 0~5.
- HighMotion B 행: 북, 북동, 동, 남동. 각 행의 6열은 걷기 0~5.
- 규격: 장당 1536×1024 RGBA, 6열×4행, 알파 0/255
- Unity: Point 필터, mipmap 없음, 무압축, 180 PPU
- 레거시 4×2는 위 행 정면·왼쪽·뒤·오른쪽 A, 아래 행 동일 방향 B다.

정체성, 머리, 복장, 소지품은 각 인물의 정본 원화에 맞추고, 체형과 얼굴을 서로 섞지 않는다.
