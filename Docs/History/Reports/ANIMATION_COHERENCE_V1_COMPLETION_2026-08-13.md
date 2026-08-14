> [!NOTE]
> 역사 구현 보고서입니다. 현재 정본·미완료·최신 검증은 [PROJECT_STATE.md](../../PROJECT_STATE.md)를 따릅니다.

# Animation Coherence V1 — P0-0 완료

날짜: 2026-08-13

## 완료 범위

- 12명 × 8방향 × 6프레임 = 576장 보행 에셋
- 12명 × 8방향 = 96개 보행 루프
- 기존 24개 원본 시트 보존: `Assets/Art/Characters/BeforeCoherenceV1/`
- 기존 runtime PNG 경로와 `.meta`/GUID 유지
- stale 방향·프레임 수동 승인 값 fail-closed 무효화

## 최종 결과

| 검사 | 결과 |
| --- | --- |
| walk strict | PASS 96/96 |
| 캐릭터 / 루프 / 프레임 계약 | 12 / 96 / 576 |
| median의 중앙값 | 6.545% |
| worst 최대값 | 22.477% |
| ratio 중앙값 / 최대값 | 0.4607 / 0.6899 |
| 고유 포즈 | 모든 루프 6/6 |
| foot drift / stable root drift / closure 최대 | 0px / 0px / 0px |
| 신규 상체 세로 alpha crack | 최대 증가 0px |
| canonical sheet ↔ frame exact | PASS 576/576 |
| hard alpha / native RGBA / 256×256 | PASS 576/576 |

## 제작 방식

프레임 0의 머리·머리카락·몸통·옷·팔을 하나의 canonical upper body로 유지한다. 원본 여섯 장에서 실제로 그려진 다리 접촉 포즈 15쌍을 전수 평가하고, 가장 좋은 두 접촉 포즈의 다리·발만 정수 X 이동으로 보간한다.

상체 픽셀을 부위별 사각형으로 잘라 재조합하지 않는다. 이 제한으로 장발과 옷 중앙에 생기던 투명 세로 틈, 전신 흔들림, 프레임별 정체성 변화가 제거된다. cross-dissolve, ordered dither, 회전, 확대·축소, 리샘플링은 사용하지 않는다.

## 재현 명령

```powershell
python Tools\stabilize_locomotion_cycles.py
python Tools\measure_animation_coherence.py --motion walk --strict
python Tools\split_high_motion_sheets.py --verify-only
python Tools\test_animation_coherence_gate.py
```

`stabilize_locomotion_cycles.py`는 기본적으로 검토 후보와 before/after contact sheet, GIF, JSON/TXT 보고서를 만든다. 실제 승격은 `--apply`를 명시해야 하며, 기존 `.meta`가 없거나 원본 백업이 일치하지 않으면 중단한다.

## Unity 검증

Unity 6000.3.21f1을 `-batchmode -nographics`로 실행하여 import/compile과 `FamilyCompany.Editor.PrototypeValidation.Run`을 검증한다. Unity 창을 띄우는 foreground 실행은 사용하지 않는다.

초기 승격 직후 background 검증은 `SCENE_LINKAGE_PASS`, `OFFICE_SEATING_BUILDER_VALIDATION: PASS`, `FAMILY_COMPANY_VALIDATION: PASS`로 완료됐다. 최종 seam-free PNG 재승격 뒤 같은 명령을 두 번 재시도했으나, 프로젝트/컴파일 오류가 아니라 로컬 Unity 라이선스가 `No valid Unity Editor license found`(return code 198)로 거부되어 최종 재import 실행은 완료하지 못했다. Python 에셋 계약·sheet round-trip·독립 시각 QA는 최종 파일 기준으로 모두 통과했다.

## 후속 범위

이 문서는 P0-0 보행 coherence 완료 기록이다. typing/mouse/drink 같은 착석 업무 micro-action의 별도 coherence 승격과 새 이미지에 대한 사람의 방향 시각 재승인은 후속 단계로 남는다.
