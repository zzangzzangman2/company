# Player Walk Rig V2 authoring source

`FC-PLAYER-WALK-RIG-V2`는 주인공 보행을 런타임 리그가 아닌 고정 PNG로 굽기 위한 Editor 전용 원본이다.
현재 승인 대기 범위는 `south` 한 방향뿐이며, 다른 7방향·가족·직원에는 복제하지 않는다.

## 현재 후보

- 외형 기준: `Assets/Resources/FamilyCompany/PlayerSouthContactV1/Frames/player_south_contact_0_v1.png`
- PSB: `PlayerWalkRig_south.psb` (`PSB v2`, 384×512, 17 rigid pixel layers)
- 얼굴·모자·머리·재킷·몸통은 승인된 픽셀을 유지한다.
- 팔과 하체만 좌우 ownership이 고정된 별도 파츠다.
- Unity authoring은 `LimbSolver2D` 2개와 `IKManager2D`를 사용한다.
- 런타임 후보는 단일 `SpriteRenderer`가 읽는 8장 PNG다. 리그·PSB·2D IK는 Player에 포함하지 않는다.

레이어와 관절 좌표의 기계 판독 정본은 `south-layer-manifest.json`, 베이크 입력 정본은
`rig-contract.json`, 베이크 결과 정본은
`Assets/Resources/FamilyCompany/PlayerBakedWalkV2/source-receipt-south.json`이다.

## 재생성

```powershell
python -m pip install psd-tools==1.17.4 --target work\python_deps
python Tools\build_player_walk_rig_v2_source.py
```

그 뒤 Unity `6000.3.21f1` 숨김 배치모드에서 차례로 실행한다.

```text
FamilyCompany.Editor.PlayerSouthWalkRigV2AuthoringBuilder.RunFromCommandLine
FamilyCompany.Editor.PlayerWalkRigV2Baker.RunFromCommandLine   (D3D11 필요)
FamilyCompany.Editor.PlayerBakedWalkV2Validation.RunSouthCandidate
```

사람 검토용 작은 결과는 다음 명령으로 `Artifacts/PlayerWalkV2/SouthQa`에만 만든다.

```powershell
python Tools\build_player_walk_v2_qa_artifacts.py
```

## 승격 경계

- 기본 게임은 원본 48프레임 `Legacy48`을 계속 사용한다.
- `PlayerNaturalWalkV1`과 불완전한 V2는 명시적 command-line flag 없이는 활성화되지 않는다.
- 8방향 각각의 실제 row와 receipt가 모두 없으면 V2 catalog를 만들지 않는다.
- south 사람 승인 전에는 다른 방향을 만들지 않는다.
- 실제 Windows D3D11 Player trace와 사람 검토 전에는 배포·Downloads 교체·최종 완료 판정을 하지 않는다.

FC-WALK-GUARDRAIL-V1 확인: 0/3 해부학적 앞발 교대, 2/5 낮은 통과발 교대, 짧은 보폭, 동일 실루엣, 별도 전환 그림 금지, actual normal EXE 판정 전 미배포.
