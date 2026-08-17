# Family Walk Identity-Locked Six-Pose Cycles

> **필수 선행 계약:** 새 보행 에셋이나 코드를 만들기 전에
> `Docs/FAMILY_WALK_ART_GUARDRAILS.md`의 `FC-WALK-GUARDRAIL-V1`과
> `FC-WALK-TWOSTEP-GATE-V1`을 전문 확인하고 작업 로그에 확인 문구를 남긴다.
> 두 걸음 게이트의 FAIL은 사람 눈으로 뒤집을 수 없고, PASS도 필요조건일 뿐 충분조건이 아니다.

이 폴더는 가족 4명 × 8방향 × 6프레임 보행의 유일한 추적 source layer다. 2026-08-18의
identity-locked candidate 61이 현재 정본이며, V4/V5/V6/V7/raw import 세대 이름은 승인 근거가 아니다.

## 추적 산출물

- `<member>/<direction>/<member>_<direction>_half_<0..5>.png`: 표식이 한 번도 닿지 않은 출하 source
  192장. 프레임마다 전신을 다시 생성하지 않고 방향별 canonical body/portrait와 분리된 두 하체를
  결정론적으로 합성한다.
- `IdentityModelV1/<member>_<direction>_identity_anchor.png`: 방향별 canonical identity anchor 32장.
  같은 가족의 머리 크기·얼굴 landmark·어깨/몸통 폭·골반 높이·옷 길이·다리/신발 크기를 고정한다.
- `MarkerReviewV1/<member>_<direction>_walk_<0..5>.png`: 출하 source의 별도 검수 사본 192장.
  왼다리/오른다리 표식만 다르고 알파 실루엣은 출하본과 픽셀 단위로 같다. 출하 픽셀에 표식을 칠한 뒤
  지우는 경로는 없다.
- 런타임은 `Assets/Art/Characters/{Player,OlderSister,Father,Mother}/Pixel/HighMotion/Frames`의
  192 frame과 가족별 2 sheet다. source, frame, sheet는 hard-alpha 정규화 뒤 byte-exact여야 한다.

## 제작 계약

- 프레임 의미는 왼발 접지·지지·오른발 낮은 통과·오른발 접지·지지·왼발 낮은 통과다.
- 3·4·5 하체는 0·1·2의 골반축 반사다. 얼굴·머리·의상·카메라 방향은 반사하지 않는다.
- 생성 예산은 가족당 `south, southeast, east, northeast, north` 5방향 × 첫 반주기 3패널 =
  15패널이다. 반대 방향과 두 번째 반주기는 결정론적으로 파생한다.
- 모든 frame은 256×256 RGBA, alpha 0/255, 동일 바닥선과 가족별 동일 키를 사용한다. 분리 조각,
  크로마, 잔상, 바닥 파묻힘은 허용하지 않는다.
- 실제 normal 런타임은 승인 walk/idle만 사용한다. `LocomotionTransitionsV1`의 다른 세대 초상화를
  production 가족에게 섞지 않는다.

## 유일한 publish 경로

`Tools/build_family_walk_half_cycles_v2.py`는 이 source tree를 검사해 runtime frame/sheet로 쓰는 단일
경로다. 구형 bootstrap/import/joint-rig/raw-strip 배타 CLI 모드는 제거됐다. `--write`는 먼저 source와
marker review copy의 두 걸음 게이트를 실행하며, PASS가 아니면 아무것도 쓰지 않는다.

```powershell
.\Tools\Verify-FamilyWalkTwoStep.ps1 -SelfTest
.\Tools\Verify-FamilyWalkTwoStep.ps1 -Source artsources
python .\Tools\build_family_walk_half_cycles_v2.py --check
python .\Tools\test_family_walk_half_cycles_v2.py
```

현재 추적 source의 필수 결과:

```text
FAMILY_WALK_ANATOMY_MARKER_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 source=artsources rows=32
FAMILY_WALK_TWO_STEP_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 source=artsources rows=32
```

candidate 61은 이 구조와 사람 검수를 모두 통과했다. candidate 62 재생성 시도는 두 걸음 게이트에서
실패해 repo source로 승격하지 않았다. probe 53은 구조 연구 기준으로만 보존한다. 표식을 출하본에 칠했다
지운 어두운 외곽이 남으므로 실제 출하 픽셀은 폐기했고, 그 실패 때문에 marker-copy 분리가 정본이 됐다.

Venture Tycoon 영상은 짧은 보폭·차분한 리듬·상체 안정성만 참고했고 그림은 복제하지 않았다. 신규 제3자
아트는 추가하지 않았으며 기존 프로젝트 권리 선언을 따른다.
