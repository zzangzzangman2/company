# Mother Side Walk V3

계약 ID: `FC-MOTHER-SIDE-WALK-V3`

엄마의 east/west 보행에서 머리·몸통은 옆을 보지만 발끝은 화면 정면/반대쪽으로 벌어진 결함을 교체하는
추적 원본이다. `mother_east_six_pose_raw_v1.png`는 built-in imagegen으로 생성한 east 온몸 6포즈이며,
`Tools/build_mother_side_walk_v3.py`가 green key, 225px 전신/y247 정렬을 수행한다. west는 east를 정확히
좌우 반전해 파생한다. runtime 직접 쓰기는 `generate_character_locomotion_v1.py`만 담당한다.

## 생성 입력과 채택 기준

- identity reference: `ArtSources/FamilyWalkHalfCyclesV2/IdentityModelV1/mother_east_identity_anchor.png`
- pose/direction reference: 이전 생성 시안과 동일 엄마 side profile
- raw SHA-256: `DB24D3B44BDA89C978CBCE5A7D583A260B0D70D9CF0E0633DE29745B7EE83E32`
- 채택 시안: built-in imagegen의 다섯 번째 결과. 첫 결과는 일부 뒤발 앞코가 west를 향해 폐기했고,
  두 번째/세 번째는 같은 다리 가림이 반복돼 폐기했다. 네 번째는 반대 leg ownership을 과장하면서 치마와
  무릎을 행진 자세로 왜곡해 폐기했다. 다섯 번째는 P4/P5 하체만 다시 낮은 발목 높이 스윙으로 고쳤다.

최종 prompt의 핵심은 여섯 패널을 contact A, support A/swing B, passing B, contact B,
support B/swing A, passing A로 지정하고, 모든 패널에서 `rounded toe box=RIGHT`, `narrow heel=LEFT`를
강제한 것이다. 얼굴·머리·피치 카디건·크림 블라우스·청록 치마·갈색 로퍼·시계 identity를 유지하고
녹색 단색 배경, 동일 크기/바닥선, 잘림·그림자·문자·추가 사지를 금지했다.

## 재현

```powershell
py -3 Tools/build_mother_side_walk_v3.py --write
py -3 Tools/build_mother_side_walk_v3.py --check
py -3 Tools/generate_character_locomotion_v1.py
py -3 Tools/verify_character_locomotion_v1.py `
  --candidate-root Artifacts/CharacterLocomotionGenerationV1/Candidate
```

`mother_side_walk_v3_manifest.json`은 raw SHA, 12개 RGBA frame hash,
head/torso/pelvis/knees/ankles/shoe-toe 방향과 support-leg 교대의 사람 승인 의미를 고정한다. raw·manifest·
frame 중 하나라도 달라지면 builder와 runtime writer가 모두 fail-closed한다.
