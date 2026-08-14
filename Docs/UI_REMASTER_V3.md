# Family Company UI Remaster V3

기준일: 2026-08-14
기준 구현 순서: workforce capability candidate `32e9b9b09d397bd327fe9aad1c03f93a9c5ded62` → UI Remaster V3

## 범위와 시각 기준

UI Remaster V3는 따뜻한 한국 가족회사·목재 사무실 분위기의 캐주얼 경영 타이쿤 UI다. 순수 검정 bar, 납작한 회색 wireframe, 네온·유리·카지노·SF 금속, bitmap에 구운 글자를 사용하지 않는다. 화면은 크림색 종이, 청록 구조선, 산호색 선택 강조, 절제된 금색 고정구와 짙은 녹색 잉크 글자로 통일한다.

팔레트:

- warm cream `#FFF4D8`
- clear teal `#2F7771`
- coral `#F27662`
- sunny gold `#F2C35B`
- deep green ink `#203B3A`
- pale mint `#DCECE4`

사용자 거절 근거는 `Artifacts/UiRemasterV3/Before/user-rejected-1252x745.png`, 능력치 compact 회귀 근거는 `Artifacts/UiRemasterV3/Before/workforce-before-1280x720.png`에 보존한다.

## Typography contract

모든 실제 글자는 런타임 폰트로 그린다. heading은 `Assets/Fonts/Runtime/MaplestoryBold.ttf`, body는 `Assets/Fonts/Runtime/MaplestoryLight.ttf`, fallback은 `Assets/Fonts/Runtime/PretendardVariable.ttf`다. 출처와 라이선스는 `Docs/FONT_LICENSES.md`와 `Assets/Fonts/Licenses/LICENSE-Maplestory.txt`가 정본이다.

1280×720 최소 실제 pixel tier:

| tier | px | weight | alignment |
| --- | ---: | --- | --- |
| main title | 44 | Maplestory Bold | centered/left by screen |
| panel title | 28 | Maplestory Bold | left, vertical middle |
| card title | 20 | Maplestory Bold | left |
| workforce skill title | 18 | Maplestory Bold | left |
| body/status/XP | 16 | Maplestory Light or Bold asset by emphasis | left |
| top company/date | 18 | Bold/Light | left/center |
| bottom navigation | 17 | Bold | centered to icon axis |
| button | 16 | Bold | full center |
| caption | 14 | Light | only non-critical annotation |

autosize는 끈다. `MaplestoryBold.ttf` 자체가 굵은 파일이므로 TMP 합성 Bold를 추가 적용하지 않는다. body line-height 목표는 1.35, title은 1.20이다. Main Navigation은 표시 문자열을 dynamic TMP multi-atlas에 미리 넣고 누락을 오류로 기록한다.

## Responsive contract

- 기준 CanvasScaler는 1920×1080, match 0.5를 유지한다.
- 1280×720/1392×768에서 top HUD는 최소 56px, dock은 최소 88px다.
- 중앙 panel은 1280급에서 약 1040px, 1920에서 1120px, 큰 화면에서 최대 1180px다.
- workforce compact는 3열×2행이며 skill card 실제 높이 82px 이상, 본문 16px 이상이다.
- workforce 상태는 `체력`, `스트레스`, `신뢰`, `저항` 네 개의 짧은 동일 열로 분리한다.
- 실제 화면 QA는 text overflow 0뿐 아니라 workforce text rectangle collision 0도 요구한다.

## ImageGen 산출물

built-in `image_gen`만 사용했고 CLI/API fallback은 사용하지 않았다. 현재 채택 자산은 24개다. 모든 exact final prompt, 생성 call ID, 생성 원본 경로, 프로젝트 경로, alpha 보정 범위와 폐기 사유는 `Assets/Art/UI/Resources/UiRemasterV3/Generation/ui_remaster_v3_imagegen_ledger.json`에 기록한다.

현재 프로젝트 자산:

- `Assets/Art/UI/Resources/UiRemasterV3/Title`: title hero, logo frame, 4 button states, 2 save-slot states
- `Assets/Art/UI/Resources/UiRemasterV3/Title/Icons`: new, continue, load, settings, exit
- `Assets/Art/UI/Resources/UiRemasterV3/Loading`: V3 background plus V4 panel/progress track/fill/work icon. V4는 built-in 원본 RGB/alpha를 그대로 보존하고 투명 여백만 ledger의 crop rect로 제거했다.
- `Assets/Art/UI/Resources/UiRemasterV3/Common`: reusable modal frame, edge-to-edge V4 card normal/hover/featured/disabled, ornament-free V5 compact workforce card

Import contract:

- Sprite UI: true alpha, sRGB, mipmap off, Clamp, Bilinear, Uncompressed, PPU 100
- backgrounds: Default texture, max 4096
- frames: max 2048 or 4096 for common modal
- icons: max 512
- borders: V4 common card `140/140/140/140`, title button `230/170/230/170`, save slot `250/190/250/190`, logo `230/170/230/170`, V4 loading panel `120/100/120/100`, V4 track `150/60/150/60`, V4 fill `140/80/180/80`, modal `120/120/120/120`
- V5 compact card의 Sprite border는 `120/120/120/120`으로 보존하지만, 72–86px 셀에서는 코너 찌그러짐을 없애기 위해 `Image.Type.Simple`로 렌더한다. built-in ImageGen RGB는 그대로 유지하고, 모델이 잘못 내보낸 checker/opaque 외부만 측정된 둥근 사각 alpha mask로 교정했다. 네 모서리 alpha는 0이며 가장자리에는 128/255 anti-alias 샘플이 존재한다.

## 최종 능력치·해상도 QA

- release player: `Artifacts/UiRemasterV3/Player/FamilyCompanyUiRemasterQa.exe` (`BuildOptions.None`, D3D11)
- 자동 보고서: `Artifacts/UiRemasterV3/PlayerCaptures-FinalCandidateV4/main-navigation-hud-player-qa.txt`
- 능력치 캡처: `Artifacts/UiRemasterV3/PlayerCaptures-FinalCandidateV4/menu-people-1280x720.png`, `menu-people-1392x768.png`, `menu-people-1920x1080.png`
- 로딩 캡처: `Artifacts/UiRemasterV3/LoadingCapture-Final/1280x720-v4-final-pass/starter-office-loading.png` (D3D11, 1280×720, framebuffer black gate PASS)
- 검증 해상도: 1280×720, 1392×768, 1600×900, 1600×1000, 1920×1080, 2560×1440
- 계약: MapleStory font, panel 28px 이상, 이름 18px 이상, body 16px 이상, autosize off, TMP overflow 0, workforce text rectangle collision 0
- compact 능력치는 1280급에서 3열×2행, 큰 화면에서 2열×3행이다. 모든 셀은 layout group이 남은 폭을 동일 분배하므로 수동 폭 반올림에 따른 우측 overflow가 없다. 공용 modal이 읽기 면을 제공하므로 workforce 전체/상세에 중복 장식 frame을 두지 않으며, 개별 직원·능력치·상태 카드만 generated compact Sprite를 사용한다.

## Legacy/rejected removal candidates

기존 `MainNavigationV2` runtime assets는 capability candidate와 route 호환 때문에 이번 후보에서 파괴적으로 삭제하지 않는다. Title V2 money-rain resource 연결과 코드 생성 검은 fallback은 V3 연결 후 reference 0을 검증한 다음 중앙 통합에서 제거 후보로 판단한다. 장식과 외부 padding이 능력치 글자를 침범했던 V3 common card 4종과 투명 여백이 과했던 loading V3 frame/track/fill/icon 4종은 superseded removal candidate이며 런타임 reference는 0이다. 거절된 불투명/checker/alpha-halo ImageGen 출력과 검은 Hidden 캡처 시도는 `Artifacts/UiRemasterV3/Rejected` 또는 비최종 QA 폴더에 보존하고 프로젝트 Assets에는 연결하지 않는다.
