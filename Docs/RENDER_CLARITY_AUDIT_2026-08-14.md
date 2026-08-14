# 렌더링 선명도·픽셀 안정성 감사 — 2026-08-14

## 범위와 기준

- 기준 해상도는 `1920×1080`, 보조 검증은 `1600×900`, `1600×1000(16:10)`, `2560×1440`이다.
- 시작 기준은 `52a787f7c821b3297c1118299bad003089b7362c`이며 격리 브랜치
  `codex/render-clarity-pixel-stability`에서만 작업했다.
- 시작 시 Downloads 실행본은 같은 commit의 clean Release, Unity `6000.3.21f1` 빌드였다.
- `OfficeRuntimeAgent`, `DirectionalSpriteAnimator`, 보행 PNG, 벽/출입구, 좌석/착석, 체력 UI,
  상·하단 HUD 파일과 자산은 수정하지 않았다.

## 원인별 판정

| 감사 항목 | 판정 | 증거 |
| --- | --- | --- |
| 실제 Windows 출력 | 정상, 원인 아님 | Downloads 실행본 Win32 실측은 outer/client `1920×1080 @ 0,0`, borderless style, DPI 96, `PerMonitorV2`다. PlayerSettings도 native resolution, borderless fullscreen, fixed DPI factor 1.0이다. |
| 고정 저해상도 후처리 | **주원인** | `PixelatedCameraEffect`가 `1920×1080` 카메라를 `960×540`으로 내린 뒤 Point로 다시 확대했다. 결과적으로 월드만 2×2 출력 블록이 되고 Overlay UI는 영향을 받지 않았다. |
| URP / Render Scale / Dynamic Resolution / Upscaler | 원인 아님 | Built-in Render Pipeline이며 URP/HDRP, Pixel Perfect, post-processing/upscaler package가 없다. 최종 ScalableBuffer는 `1.00×1.00`, camera dynamic resolution은 false다. |
| 화면 sharpening | 사용하지 않음 | halo/noise를 만드는 전체 화면 sharpening을 추가하지 않았다. |
| Camera | 부분 원인 | Orthographic fit 자체는 office bounds를 보존했지만 물리 픽셀 grid 정렬이 없었다. 시작 `orthographicSize=7.048611`, runtime SpriteRenderer 73/73의 anchor가 fractional screen coordinate였다. |
| PixelPerfectCamera | 없음 | package/component 모두 없다. 기존 180 PPU와 전체 사무실 fit을 보존하고 전용 presentation snap을 사용한다. |
| TextureImporter | 활성 도트 자산은 정상 | 1,560 PNG가 Sprite, Point, mipmap off, Standalone uncompressed, max size 원본 보존, 180 PPU다. |
| Sprite Atlas | 없음 | `.spriteatlas`/`.spriteatlasv2`가 없어 atlas compression/padding에 의한 열화는 없다. |
| Painted/UI asset | 별도 정책이 맞음 | 대표 고해상도 UI 4개는 Bilinear를 유지한다. 월드 도트 정책을 UI에 전역 강제하지 않는다. |
| CanvasScaler/TMP | 월드 열화와 별개 | runtime CanvasScaler 2개는 모두 `1920×1080`, match 0.5다. 월드 카메라의 540p downsample 뒤에 그려지는 Overlay라 기존에도 상대적으로 선명했다. |
| MSAA | 주원인 아님 | Ultra의 4× MSAA는 540p 정보 손실을 복원하지 못한다. 최종 도트 프로파일은 native Point 경계를 일관되게 유지하도록 0으로 고정한다. |

## 원본 크기와 실제 화면 밀도

활성 월드 자산은 모두 180 PPU이므로 같은 카메라에서 source pixel당 화면 pixel 비율이 같다.

| 분류 | 대표 원본/런타임 크기 | 1920×1080 화면 비율 | 판정 |
| --- | ---: | ---: | --- |
| 바닥 | `320×160` | `0.425616 screen px/source px` | 원본 부족보다 전체 사무실 fit에 의한 축소가 지배적이다. |
| 가구·벽 | `640×512` | `0.425616` | 전화선·키보드·얇은 테두리 일부는 1 화면 픽셀보다 작다. 540p 중간 버퍼가 이를 더 합쳤다. |
| 캐릭터 | `256×256` | `0.425616` | 얼굴/눈은 최종 화면에서 수 픽셀이다. Bilinear는 녹이고, native Point+snap은 실루엣을 보존한다. |

- 기존 540p 경로의 내부 비율은 `0.212808 internal px/source px`였고 그 결과를 2배 출력했다.
- 최종 1080p는 중간 축소 없이 `0.425616`이다. `1600×900`은 `0.354680`, 1440p는 약
  `0.5675`다.
- 따라서 이번 현상의 대부분은 런타임 샘플링 문제이며, 소스가 단순히 저해상도라서 생긴 문제가 아니다.
  다만 전체 사무실을 한 화면에 넣는 구도에서는 눈·전화선·키보드 키처럼 원래부터 1 화면 픽셀 미만인
  세부를 추가로 복원할 수 없다. 이번 변경에서는 PNG 자동 업스케일·재생성을 하지 않는다.

## 구현

- `PixelClarityDefault.asset`은 1920×1080, native scale 1.0, legacy half-height off, camera/actor
  presentation snap on, PPU 180, AA 0, mip limit 0을 선언한다.
- `PixelClarityRuntime`은 새 카메라가 생긴 첫 프레임부터 legacy effect를 끄고 dynamic resolution을
  금지한다. 해상도/aspect/layout 변경 때 office bounds를 다시 fit하고 카메라를 물리 픽셀 grid에 맞춘다.
- 움직이는 비착석 캐릭터는 simulation root를 바꾸지 않는다. `onPreCull`에서 SpriteRenderer 표현만 최대
  반 픽셀 이동하고 `onPostRender`에서 즉시 복원한다. 좌석/착석 시스템은 건드리지 않는다.
- `RenderClarityValidation`은 프로파일, pipeline/player 설정, 도트 1,560개 importer, painted UI 경계,
  atlas 부재를 fail-closed로 검사한다.
- `RenderClarityRuntimeQa`는 디버그 UI를 남기지 않고 legacy 540p, native Point unsnapped, native
  Bilinear, 최종 native stable을 같은 프레임에서 D3D11 RenderTexture로 읽는다. 검은/균일 캡처는
  밝기 범위와 유효 픽셀 수로 실패한다.

## QA 결과

| 경로 | 결과 | 핵심 수치 |
| --- | --- | --- |
| Editor render validation | PASS | pixel importers 1,560, painted UI 4, atlas 0 |
| `PrototypeValidation` | PASS | `FAMILY_COMPANY_VALIDATION: PASS` |
| Windows x64 Release build | PASS | Unity 6000.3.21f1, warnings 0, 756,979,617 bytes |
| 1920×1080 D3D11 | PASS | 실제 window/camera 1920×1080, DPI 96, render scale 1.0, movement 12 frames |
| 1600×900 D3D11 | PASS | 실제 window/camera 1600×900, crop/stretch 없음 |
| 1600×1000 D3D11 | PASS | 실제 16:10, ortho `7.831790`로 증가해 폭을 보존 |
| 2560×1440 D3D11 | PASS (offscreen target) | 물리 모니터가 1920×1080이라 window는 2560×1080에 제한, GPU target/capture는 정확히 2560×1440 |
| 기존 Starter Main Flow | PASS | 8방향, 교차/좁은 통로, 동적 가구, 4× 충돌, 네 좌석, 계약, 저장/불러오기, agent penetration 0 |

- 1920 이동 시퀀스에서 실제 이동 거리는 `0.513567 world unit`, 각 렌더에서 4명 presentation snap,
  최대 snap은 `0.5048px`였다.
- 0.35px씩 요청한 카메라 이동 8프레임에서 world origin의 screen residual은 매 프레임 `(0,0)`이고
  출력 이동은 정수 픽셀 단계다.
- 동일 1:1 디테일 crop의 평균 절대 경계 에너지는 legacy `15.254203`, native Bilinear `31.605309`,
  최종 native stable `33.520332`다. 최종은 legacy 대비 약 2.20배의 얇은 경계 정보를 보존한다.

## 통합 경계

- 이 작업은 새 C#·meta·ScriptableObject 파일만 추가했다. 제한된 병렬 작업 파일과 직접 overlap이 없다.
- 작업 중 canonical main은 `52a787f`에서 `715ef10`으로 전진했고 벽/출입구 작업의 미완료 변경도
  존재했다. 이 브랜치를 그대로 merge하지 말고 commander 신호 후 최신 clean main에 새 파일과 문서
  append를 재적용한 뒤 전체 빌드를 다시 만든다.
- Downloads/Family는 이 작업이 덮어쓰지 않았다. commit/push/deploy도 하지 않았다.
