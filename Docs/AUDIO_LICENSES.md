# Audio sources and licenses

이 문서는 가족회사 게임에 포함된 BGM·효과음의 출처와 라이선스를 보관하는 개발 문서다.
게임 화면, 타이틀, 설정, 엔딩에는 저작권·크레딧 문구를 노출하지 않는다. 배포 시 필요한
표기는 릴리스 메타데이터와 별도 크레딧 자료에서 처리한다.

이관일: 2026-08-10

- 읽기 전용 원본: `C:/Users/godho/Documents/Codex/simul/flutter_app/assets/audio/`
- Unity 경로: `Assets/Audio/Resources/Audio/`
- 이관 방식: 파일명과 바이트를 바꾸지 않은 무변형 복사
- UI 경계: `simul`의 세로 모바일 화면·좌표·레이아웃은 복사하지 않고 오디오 에셋과 런타임 규칙만 Unity용으로 이관
- 검증: BGM 11개와 SFX 39개, 총 50개 SHA-256 원본 일치
- 정렬 파일 집합 SHA-256: `1521D5FD63C7114C6E05368B67AC832EC0E5765525339705198AAD5F4E4CAE3C`

## BGM — PeriTune

- 제작자: PeriTune / Sei Mutsuki
- 라이선스: CC BY 4.0
- 이용 안내: <https://peritune.com/about/>
- 표기 문구: `Music: PeriTune <https://peritune.com/>`

| 게임 파일 | 원곡 | 원본 페이지 | 주 사용처 |
| --- | --- | --- | --- |
| `title_gentle_theme.ogg` | Gentle Theme (Piano) | <https://peritune.com/blog/2018/04/24/gentle_theme/> | 타이틀·로딩 |
| `story_hesitation.ogg` | Hesitation | <https://peritune.com/blog/2022/02/26/hesitation/> | 긴장 장면 |
| `story_piano_sad.ogg` | Piano Sad2 (Strings) | <https://peritune.com/blog/2018/09/06/piano_sad2/> | 계약 실패·부도·회상 |
| `finance_sakuya.ogg` | Sakuya | <https://peritune.com/blog/2015/11/21/sakuya/> | 은행·정산·경영 |
| `hub_gentle_brew.ogg` | Gentle Brew (official loop) | <https://peritune.com/blog/2025/01/22/gentle_brew/> | 사무실·일상 허브 |
| `hub_verdure.ogg` | Verdure3 | <https://peritune.com/blog/2016/12/12/verdure3/> | 집·일상 장면 |
| `relationship_raindrop.ogg` | RainDrop | <https://peritune.com/blog/2022/07/11/raindrop/> | 가족 대화·관계 이벤트 |
| `market_portside_cafe.ogg` | Portside Café (official loop) | <https://peritune.com/blog/2026/03/13/portside-cafe/> | 주식·PC·시장 |
| `action_strategy.ogg` | Strategy5 | <https://peritune.com/blog/2017/12/30/strategy5/> | 액션·시간 압박 |
| `horse_racing_prairie4.ogg` | Prairie4 (official loop) | <https://peritune.com/blog/2019/03/01/prairie4/> | 원본 호환 보존 |
| `casino_taisho.ogg` | TaishoRoman Theme2 | <https://peritune.com/blog/2020/11/19/taishoroman_theme2/> | 원본 호환 보존 |

음원은 원본 OGG를 반복 재생하고 런타임 볼륨만 조절하며 편집하지 않는다.

## SFX — Kenney

- 제작자: Kenney
- 라이선스: CC0 1.0 (퍼블릭 도메인)
- 이용 안내: <https://kenney.nl/support>
- 저작자 표기는 의무가 아니지만 출처 추적을 위해 이 문서에 기록한다.

| 원본 팩 | 원본 페이지 | 게임 내 용도 |
| --- | --- | --- |
| Interface Sounds | <https://kenney.nl/assets/interface-sounds> | 클릭, 선택, 확인, 오류, 알림, 토글, CRT |
| RPG Audio | <https://kenney.nl/assets/rpg-audio> | 책, 종이, 동전, 문, 발걸음, 금속 잠금 |
| Impact Sounds | <https://kenney.nl/assets/impact-sounds> | 벨, 충돌, 타격 |
| Casino Audio | <https://kenney.nl/assets/casino-audio> | 카드, 칩, 주사위 원본 호환 보존 |

## Horse racing and crowd SFX — OpenGameArt / Freesound

| 게임 파일 | 원본·제작자 | 라이선스 | 원본 페이지 |
| --- | --- | --- | --- |
| `horse_gallop_loop.ogg` | `ground.mp3`, D4XX의 `Single Horse Galopp`를 congusbongus가 루프용으로 편집한 파일 | CC0 1.0 | <https://opengameart.org/content/horse-gallop-on-different-surfaces> |
| `crowd_ambience.ogg` | `Crowd Shouting/Speaking Ambience`, StarNinjas | CC0 1.0 | <https://opengameart.org/content/crowd-shoutingspeaking-ambience> |
| `crowd_victory.ogg` | `Well Done`, qubodup | CC0 1.0 | <https://opengameart.org/content/well-done> |

`horse_gallop_loop.ogg`는 원본 묶음의 `credits.txt`에서 CC0로 개별 표기된
`ground.mp3`만 사용해 3.09초 OGG 루프로 변환된 `simul` 파일을 그대로 이관했다.
묶음 안의 CC BY 파일은 포함하지 않았다.

## 제외한 음원

테일즈위버 원곡은 무료 청취·OST 공개와 게임 내 재배포 허가가 별개이고 넥슨이 OST
저작권을 보유한다고 명시하므로 포함하지 않았다. 명시적인 재사용 허가를 확보하기 전에는
프로젝트에 복사하지 않는다.

- 넥슨 저작권 고지: <https://tales.nexon.com/News/Event/370>
- 공식 OST 판매 안내: <https://tales.nexon.com/News/Notice/116305>
