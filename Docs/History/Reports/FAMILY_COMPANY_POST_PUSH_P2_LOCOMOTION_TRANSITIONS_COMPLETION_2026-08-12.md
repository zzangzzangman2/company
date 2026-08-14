> [!NOTE]
> 역사 구현 보고서입니다. 현재 정본·미완료·최신 검증은 [PROJECT_STATE.md](../../PROJECT_STATE.md)를 따릅니다.

# Post-push P2 이동 전환 애니메이션 완료

기준 문서: `FAMILY_COMPANY_POST_PUSH_REVIEW_AND_TASKS_2026-08-12.md`

## 완료 범위

- 가족 4명(`player`, `older_sister`, `father`, `mother`)에 대해 8방향 전환 아트를 추가했다.
- 전환 클립은 `turn_in_place`, `walk_start`, `walk_stop`, `short_shuffle` 4종이다.
- 클립마다 방향별 2포즈를 사용하므로 캐릭터당 64장, 전체 256장의 독립 PNG다.
- 기존 8방향×6프레임 걷기 루프는 `Walk` 상태에서 그대로 사용한다.
- `DirectionalSpriteAnimator`의 기존 거리 기반 gait 상태를 다음 전용 아트 슬롯에 연결했다.
  - `Pivot` → `turn_in_place`
  - `StartStep` → `walk_start`
  - `Stopping`과 `Idle` → `walk_stop`
  - `ShortShuffle` → `short_shuffle`
- 에셋이 없을 때는 기존 걷기 프레임으로 되돌아가는 fail-safe 경로를 유지한다.

## 아트 생성·정규화

- OpenAI 내장 ImageGen을 사용했다.
- 각 가족의 기존 8방향×6프레임 원본 시트 2장을 캐릭터 참조로 사용했다.
- 공통 생성 조건은 다음과 같다.
  - 4×4 시트: 각 행에 두 방향, 방향당 A/B 2포즈
  - 행 순서: `South/Southwest`, `West/Northwest`, `North/Northeast`, `East/Southeast`
  - 기존 얼굴, 머리, 의상, 비율, 픽셀 밀도 유지
  - 과장된 달리기·점프·스쿼트·바운스 금지
  - 단색 마젠타 크로마 배경, 그림자·바닥·텍스트·격자 금지
- `Tools/Build-LocomotionTransitionFrames.ps1`가 크로마 제거, 분리된 알파 조각 제거, 하드 알파화, 256×256 분할을 수행한다.
- 모든 프레임은 발바닥 중심을 X=128, 바닥 여백 8px로 정규화했다. 방향 전환 때 피벗이 위아래 또는 좌우로 튀지 않는다.
- Unity Importer는 Sprite/Single, 180 PPU, Point, mipmap 없음, 무압축, 하단 중앙 피벗이다.

## 런타임 데이터

- `OfficeLocomotionTransitionCatalog`를 `Resources/HighMotion` 아래에 둔다.
- 카탈로그는 4명×4클립×8방향×2포즈 = 256개 슬롯을 모두 채워야 유효하다.
- 각 가족의 4개 원본 시트를 합친 SHA-256을 카탈로그에 저장한다.
- Starter Office가 시작될 때 카탈로그를 검증하고 가족 Actor 4명에게 연결한다.

## 검증 결과

- `OFFICE_LOCOMOTION_TRANSITION_ASSET_QA_PASS`
  - members=4, clips=4, directions=8, poses=2
  - slots=256, uniqueArt=256
  - hardAlpha=true, bottomPadding=8
- `OFFICE_LOCOMOTION_TRANSITION_RUNTIME_QA_PASS`
  - `StartStep`, `Walk`, `Stopping`, `Idle`, `ShortShuffle`, `Pivot` 상태 전환 검증
  - `Walk`만 기존 걷기 루프를 사용하고 나머지는 전환 스프라이트를 사용
- 기존 `OfficeNavigationValidation`: PASS
  - seeds=128, paths=1,152, segments=2,289, scenePaths=27
- 기존 `PrototypeValidation`: `FAMILY_COMPANY_VALIDATION: PASS`
- 가족별 8방향×8행 접촉 시트 4장을 육안 검사했다.

## P2 결론

원문이 요구한 서브셀 가구 충돌과 출발·정지·제자리 회전 전용 애니메이션을 모두 완료했다. 기본 걷기 루프 6→8 확대는 문서에서도 후속 선택 사항이므로 이번 범위에서는 유지한다.
