# PROJECT STATE

이 문서는 과거 작업 일지가 아니라 **현재 실행 가능한 상태, 아직 통합되지 않은 상태, 정확한 다음 작업**만 기록하는 정본이다. 날짜별 구현 증거는 `History/Reports/`에 보존하며 이 문서보다 우선하지 않는다.

## 2026-08-28 / Father V19 standalone rig 사용자 기각 — third leg와 인형 보행 원인 확정

- 사용자 승인 Tripo H3.1 정적 외형은 유지한다. 별도 Meshy `3d_rigging` + action 613 결과는 사용자가
  전신/하체 확대에서 세 번째 다리와 인형 같은 보행을 확인해 기각했다. Unity에는 가져오지 않았다.
- 8-credit 작업은 정확히 1회만 완료됐고 balance는 `46 -> 38`; 자동 재시도는 없었다.
- raw GLB 구조 검사에서 strong arm/leg mixed vertices `6,547`, lower-body left/right leg mixed
  vertices `898`, hip-joint separation/body-width `0.01933`, animated edge stretch max `10.76x`가
  확인됐다. 정적 Tripo surface는 `132` disconnected components와 arms-down pose라 standalone
  autorigger가 limb/garment ownership을 잘못 분배했다.
- standalone `3d_rigging`에는 prompt/remesh/topology/pose 제어가 없으므로 같은 요청 반복을 금지한다.
  `Tools/Blender/validate_generated_biped_skin_glb.py`를 Unity 이전 강제 fail-closed gate로 추가했다.
- animation-ready 대안은 처음부터 A-pose + quad remesh + rig + action 613을 같이 만드는 Meshy
  `multi_image_to_3d` one-package뿐이다. 읽기 전용 비용은 정확히 `38 credits`, 현재 잔액도 `38`이며
  아직 제출하지 않았다. 별도 사용자 승인 없이는 제출하지 않는다.
- 상세 근거:
  [FATHER_V19_TRIPO_STANDALONE_RIG_REJECTION_2026-08-28.md](FATHER_V19_TRIPO_STANDALONE_RIG_REJECTION_2026-08-28.md).
  `productionEligible=false`; production/default/Downloads/배포본은 변경하지 않았다.

## 2026-08-27 / 사용자 잠금: reference 방향 유지 + 정확히 두 다리

- 사용자 제공 `C:\Users\godho\Downloads\mfc6Kr0QXh5SWdHhJyWDGw.mp4` (`960x720`, 30 fps,
  180 frames, SHA-256 `0EDCF8EADA4C436DB5E45225589757E68FC302EFC897906C1E00211ED58FA8F7`)를
  전 프레임으로 펼쳐 확인했다. 이 MP4는 이동 경로/방향 reference이며 기형 보행 pose donor가 아니다.
- 새 Father는 연속 ground position의 실제 delta를 바라보고 코너에서 다음 이동 벡터 쪽으로 연속
  회전한다. 기존 body의 검증값은 `+90°`였지만 새 V19 rig는 0/90/180/270 비교로 다시 확정한다.
- raw GLB에는 left/right leg deform chain이 정확히 하나씩만 있어야 한다. extra limb, cross-leg
  weight, arm/hand-to-leg weight, pelvis 아래 제3 appendage와 전 프레임 third-leg silhouette은 모두
  `0`이어야 한다.
- 상세 fail-closed 정본:
  [FATHER_V19_DIRECTION_AND_TWO_LEG_CONTRACT_2026-08-27.md](FATHER_V19_DIRECTION_AND_TWO_LEG_CONTRACT_2026-08-27.md).
  `productionEligible=false`; production/default/Downloads/배포본은 변경하지 않았다.

## 2026-08-27 / strict V19 Higgsfield 유료 제출 1회도 gateway 차단

- 사용자 최종 승인 뒤 `Korean Father 3D` Supercomputer 대화에 strict V19 요청을 정확히 한 번
  전송했다. 네 방향 URL 순서, one-package mesh/rig/skin/walk, exact-two-leg, action 30, quad 60k,
  A-pose, PBR 조건을 모두 포함했다.
- 승인 카드에서 Texture/Rigging/Animation이 모두 켜진 상태와 `Approve 38`을 확인하고 한 번만
  승인했다. 작업은 약 51초 후 HTTP 403 `only_mcp_usage_on_trial_is_available`, `job_ids: []`로
  끝났다. job ID와 GLB는 없고 자동 재시도도 하지 않았다.
- 사후 balance는 `64`, 유료 plan `plus`로 제출 전과 같다. `free_trial=cancelled_by_user`는 별도 체험
  이력이지 현재 subscription이 아니다. 실제 3D 차감은 `0`이다.
- 무료체험 계정이나 입력/파라미터 실패가 아니다. paid Plus 웹 경로가 38-credit payload를 승인한 뒤
  MCP-only trial gate에 걸리고, 현재 MCP에는 `generate_3d`가 없는 server routing/tool-exposure
  불일치다. paid web routing이 수정되거나 실제 `generate_3d`가 생기기 전 같은 payload를 반복하지 않는다.
- 지원용 정본:
  [HIGGSFIELD_PAID_PLUS_3D_GATE_MISMATCH_2026-08-27.md](HIGGSFIELD_PAID_PLUS_3D_GATE_MISMATCH_2026-08-27.md).
- 브라우저 작업은 background로 수행했고 실패 화면은 같은 대화에 보존했다. `productionEligible=false`;
  production/default/Downloads/배포본은 변경하지 않았다.

## 2026-08-27 / Father action-613 저장 손상 여부 재감사 — 원본 걷기 불합격 확정

- Higgsfield history에서 과거 완료 3D 작업 4건(static, idle, run 644, walk 613)을 다시 확인했다.
  GPT/Higgsfield가 3D를 만들 수 없다는 이전 설명은 잘못이었다. 현재 세션의 `generate_3d` 미노출은
  과거 생성 가능 여부와 별개다.
- 네 cloud GLB를 다시 받아 기존 prepare receipt와 SHA-256을 비교했고 4건 모두 정확히 일치했다.
  다운로드/저장 손상은 없다.
- walk 613 raw GLB와 Unity용 FBX는 vertices/polygons/bone hierarchy가 같고, changed-weight vertices
  `0`, max weight delta `0.0`, normalized bone pose max delta `4.70e-7`, deformed mesh max delta
  `7.05e-7`다. 변환도 원본 자세를 바꾸지 않았다.
- raw GLB와 FBX를 같은 전 위상으로 나란히 확대 검수한 결과, 긴 저위치 팔·불안한 손/팔 회복·좁고
  겹치는 다리 실루엣이 원본부터 동일했다. action 613은 `SOURCE_WALK_REJECTED`다.
- 동시에 idle과 walk는 `sameGeometry=false`, `sameSkinWeights=false`, `sameBindSkeleton=false`다.
  과거 static/idle body에 다른 작업의 Humanoid clip을 섞은 V61~V72 경로가 옷·팔·손·상체를 더
  악화시킨 것도 확인했다. V73 one-package는 혼합만 제거했으며 나쁜 raw walk를 그대로 물려받았다.
- 앞으로는 raw GLB full cycle + 균등 위상 원본 24프레임을 Unity보다 먼저 육안 통과시킨다. 통과 후에만
  같은 package를 30 fps 결정 변환하고 Generic/direct skeleton으로 재생한다. Humanoid/muscle retarget,
  posture/limb override는 금지한다.
- 상세 감사:
  [FATHER_V18_RAW_GLB_VS_FBX_WALK_AUDIT_2026-08-27.md](FATHER_V18_RAW_GLB_VS_FBX_WALK_AUDIT_2026-08-27.md).
  신규 3D 생성/차감 `0`, `productionEligible=false`; production/default/Downloads/배포본은 변경하지 않았다.

## 2026-08-27 / 현재 최고 우선순위: Father V19 Higgsfield whole-package rebuild

- 사용자는 V74 이하의 외형/팔/손/걷기 수정 경로를 중단하고, 같은 Father를 여러 방향에서 유지하는 새
  캐릭터와 자연스러운 2족 `Casual_Walk`을 Higgsfield 한 패키지로 다시 만들라고 지시했다. 걷기가
  최우선이며 기존 절차 보행이나 임의 팔 보정으로 대체하지 않는다.
- V19 four-view V2 입력을 front → left → back → right로 확정했다. V1은 outline과 중복 profile 때문에
  내부 폐기했다. V2는 3D 입력 후보이며 사용자 완성 외형 승인을 뜻하지 않는다. V1/V2 reference image
  생성에 각각 2 credits, 합계 4 credits를 사용해 잔액이 68에서 64가 됐다.
- Meshy `multi_image_to_3d`에 texture + rigging + action 30 `Casual_Walk` + quad remesh 60k를
  한 GLB로 요청한다. Higgsfield 승인 UI의 실제 견적은 `38 credits`, 확인 잔액은 `64 credits`다.
- paid Plus 웹 제출은 job 생성 전 HTTP 403 `only_mcp_usage_on_trial_is_available`로 거절됐다. 현재
  Codex와 ChatGPT 연결 표면에는 필요한 `generate_3d` 호출기가 노출되지 않았다. 이는 무료체험 상태가
  아니라 paid-web/MCP routing mismatch이며 과거 완료된 3D 작업 4건과도 모순되지 않는다. 오늘 V19
  3D job `0`, 3D 차감 `0`, GLB `0`이며 웹/MCP 자동 재시도는 하지 않는다.
- 차단 해제 뒤 정확히 한 작업만 제출하고, repository 밖에서 mesh/skin/embedded walk를 먼저 확대
  검수한다. 그 뒤에만 별도 V19 experimental Unity 후보와 실제 맵 GIF를 만든다.
- 상세 정본:
  [FATHER_V19_HIGGSFIELD_MCP_BLOCKED_HANDOFF_2026-08-27.md](FATHER_V19_HIGGSFIELD_MCP_BLOCKED_HANDOFF_2026-08-27.md).
  `productionEligible=false`; production/default/Downloads/배포본은 변경하지 않았다.

## 2026-08-27 / 현재 Father 검토 후보: V72 하체 복원 + straight rigid arms V74

- 사용자가 V73 실제 영상에서 `V72보다 다리 움직임까지 더 이상해졌다`고 기각했다. 팔만 수정해야
  했는데 모델/Avatar/skin/bind skeleton 전체를 native action package로 바꾼 것이 원인이다. V73은
  `USER_VISUAL_REJECTED_CHANGED_LEG_GAIT`이며 현행 후보로 재사용하지 않는다.
- V74는 V72의 `FatherV18CleanBipedRigV4`, idle/walk pair, 하체·골반·몸통·머리 sanitation, facing
  `-16.9219°`, stride `0.675`, cycle `0.99380799s`, corner `360°/s`를 복원했다. 절차 보행이나 V73
  native package를 사용하지 않는다.
- 변경은 최종 팔 처리 한 곳뿐이다. 정적 기준 shoulder-to-finger hierarchy를 곧고 rigid하게 복원한
  뒤 upper-arm root만 fixed body-side axis 둘레로 좌우 반대 최대 `6°` swing한다. elbow/wrist/finger,
  outward, behind-body tuck 보정은 모두 `0`이다.
- V74 R2 hidden actual-map runtime은 telemetry `1,344`, 30 fps PNG `673`, `22.4000s`, 두 회로를
  완료했다. V72와 Office 위치/root yaw/gait phase/motion phase가 전 sample에서 정확히 같고, foot/hips
  95 percentile 차이는 `3.27e-5` 이하다. 전 673프레임을 23개 연속 확대 시트와 전체 경로 GIF로
  검수했으며 옷 파열·third leg·분리 손·뒤로 젖힌 상체·방향 점프는 보이지 않았다.
- 첫 R1의 V66식 elbow bend/outward 보정은 손이 앞으로 나온 로봇 자세를 만들어 내부 폐기했고 R2에는
  포함하지 않았다. 자동/내부 판정은 사용자 합격을 대신하지 않는다.
- 상세:
  [FATHER_V18_CLEAN_BIPED_STRAIGHT_ARM_WALK_V74_QA_2026-08-27.md](FATHER_V18_CLEAN_BIPED_STRAIGHT_ARM_WALK_V74_QA_2026-08-27.md).
  상태는 `USER_VISUAL_REVIEW_REQUIRED`, `productionMutation=false`, `productionEligible=false`다.
  추가 Higgsfield 사용 `0 credits`; production/default/Downloads/배포본은 변경하지 않았다.

## [사용자 하체 기각·V74로 대체] 2026-08-26 / native action-613 package V73

- 사용자가 V72의 실제 화면에서 팔이 뒤에 고정되고 손·팔이 흐물거린다고 기각했다. 원인은 action 613
  자체가 아니라 정적-derived V4 mesh/skin에 다른 FBX의 Humanoid clip을 리타깃한 뒤, 원본 팔 동작을
  T-pose-derived rigid hierarchy로 덮어쓴 혼합 경로였다. V72는
  `USER_VISUAL_REJECTED_ARM_OVERRIDE`이며 재사용하지 않는다.
- V73은 `Downloads/rpg.mp4`의 직접 적용 방식대로 action-613 walk FBX 하나를 visible mesh, Avatar,
  bind skeleton, skin weights, `Casual_Walk_inplace` clip의 공동 권위로 사용한다. muscle-delta retarget,
  anatomical sanitation, rigid-arm restore, procedural gait는 모두 끈다. 정적 승인 외형의 surface material과
  4096 albedo만 그대로 적용한다.
- map contract는 native facing `90°`, stride `0.8526`, locked cycle `0.99380799s`, corner `360°/s`다.
  hidden 720p 실제 맵 두 바퀴에서 telemetry `1,344`, 30 fps PNG `673`, `22.4s`를 기록했다.
- 화면 픽셀 직접 추적으로 네 직선 구간 × 30 frames와 네 회전 구간 × 30 frames를 확대 검수했다. 팔은
  좌우 반대 위상으로 연속 스윙하고, third leg·옷 파열·분리 신발·방향 점프는 보이지 않았다. 두 바퀴
  경계 frame `336 -> 337`도 연속이다. 자동 판정은 사용자 승인을 대신하지 않는다.
- 상세:
  [FATHER_V18_NATIVE_613_WALK_V73_QA_2026-08-26.md](FATHER_V18_NATIVE_613_WALK_V73_QA_2026-08-26.md).
  상태는 `USER_VISUAL_REVIEW_REQUIRED`, `productionMutation=false`, `productionEligible=false`다.
  추가 Higgsfield 사용 `0 credits`, 잔액 `68`; production/default/Downloads/배포본은 변경하지 않았다.

## [사용자 팔·손 기각·V73으로 대체] 2026-08-26 / 정적 외형 + clean-biped V4 + Claude action 613 V72

- 사용자가 V66을 `Claude 걷기를 그대로 쓰지 않고 임의로 바꾼 보행`으로 기각했다. V66은
  `USER_VISUAL_REJECTED_WRONG_MOTION_SOURCE`이며 재사용하지 않는다.
- V67은 action 613 보행을 복원했지만 손·팔 이상이 남았고, V69도 사용자가 실제 GIF에서 `너무
  흐물거린다`고 기각했다. Humanoid arm/forearm muscle 진폭만 줄이는 방식은 재사용하지 않는다.
- V72는 action 613의 hip/knee/pelvis/torso/head와 map 이동을 유지하되, 어깨부터 손가락까지 정적 기준
  hierarchy를 매 프레임 rigid하게 복원한다. 상완 root만 몸 쪽 `4°`, 실제 측정 정면축 기준 반대 앞뒤
  swing 최대 `2°`를 적용한다. 팔꿈치·손목·손가락은 각자 변형되지 않는다.
- 외형/skin은 V66에서 만든 `FatherV18CleanBipedRigV4`와 정적 FBX surface material을 그대로 쓴다.
  셔츠·칼라 안정 component 38개, cross-side `0`, arm+leg mixed `0` 계약을 유지한다.
- map contract는 action-613 pair의 facing `-16.9219°`, stride `0.675`, locked cycle
  `0.99380799s`, corner `360°/s`다.
- hidden 720p 실제 맵에서 telemetry `1,344`, 30 fps PNG `673`, 두 회로를 완료했다. 확대 one-cycle
  30프레임에서 손이 소매에 붙고 손목 뒤집힘·어깨 으쓱임이 사라졌으며 작은 반대 팔 스윙은 남았다.
  손의 골반 상대 높이는 V72 L `-0.07..-0.00`, R `-0.10..-0.07`이다. 전체 맵에서
  torn shirt, third leg, separated shoe, backward torso, wrong facing은 보이지 않았다.
  자동 판정은 사용자 승인을 대신하지 않는다.
- 상세:
  [FATHER_V18_CLEAN_BIPED_CLAUDE_WALK_V72_QA_2026-08-26.md](FATHER_V18_CLEAN_BIPED_CLAUDE_WALK_V72_QA_2026-08-26.md).
  상태는 `USER_VISUAL_REVIEW_REQUIRED`, `productionMutation=false`, `productionEligible=false`다.
  Higgsfield 사용 0 credits, 잔액 68; production/default/Downloads/배포본은 변경하지 않았다.

## [사용자 기각·V67로 대체] 2026-08-26 / 정적 외형 + clean-biped V4 + 전용 SD 보행 V66

- 사용자가 기준으로 다시 지정한 유료 정적 Father V18의 topology/UV/material/texture와 체형을 그대로
  유지한다. 움직이는 동안에도 머리, 얼굴, 셔츠, 팔·손, 바지, 두 발이 정적 기준처럼 또렷해야 한다.
- V61은 사용자에게 `옷이 뜯김`, `상체가 뒤로 젖은 좀비`, `캐릭터 자체가 이상함`으로 기각됐다. 실제
  진단에서 셔츠 중심 3,116 vertices 중 2,011개가 arm weight를 갖고 있었고 signed torso lean 평균은
  `-1.4516°`였다. V61/V62의 Unlit material도 정적 FBX보다 어둡고 평평했다.
- V63은 정적 material과 안정 torso panel을 적용했지만 다른 체형용 action 613 리타겟 실루엣이 남았다.
  V64는 T-pose arm rest를 잘못 복원해 팔이 수평으로 벌어진 진단본이다. 둘 다 탈락이다.
- 기각된 V66도 `FatherV18CleanBipedRigV4`를 썼다. V4는 V67을 거쳐 V69에서도 계속 유지한다. 28,895 vertices/49,192 polygons,
  24 bones/22 deform bones, 최대 4 influences, cross-side `0`, arm+leg mixed `0`이며 셔츠·칼라·허리
  안정 panel 38개에서 limb membership 11,235개를 제거했다. FBX SHA-256은
  `107DE6C4D2F36C1048746275B4E4E108447094705684D75AECF62CA1220F50B0`다.
- action 613의 moving mesh/skeleton/skin/AnimationClip은 V66에서 사용하지 않는다. 리그 자체의 T-pose
  계약에서 팔을 몸 옆으로 먼저 내리고, 0.88초 SD 2족 cycle을 거리로 구동한다. 팔은 outward `1°`,
  서로 반대 swing `8°`, elbow bend `12°`; torso 추가 lean은 `0°`다. 골반 좌우 흔들림과 world-space
  foot pull은 없고 작은 upward-only rise만 있다.
- 방향은 실제 QA ground 이동 벡터 + clean-rig authored-forward `90°`, 코너 회전은 기존 360°/s 선형
  blend를 유지한다. exact yaw `0/45/90/135/180/225/270/315°` 2K 시트를 별도로 만들었다.
- V66 실제 맵 증거는 30 fps `673` PNG/telemetry `1,344`, 2K `169` PNG, 두 회로 완료다. 34-frame
  확대 루프 전체와 8 yaw에서 옷 분리, third leg, 다리 교차, 분리 신발, 뒤젖힌 몸통, 수평 팔, 거인
  축척은 보이지 않았다. 전체 맵 GIF에서 진행 방향·코너 회전과 loop seam도 확인했다.
- 보조 수치: lateral foot sign 교차 `0/1,344`, 최소 발 간격 `0.2465`, 양팔 상관 `-0.99993`, 손과
  반대 발 상관 `0.8215`, signed torso lean 사실상 `0°`. 자동 수치 PASS는 사용자 승인이 아니다.
- 상세: [FATHER_V18_CLEAN_BIPED_NATURAL_WALK_V66_QA_2026-08-26.md](FATHER_V18_CLEAN_BIPED_NATURAL_WALK_V66_QA_2026-08-26.md).
  V66 상태는 `USER_VISUAL_REJECTED_WRONG_MOTION_SOURCE`, `productionMutation=false`,
  **`productionEligible=false`**다.
  추가 Higgsfield 사용량 `0 credits`, 잔액 `68`; production/default/Downloads/배포본은 변경하지 않았다.

## [V67로 재개·V61 사용자 기각] 2026-08-26 / Codex 외형 + Claude 클립

- 당시 목표는 "Codex의 clean biped 외형 + Claude의 `Casual_Walk_inplace`(613) 클립"이었다. 그러나
  실제 V61 영상에서 옷 파열과 뒤로 젖힌 상체가 확인되어 그 리그/보정은 기각됐다. 현행 V69는 V4의
  안정 torso skin과 action-613 보행 source로 같은 목표를 다시 수행한다.
- **리타겟 2종 모두 실패했다.** Unity Humanoid 직접 리타겟은 상체 기울기 `8.42도`, 근육값 차분
  리타겟은 `10.68도`로 더 나빠졌다. 후자가 실패한 것이 진단이다: 기준 포즈(T-pose) 문제라면 차분이
  고쳤어야 한다. 원인은 기준이 아니라 **스키닝 가중치**다. `classify_vertex()`가 정점을 높이 구간과
  좌우 거리로 뼈에 배정하므로 큰 관절 회전을 못 받는다. 절차적 보행은 각도가 작아 멀쩡할 뿐이다.
- **힉스필드 재생성은 낭비다.** clean biped 메시와 Claude 메시는 정점 `28,895` 대 `28,924`, 알베도
  픽셀 평균 차이 `7.76/255`(3%)로 사실상 같다. 즉 사용자가 본 "좋아진 외형"은 모델이나 텍스처가 아니라
  스키닝 품질이다. 같은 메시를 다시 리깅하면 지금 에셋과 같은 결과가 나온다. 잔액 `68` 유지.
- 요청 내용과 숫자 합격선은
  [CODEX_WORK_ORDER_CLEAN_BIPED_SKINNING_2026-08-26.md](CODEX_WORK_ORDER_CLEAN_BIPED_SKINNING_2026-08-26.md)에
  있다. 핵심은 뼈 거리 기반 자동 가중치로 교체, bind pose를 T-pose로 확정, 검증은 절차적 보행이 아니라
  클립을 얹은 상태로.
- 현재 빌더는 승인된 조합(힉스필드 바디 + 자기 클립, 오프셋 90 / 보폭 0.8526)에 코너 회전 수정이
  올라간 상태다. 리그가 합격선을 넘으면 Claude가 오프셋과 보폭을 재산출해 후보별 상수에 반영한다.
- 새 계측: `PoseSnapshot.torsoUpLocal`(골반→머리, 호스트 로컬). 상체 기울기를 키 감소로 추정하던 것을
  대체한다. 이전에 10% 키 감소로 `37도`라 추정했으나 실측은 `8.42도`였다.

## [V66으로 대체됨] 2026-08-26 / clean biped 리그 V1 인수 — 방향 검증, 알베도 회귀 1건 차단

Codex가 만든 `FatherV18CleanBipedRigV1`이 현재 후보다. 이후 작업은 Claude가 단독으로 잇는다.

- **방향 검증됨.** `fatherMotionFacingOffsetDegrees = 90`이 이 리그에도 맞다. 몸 정면과 이동 방향의
  각도차 **중앙값 0.19도**(평균 3.92는 코너 회전 지연 포함). 판정 근거와 방법은
  [FATHER_V18_FACING_OFFSET_METHOD.md](FATHER_V18_FACING_OFFSET_METHOD.md)에 정본으로 남겼다.
- **리그 품질이 힉스필드보다 낫다.** 발 스윙이 발가락 방향과 **62:1**로 정렬돼 있다(힉스필드는
  1.6~3.5:1). 보폭 0.36 신장으로 정상 범위다. 팔이 옆선에 있고 다리 변형도 없다.
- **한때 파탄으로 보였던 수치는 전부 계측 착시였다.** `leftFootLocal` 등은 호스트 로컬이라 이 경로에서
  캐릭터의 경로 순회가 섞여 들어온다(골반이 호스트 기준 `2.485` 이동, 힉스필드는 `0.025`). 골반을 빼지
  않으면 스윙 정렬이 `0.14`, 보폭 정합이 `0.1615배`로 읽힌다. **팔다리 진폭은 항상
  `footLocal - hipsLocal`로 잰다.**
- **알베도 회귀 1건을 잡았다.** 새 리그의 4096 알베도가 `maxTextureSize 2048` + 손실압축으로
  임포트되고 있었다. 8월 26일에 고쳤던 것과 같은 결함이고, 게이트가 고정 목록이라 새 파일이 그대로
  빠져나갔다. **게이트를 자동 탐색으로 바꿨다** — `Candidates` 아래 `*-albedo.png`는 생기는 즉시
  검사 대상이 된다. 현재 4종 전부 `4096x4096 BC7` PASS.
- **계측 도구 추가.** `PoseSnapshot.toeForwardLocal`(발목→발가락, 호스트 로컬)을 영수증에 기록한다.
  방향 오프셋을 `K = -atan2(F.x, F.z)`로 즉시 풀 수 있고, 다음 리그 교체 때 재검증이 몇 분이면 끝난다.
- 참조되지 않는 중간 씬 V32~V38(2.8MB)을 삭제했다. 빌더가 쓰는 것은 V39 하나다.
- `FAST_QA_WINDOWS.cmd -Profile editor-broad` PASS. `productionEligible=false`와 Higgsfield 잔액 68
  유지. production/default/Downloads/배포본은 건드리지 않았다.
- 남은 것: 코너 회전 지연(초당 360도, 코너에서 약 0.3초 옆걸음)과 사용자 시각 승인.

## [사용자 기각·V66으로 대체됨] 2026-08-26 / V18 정적 외형 + 절차형 clean biped V39

- 사용자가 실제 영상을 보고 기존 V18 imported walk를 `다리가 3개`, `흐물흐물`, `팔이 가만히 있음`,
  `캐릭터 자체가 이상함`으로 판정했다. 아래의 "Father V18 보행 정합 완료"는 자동/계측 단계의 과거 기록이며
  **시각 합격을 뜻하지 않는다**. moving FBX의 skeleton/weights/clip은 새 후보에 재사용하지 않는다.
- 새 후보는 유료 정적 `FatherV18HiggsfieldStatic`의 외형을 그대로 쓴다. 28,895 vertices, 49,192 polygons,
  topology/UV/material slot을 보존하고 clean 24-bone biped armature와 결정적 skin weights만 추가했다. rest 상태
  최대 정점 오차는 `6.143906e-8`, 좌우 교차 weight `0`, arm+leg 혼합 weight `0`, 정점당 최대 influence `2`다.
  정본 FBX는 `Candidates/FatherV18CleanBipedRigV1/father-v18-clean-biped-rig-v1.fbx`, SHA-256
  `83C6892C1C0F8BDC6081F3D8086BFCD5D4E4F3008F843F4ED07730FD94AB4F2F`다.
- clean-biped V36도 사용자가 실제 프레임에서 `팔은 이게 뭐니`, `허리가 곱추야?`라고 지적해
  `USER_VISUAL_REJECTED_HIDDEN_ARMS_HUNCHED_SILHOUETTE`로 봉인한다. V37은 팔/상체 보정축에 host `+Z`를
  잘못 써 일부 방향에서 팔을 옆으로 튀긴 진단본이고, V38은 올바른 모델축으로 style A/B를 비교한 조정본이다.
- Unity V39는 shared human clip이나 생성 motion clip을 쓰지 않는다. 0.88초 SD 2족 cycle에서 두 다리가
  반대 위상으로 지지/회복하고, 회복 무릎을 분명히 굽히며, 팔은 몸 옆에서 작게 반대 스윙한다. 좌우 골반
  흔들림과 발을 월드에 잡아당기던 two-bone IK는 제거했고 작은 수직 rise만 남겼다.
- 모델의 실측 정면 `local -X`에서 body forward=`-transform.right`, body side=`transform.forward`를
  일관되게 사용한다. 정적 옆선에서 상체 중심을 뒤로 `5°` 세우고 얼굴은 수평으로 유지한다. 팔은 rest
  pose에서 바깥 `2°`, 앞뒤 `6°` 반대 swing, 팔꿈치 `22°`만 적용해 손이 몸 뒤로 사라지거나 긴 막대처럼
  보이지 않게 했다. 이 값은 V38 두 후보의 169프레임 전체 비교 뒤 선택했고 V39 기본값으로 고정했다.
- 실제 Starter Office Father agent가 같은 3x3 외곽을 두 바퀴 이동했다. 60 fps telemetry `1,344` samples와
  7.5 fps 실제 렌더 `169` frames 전부를 여섯 장의 확대 시트와 전체 영상으로 육안 검수했다. 전 프레임에서
  보이는 다리는 정확히 둘이며 extra limb/cone, 분리 신발, mesh melting, 다리 교차, 거인 축척은 없었다.
  양발의 전방 도달/굽힌 무릎 회복과 반대팔 스윙이 보이고 방향·코너도 일관된다.
- raw foot-bone local/world 좌표는 host 회전 구간에서 화면의 변형 신발 위치와 일치하지 않는 큰 변화를
  포함하므로 발 미끄럼 자동 합격 수치로 쓰지 않는다. `compositeVisualContentPass`도 판정 근거가 아니라 캡처
  유효성 확인일 뿐이다. 최종 판정은 실제 GIF 전체를 보는 사용자 시각 승인이다.
- 한 route circuit `7.950477 u`를 정확히 9 motion cycles로 맞춘 stride는 `0.8833864 u`다. 동일 route
  지점의 두 번째 회로 seam은 position `0`, yaw `0°`, wrapped motion phase delta `0.0000868`이다.
- 빌드/런타임: `FatherV18CleanBipedNaturalWalkMapBuildV39` /
  `outputs/father-v18-clean-biped-map-runtime-v39-final`. 상태는
  `FATHER_V18_CLEAN_BIPED_NATURAL_MAP_PROOF_COMPLETE`, `productionMutation=false`,
  **`productionEligible=false`**다. user approval 전에는 승격하지 않는다.
- Unity/Player는 `-batchmode -nographics`와 hidden process로만 실행했고 `MainWindowHandle`을 감시했다.
  종료 후 Unity/Player/Blender process는 없다. production/default/Downloads/배포 실행본은 변경하지 않았다.
- 이번 rebuild의 Higgsfield 사용량은 `0 credits`; 잔액은 `68` 그대로다. 상세 근거와 재현 경로는
  [FATHER_V18_CLEAN_BIPED_NATURAL_WALK_QA_2026-08-26.md](FATHER_V18_CLEAN_BIPED_NATURAL_WALK_QA_2026-08-26.md)에 있다.

## 2026-08-26 / Father V18 보행 정합 완료 — 걷기 액션 교체 + 렌더/위상 버그 3건 수정

증거: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV18HiggsfieldCasualWalkRuntimeV26R1`
(`Artifacts/`는 `.gitignore` 대상, 로컬 증거). 루트 2바퀴 완주, 표본 1344개, 캡처 60.0 fps 고정.

- **최종 정합 0.9995배.** 클립 사이클당 이동 `0.7744 u` 대 보폭 실측 `0.7747 u`, 사이클당 슬립
  `-0.0004 u`(신장의 0.02%)로 측정 노이즈 수준이다. 수정 전 기준값은 poseStrength 0.45에서
  1.66~1.92배, 1.0에서 0.841배였다.
- **소스 액션을 교체했다.** Higgsfield action 613 `Casual_Walk_inplace`, 8 credits, 잔액 76 → 68.
  `model_url`은 idle-0/run-644와 같은 Tripo 베이스라 몸이 동일하다(정점 28,924, 본 24, topology/UV/
  텍스처 일치). 644 `Lean_Forward_Sprint`는 소스 GLB 실측에서 엉덩이를 15% 낮추고 상하 진폭이 3.7배인
  전력질주였고, 보폭이 1.79 u/s를 함의해 0.666 u/s 사무실 보행과 2.7배 어긋났다. 히스토리로만 보존한다.
- **`Unlit/Texture`가 빌드에서 스트립되고 있었다.** 런타임 `Shader.Find`로만 만들던 머티리얼이라 참조가
  0건이었고, 플레이어에서 `null`을 받아 `?? Shader.Find("Sprites/Default")`로 조용히 폴백했다. 스프라이트
  셰이더는 정점 컬러를 곱하고 `ZWrite Off`라 3D 스킨드 메시를 어둡게 뭉갠다. **V18~V22 플레이어 실행이
  전부 이 상태였고**, V19의 `USER_VISUAL_REJECTED_STRETCHED_LEGS_WASHED_COLOR`를 포함한 반복된 색상
  불만이 최소한 부분적으로 이것이다. 씬이 참조하는 머티리얼 애셋으로 고정하고 폴백을 제거해 예외로 바꿨다.
- **`phaseOffset`이 수식에서 상쇄되고 있었다.** 호출부가 `LockedCycleSeconds`를 곱하고 actor가 다시
  나누므로 `FindLeftForwardContactPhase`의 접지 정렬이 한 번도 적용되지 않았다. 발 고정이 보폭 중 임의
  지점에서 돌아 좌우가 96/48로 어긋났고, 정렬 후 73/63으로 대칭이 됐다.
- **보행 위상은 시간이 아니라 거리로 진행한다.** `OfficeLocomotionGaitRules`가 `DefaultStrideLength`
  이동마다 한 사이클을 완성하고 `DirectionalSpriteAnimator.ConfigureLocomotion`이 다른 stride를 예외로
  거부하므로 2D 케이던스는 전역 고정이다. 따라서 보폭이 다른 클립은 재타이밍으로 맞출 수 없고, 에이전트의
  누적 `GaitDistance`를 그 클립의 보폭으로 나눠 구동해야 한다. `gaitPhase01`과 달리 단조 증가라 비정수
  보폭비에서도 랩 불연속이 없다. 근본원인 문서 4단계의 "cycle time을 산출하라"는 이 전제 위에 있었으므로
  폐기한다.
- 계측 상수: office→QA 배율 `0.9082`, 오피스 사이클당 이동 `0.9026 u`,
  `fatherMotionStrideOfficeUnits = 0.8526`. `target3DHeight`는 걷기 `1.4820` / 스프린트 `1.6080`으로
  클립의 포즈 바운즈를 읽는 투영 높이 보정 때문에 클립마다 다르며 클립 안에서는 안정적이다.
- 유료 알베도 3종 전부 `4096x4096 BC7` 실측 PASS이며 `Family3DHiggsfieldAlbedoImportValidation`이
  Fast QA broad에서 PNG IHDR과 대조해 강제한다.
- **턴 블렌딩 추가(2026-08-26, 크레딧 0).** 오피스는 방향을 8옥탄트로 해석해 코너마다 90도를 한 프레임에
  전달했다. `ResolveBlendedYaw`가 `Quaternion.RotateTowards`로 초당 360도 상한을 걸어 돌린다. 실측:
  프레임당 최대 회전 `6.001도`(상한 360/60=6.000과 일치), route 코너 7개가 **전부 정확히 90.0도를
  16프레임(0.267초)에** 완료, 목표 대비 최대 오차 `129도`는 스폰 직후 첫 방향 잡기 1회뿐이고 코너가
  아니다. 스냅이었다면 오차가 항상 0이다. 회전율은
  `-family3d-father-v18-motion-yaw-degrees-per-second`로 조정 가능하다(45~3600 검증).
  같은 실행에서 보폭 정합 `0.9995배`와 접지 `73/63`이 그대로 유지됐다.
- 영수증 샘플에 `rootWorldYawDegrees`(실제 적용 각도)와 `targetYawDegrees`(옥탄트 목표)를 함께 기록한다.
  이것이 없으면 영수증에 이산 방향만 남아 블렌딩을 증거로 계측할 수 없다.
- **2026-08-26 사용자 영상 판정으로 결함 2건이 드러났고, 둘 다 이 세션이 만든 것이다.**
  1. 발 고정 IK를 imported-clip 경로에 켠 것이 다리를 망가뜨렸다(`오징어처럼 흐믈거리고 다리가 3개`).
     `SolveTwoBonePlant`는 발을 월드에 못박고 다리뼈를 강제 회전시키는 V14 SD 절차적 보행 전용 솔버라,
     facing과 travel이 어긋나면 박힌 발이 옆으로 끌리며 다리를 늘인다. SD 경로로 되돌렸다. 이 경로는
     이미 `SetApplyFootIK`로 지면 구속을 갖는다.
  2. `MapOfficeDirectionToUnityYaw`가 8방향을 45도 균등으로 매핑하는데 office→QA 지면 변환이 X와 Z를
     같은 배율로 늘리지 않는다. 대각선 다리에서 실제 이동각은 54.7도인데 45도를 바라봤다. 측정된 계통
     오차 `±9.7도`가 그 `sqrt(2)` 왜곡이다. 테이블을 버리고 **연속한 QA 지면 좌표 차분에서 facing을
     유도**하도록 바꿨다. 실측 중앙값 `9.7도 -> 0.02도`, 1도 미만 프레임 `90.2%`.
- **미끄러짐은 위 2번의 결과였다.** 발은 캐릭터 정면으로 쓸어내는데 몸이 그만큼 옆으로 가면 차이가 곧
  슬립이다. 앞선 `0.9995배`는 보폭을 캐릭터 로컬 축, 이동을 월드 축으로 재면서 **둘의 각도 차이를 한 번도
  확인하지 않은** 계산이라 이 슬립을 잡지 못했다. 정합 계측에는 facing-travel 각도 오차를 함께 본다.
- 남은 오차는 전부 턴 중 지연이다(평균 4.63도, 최대 119도). 회전율 360 deg/s를 유지하며 사용자 판정을
  기다린다. `-family3d-father-v18-motion-yaw-degrees-per-second`로 조정한다.
- **2026-08-26 3차 판정: 몸이 진행 방향을 향하지 않았다. 정답은 +90도 오프셋이다.**
  임포트된 바디의 forward는 `+Z`가 아니라 `-X`이므로 `LookRotation(delta)`만으로는 몸이 90도 어긋난다.
  `fatherMotionFacingOffsetDegrees`(기본 90, `-family3d-father-v18-motion-facing-offset-degrees`로
  조정)를 `LookRotation` 뒤에 곱해 해결했다.
  확정 절차: 0/90/180/270 네 후보로 각각 실행해 같은 프레임에 이동 방향 화살표를 그려 나란히 비교했고,
  **서로 다른 두 직선 구간 모두에서 일관되게 맞는 값은 90뿐이었다.** 강제 yaw 스윕에서 뒤통수가 보이는
  최소 지점이 yaw 90인 것으로부터 대수적으로 유도한 값(`forward = -X`)과도 일치한다.
- **이 하나가 사용자가 지적한 세 증상을 모두 설명한다.** 발은 몸 기준 앞으로 쓸어내는데 몸이 뒤로
  가므로 지면을 반대로 긁어 미끄러지고, 뒤로 걷는 보행은 관절이 어색해 `오징어`로 보이며, 방향은 당연히
  맞지 않는다.
- **계측 방법론 교훈 3건.** (1) 발 스윙 축이 로컬 Z라는 것과 GLB 발가락 방향으로 "정면축 정상"이라
  두 번 판단했으나 스윙 축은 앞뒤 대칭이라 부호를 담지 못하고, 발 평균 위치의 좌우 성분 0.03~0.06은
  노이즈다. (2) 프레임에 그린 화살표를 눈으로 "오른쪽 아래"라 읽고 180도 뒤집었으나, 화면 픽셀을 직접
  추적하니 실제 이동은 `+27도`(오른쪽 위)였다. 그림을 눈으로 읽는 것도 계측이 아니다. (3) 결국 통한
  방법은 후보를 모두 실행해 같은 프레임을 나란히 놓고 고르는 것이었다. **방향 관련 주장은 후보 비교
  시트로 검증한 뒤에만 기록한다.**
- **남은 것은 사용자 시각 승인이다.** `productionEligible=false`를 유지한다. 아직 없는 것: idle 외 상태, 머리 실루엣이 승인 디자인(짧게 옆으로 넘긴 머리)과 다른 문제.

## 2026-08-26 / V23 실측: 수신부 3건은 고쳐졌고, 남은 원인은 소스 액션이다

증거는 `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV18HiggsfieldNativeRunMapRuntimeV23R1`
(`Artifacts/`는 `.gitignore` 대상이라 로컬 증거다). 루트 2바퀴 완주
`FATHER_V18_HIGGSFIELD_IDLE_RUN_MAP_PROOF_COMPLETE`, `lastScaleMatchRatio=1.0000002`,
`poseStrength=1.0`, `target3DHeight=1.6078752`, `appliedModelScale=0.8585948`.

- **캡처 수정 검증됨.** `simulationSeconds` 간격이 평균·최소·최대 모두 `0.0167s`, 정확히 60.0 fps다.
  표본 1344개(이전 180), 완전한 보행 사이클 16회 관측. 이전의 평균 0.0973s·최악 0.5327s·실효 10.3 fps
  앨리어싱은 사라졌다. 이제 움직임 판정에 쓸 수 있는 증거다.
- **접지 구속 동작 확인.** `leftFootPlanted` 96/1344, `rightFootPlanted` 48/1344. 이전에는 두 값 모두
  180/180 `false`였다. 좌우 비대칭은 기존에 기록된 36% 좌우 sweep 비대칭과 방향이 같다.
- **poseStrength 1.0은 반대쪽으로 넘어갔다.** 보폭은 사이클당 `0.439 u` → `1.073 u`로 늘어 근본원인
  문서의 선형 외삽(0.97~1.13 u)과 일치했다. 그러나 이번 실측 이동은 사이클당 `0.648 u`(root 속도
  0.666 u/s)여서 비율이 **1.66~1.92배(몸이 발보다 빠름) → 0.57~0.60배(발이 몸보다 빠름)** 로 뒤집혔다.
  V22 속도 0.846 u/s를 대입해도 0.78배로 여전히 과보폭이다. 즉 부호만 바뀌었고 여전히 미끄러진다.
- **화면이 원인을 직접 보여준다.** 프레임의 Father는 전형적인 스프린트 split-kick 자세다. 644는
  `Lean_Forward_Sprint`이고, 보폭 1.073 u를 native 0.6초로 소화하면 `1.79 u/s`가 나온다. 사무실 보행
  0.666 u/s는 그 2.7분의 1이다. 보폭을 맞추려면 사이클이 1.61~1.69초여야 하는데, 그것은 스프린트의
  2.7배 슬로모션이다. 스프린트에는 두 발이 모두 뜨는 flight phase가 있고 보행에는 없으므로 시간
  늘이기나 pose 감쇠로는 해결되지 않는다.
- **"선명하지 않다"의 실제 원인은 해상도가 아니었다.** 텍스처는 4096 BC7로 복원됐고 그 자체는 옳은
  수정이지만, 사무실 뷰에서 Father가 차지하는 크기는 세로 약 60 px이라 4096은 크게 과표본이다. 실제
  원인은 알베도 자체가 어둡다는 것이다: luma 중앙값 57/255, 최대 196, 12.2%가 40 미만. `Unlit/Texture`는
  조명이 없으므로 화면 결과가 곧 텍스처이고, 밝은 2D 픽셀 가족 옆에서 어두운 덩어리로 읽힌다.
- **결론: 다음 수순은 소스 액션 교체다.** 수신부 결함 3건은 제거됐고 남은 것은 생성물 선택이다. walk
  액션 1건(8 credits, 잔액 76)이 다음이며, 사용자 명시 승인과 비용 재확인이 선행되어야 한다. 어두운
  알베도도 같은 자리에서 함께 판단한다. `productionEligible=false` 유지.
- 중간 선택지로 `poseStrength`를 0.63~0.80으로 두면 보폭과 이동이 맞는다(0.45의 0.439 u와 1.0의
  1.073 u를 잇는 선형 기준). 그러나 V21이 0.78을 이미 실루엣 근거로 기각했고, 이는 미끄러짐을 스프린트
  자세로 바꾸는 맞교환일 뿐 해결이 아니다.

## 2026-08-26 / Father V18 결함 3건 수정, 움직임 검증은 아직 미완

- 원본 영상은 `Downloads/rpg.mp4`로 이름만 바뀌었을 뿐 `캐릭.mp4`와 **동일 파일**이다(SHA-256
  `39DB58386FC8FFF7CF6D173A5552538C6D01F64959AEDC60495A8DC3E263843E` 일치). 아래 2026-08-25 항목의
  구간 분석은 이 파일에 그대로 유효하다.
- 영상 창작자도 **같은 action 644**를 쓴다. 흐물거림은 생성물 결함이 아니라 Unity 수신부 결함이었다.
  영상은 `higgsfield.ai/mcp`의 Claude Code 탭으로 MCP를 연결해 10개 액션(`base, idle-0, run-644,
  attack1-97, attack2-221, attack3-102, dodge-158, hit-178, death-189, regular-jump-466`)을 생성했고
  우리는 `idle-0`/`run-644` 2개만 보유한다.
- 크레딧 0으로 결함 3건을 고쳤다(`0be347b8`, `26425e9e`).
  1. 유료 4096×4096 알베도 2장이 `maxTextureSize 2048` + `Compressed`(품질 50)로 임포트되어 절반으로
     깎인 뒤 손실압축됐다. 4096 + `CompressedHQ`(품질 100)로 올렸다. 2048은 아무도 지정하지 않은 Unity
     기본값이었고 `Family3DPrototypeModelImporter`는 텍스처를 건드리지 않으므로 meta가 유일한 정본이다.
  2. `ResolveFatherMotionPoseStrength()`의 `0.45`를 `1f`로 복원했다.
  3. `SamplePose`의 접지 게이트를 풀어 imported-clip 경로에서도 발 고정이 실행된다. 두 경로 공용이
     되었으므로 `ApplyNaturalSdFootPlants`를 `ApplyFootPlants`로 개명했다.
- 새 게이트 `Family3DHiggsfieldAlbedoImportValidation`을 Fast QA `broadMethods`에 등록했다. 임포트된
  Texture2D를 PNG IHDR과 직접 비교하며, 측정 결과 두 알베도 모두 `4096x4096 BC7`이다.
- **검증된 것은 1번뿐이다.** 2·3번은 `editor-broad` 컴파일 PASS(18.5s)까지이며 움직임 품질은 확인되지
  않았다. 확인하려면 V22 QA 플레이어를 빌드·실행해야 하고, 근본원인 문서가 요구하는 연속 30/60 fps
  캡처가 아직 없다. `productionEligible=false`를 유지한다.
- 남은 의심은 소스 액션 자체다. 644는 `Lean_Forward_Sprint`이고 영상 게임은 실제 질주라 맞지만 우리는
  사무실 보행 `0.846 u/s`에 쓰고 있다. 1~3번 이후에도 어색하면 walk 액션 1건(8 credits, 잔액 76)이
  다음 수순이며 사용자 명시 승인과 비용 재확인이 선행되어야 한다.
- Higgsfield MCP 서버를 `~/.claude.json` user scope에 `higgsfield`(`https://mcp.higgsfield.ai/mcp`)로
  등록했다. 상태는 `Needs authentication`이고, `claude mcp`에 auth 하위 명령이 없으므로 대화형
  `/mcp`에서 사용자가 직접 로그인해야 한다. 인증 후 새 세션부터 도구가 붙는다.

## 2026-08-25 / 현재 최우선 상태: Father V18 움직임 미해결, Claude 핸드오프

- 사용자 최종 판정은 `USER_VISUAL_REJECTED_MOVEMENT_NOT_PROPERLY_AUDITED`다. V18 정적 root 이동,
  V19 idle-body/run-retarget, V20 native run 100%, V21 pose-strength 비교, V22 native run + `0.45` +
  exact albedo 어느 것도 움직임 합격본이 아니다. 자동 route/foot-lead/정지 시트 PASS는 사용자 화면 승인을
  대체하지 않는다.
- 원본 `Downloads/캐릭.mp4`는 639.418초, 1280×720, 30 fps이며 실제 in-game movement 구간은
  `305.93..354.90s`다. 이전에 적혀 있던 `304..368s`는 shot 경계 재측정 결과 틀렸다. `354.90s` 이후는
  4일차 3D 쇼룸 turntable이므로 cadence 기준으로 쓰면 안 된다. cadence 측정용으로 잘라 쓸 최장 클린
  구간은 `305.93..318.33s`(12.40초)다. 기존 QA는 매 6번째 moving frame을 캡처해 10 fps로 재생했으므로
  실시간 동작 검증이 아니었다.
- 현재 V22 진단 구성은 run-644의 native mesh/avatar/skin/clip, 정지 시 idle-0 motion, pose strength
  `0.45`, `Unlit/Texture` exact albedo, actual Father `OfficeRuntimeAgent` root, one-time projected-height
  scale이다. production에는 들어가지 않았고 `productionMutation=false`, `productionEligible=false`다.
- **2026-08-26 측정으로 원인 순서가 뒤집혔다.** 가장 먼저 고칠 결함은 cadence가 아니라
  `poseStrength=0.45`다. `ApplyPoseStrength()`가 모든 본을 rest에서 애니메이션 포즈 쪽으로 45%만
  slerp하므로 보폭이 같이 깎인다. 다리는 사이클당 0.29~0.34 신장을 만드는데 몸은 0.56 신장을 이동해서
  **몸이 발보다 1.66~1.92배 빨리 나간다.** 두 번째 결함은 `Family3DWalkActor.cs:293`이
  `ApplyNaturalSdFootPlants`를 `dedicatedNaturalSdWalk` 뒤에 두어 V18 경로에서 접지 판정·발 고정 IK가
  아예 실행되지 않는 것이다(런타임 영수증 `leftFootPlanted`/`rightFootPlanted`가 180/180 `false`).
  cadence stretch(1.6563466×)는 실재하나 2차이며, full pose strength 기준 stride-matched 사이클은
  1.15~1.33초로 현재 0.9938초보다 오히려 길다. native 0.6초로 가면 보행 속도가 1.9배 필요해 더 나빠진다.
  작업 순서와 근거는 [FATHER_V18_MOVEMENT_ROOT_CAUSE_2026-08-26.md](FATHER_V18_MOVEMENT_ROOT_CAUSE_2026-08-26.md)를 따른다.
- Higgsfield 생성은 detailed source 18 credits, idle 8, run 8까지 완료됐고 잔액은 76이다. 9-credit
  standard는 금지하며 새 유료 job은 사용자 명시 승인과 비용 재확인 전 제출하지 않는다.
- 구형 V18~V21 빌드/런타임/씬/로그/중복 출력은 정리했다. paid/raw GLB, 생성 영수증, 현재 motion source,
  V22 scene/evidence, stash는 보존했다.
- handoff 정리 후 사람이 작성한 C#/MD/PY/JSON scoped `git diff --check`, motion/static source SHA-256 5건,
  Unity `6000.3.21f1` `FAST_QA_WINDOWS.cmd -Profile editor-broad`를 다시 확인했다. Fast QA는 변경 28개를
  import/compile해 `PASS`, total `58.936s`, compile `55.396s`를 기록했다. 이는 코드/에셋 정합성 검증이며
  사용자 움직임 화면 승인으로 해석하지 않는다.
- 다음 작업의 정본은
  [CLAUDE_FATHER_V18_MOVEMENT_HANDOFF_2026-08-25.md](CLAUDE_FATHER_V18_MOVEMENT_HANDOFF_2026-08-25.md)다.

## 2026-08-25 / 현재 최우선 상태 오버라이드: Father V14 stylized SD 보행 J

- 사용자가 실제 GIF를 보고 V13을 즉시 문제 있다고 판정했다. V13은 `USER_VISUAL_REJECTED`이며 자동 QA와
  4장 정지 시트는 합격 근거가 아니다. 확대 검수한 원본 24프레임에는 바지/다리가 한 검은 기둥으로
  합쳐지고 신발이 떨어져 보이는 구간, 이동하는 root 위에 지지 없이 같은 자세가 끌리는 발 미끄러짐,
  약한 무릎/체중 이동, 비대칭으로 고정된 팔, 18→19 방향 전환과 23→0 루프 점프가 있었다.
- 정적 `FatherApprovedV14`/Proof23 외형은 사용자 승인 상태를 그대로 유지한다. 외형 좌표는 다시 만들지
  않았다. Proof25를 감사해 두 신발 1,228개 weight membership이 A-pose 손가락으로 잘못 전이된 것을
  확인했고, Proof26은 승인 신발 좌표를 바꾸지 않고 왼발/오른발 bone에만 각각 재배정했다. 새 격리 FBX는
  `Candidates/FatherApprovedV14NaturalWalkRigV1/father-approved-v14-natural-walk-rig-v1.fbx`, SHA-256
  `0A4AE8A1620A9E7F85BF0A072DCB7B5553D2C584DC550A38EAC0DE2349383773`이다.
- G 후보는 shared human clip을 감쇠하지 않았다. `Family3DWalkActor`의 Father 전용 SD 2족 보행이
  좌우 반대 위상 허벅지/무릎, 짧은 두 발 접지, 작은 골반 체중 이동, 몸 옆의 작은 반대팔 스윙, 짧은
  two-bone foot plant를 사용한다. 실제 Father `OfficeRuntimeAgent`가 동일 Starter Office의 3x3 외곽을
  두 바퀴 이동하며 180프레임을 캡처했다. 첫 한 바퀴 113프레임 전체와 실제 다음 프레임까지 확대 검수해
  신발 분리, 지속 발 끌림, 다리 비이족성, 팔 벌림, 거인 축척, 레이아웃 전환, 루프 끝 점프가 없음을 확인했다.
- 격리 build/runtime은 `FatherApprovedV14NaturalWalkV14BuildG` /
  `FatherApprovedV14NaturalWalkMapRuntimeV14G`이다. runtime receipt는
  `FATHER_NATURAL_MAP_WALK_PROOF_COMPLETE`, build와 runtime 모두 `productionMutation: false`,
  `productionEligible: false`다. production scene, preview, EditorBuildSettings의 before/after SHA는 동일하다.
  사용자는 실제 GIF를 보고 걷기가 너무 흐물거리고 징그럽다고 판정했다. G는
  `USER_VISUAL_REJECTED_FLOPPY_RUBBERY_MOTION`이며 재사용하지 않는다.
- H는 G의 lateral hips 이동과 IK residual pelvis 이동을 모두 제거했다. 연속 sine 다리 곡선을
  접지–통과–스윙–착지 key-pose 곡선으로 바꾸고, 접지 보정은 주기의 `0.11`까지만 허용하며 관절별
  보정 각도를 제한했다. 팔 진폭은 `±0.025`, 무릎 굽힘은 `0.23`, 허벅지 진폭은 `0.14`로 줄였다.
  `FatherApprovedV14FirmSdWalkMapRuntimeV15H`의 첫 회전 114프레임 전체에서 골반/몸통 흔들림과 고무 같은
  하체 늘어짐이 사라졌는지 확대 검수했다. 사용자는 H도 걷기가 못생기고 팔이 사실상 정지해 있다고
  판정했다. H는 `USER_VISUAL_REJECTED_STIFF_GLIDING_STATIC_ARMS`이며 재사용하지 않는다.
- J는 사용자 지시에 따라 실제 Blue Archive SD 카페 보행/선택 동작을 프레임 단위로 다시 관찰하고,
  저작물이나 asset을 복사하지 않은 채 동작 원리만 반영했다. 몸통/머리 실루엣은 읽기 쉽게 고정하되
  지지 다리는 긴 구간 뒤로 밀고, 반대 다리는 짧은 구간에 무릎을 들어 회수한다. 팔꿈지는 계속 굽힌 채
  양손이 허리 앞/몸 뒤로 분명히 교대하는 반대 스윙을 한다. `0.88s` cycle, upper-leg `0.18`, swing-knee
  `0.34`, arm front/back `±0.16`, forearm bend `-0.24`이며 pelvis translation은 계속 `0`이다.
  `FatherApprovedV14StylizedSdWalkMapRuntimeV17J`의 실제 첫 회전 110프레임 전체를 확대 검수해 팔 교대,
  좌우 신발/무릎 회수, 8개 route leg와 4개 방향, 끝 경계를 확인했다. build/runtime은
  `FatherApprovedV14StylizedSdWalkV17BuildJ` / `FatherApprovedV14StylizedSdWalkMapRuntimeV17J`, build와
  runtime 모두 `productionMutation: false`, `productionEligible: false`다. J도 사용자 GIF 합격 전에는
  후보일 뿐이며 자동 PASS로 합격 처리하지 않는다.

## 2026-08-24 / 현재 최우선 상태 오버라이드: 사용자 소유 Mika/Yuuka 직접 개조

- **최신 사용자 화면 판정:** Older Sister의 connected-clothes 정적 후보
  `SisterProof66ConnectedClothesApprovalCandidate`는 사용자가 "합격"으로 판정했다. 이는 외형/연결 방식의
  화면 승인이고, 아직 rig transfer·걷기·Unity·production 승격 승인은 아니다. Player는 계속
  `FAIL_CLOSED / DIAGNOSTIC_ONLY`; Father의 v12까지는 탈락했고 최신 v14/Proof23은 사용자가
  `USER_VISUAL_APPROVED_STATIC`으로 승인했다. Father v14는 이후 118본 rig transfer, Unity Humanoid import,
  공유 걷기와 실제 StarterOffice 복제본 D3D11 수치 QA까지 통과했지만, 사용자가 V4 GIF에서 거인처럼 큰
  축척, 2족 보행으로 읽히지 않는 다리, 기괴한 팔 동작을 확인해
  `USER_VISUAL_REJECTED_GIANT_SCALE_NON_BIPEDAL_ARM_MOTION`으로 불합격했다. 정적 v14와 rig 승인은 유지하며,
  현재 열린 gate는 shared human clip을 쓰지 않는 Father 전용 SD 보행과 맵 축척 V5다.
- Mother는 사용자 소유 Mika 얼굴·눈·chestnut hair·원본 3-digit 손을 좌표/가중치 그대로 유지하고,
  VRoid Studio 2.14.0 built-in Body_0/Body_1/Body_3를 연속 피부·한 장 의상·맞물린 신발로 변형한
  v11은 사용자가 목–몸 분리 틈을 발견해 `USER_VISUAL_REJECTED_NECK_DISCONNECTION`으로 폐기했다. 수정본
  `MotherProof13ShortConnectedNeckGate`도 사용자가 얇고 길며 꺾인 흰 목 실루엣을 발견해
  `USER_VISUAL_REJECTED_CREEPY_THIN_NECK`으로 폐기했다. 이후 `MotherProof15ChibiNeckHiddenGate`의 8방향
  내부 검수에서 목처럼 보이던 회색 직사각형이 실제로는 Mika 얼굴 메시의 rear-lower shell임을 확인해 해당
  후보도 `USER_VISUAL_REJECTED_EXPOSED_REAR_FACE_SHELL`로 봉인했다. 이어서
  `MotherProof21RoundedUnderChinGate`는 목을 지나치게 숨겨 사용자가
  `USER_VISUAL_REJECTED_NO_VISIBLE_NECK`으로 폐기했다. 최신 `MotherProof26ApprovedYuukaNeckShapeGate`는
  합격한 누나 `SisterProof66ConnectedClothesApprovalCandidate`의 `SisterProof46SmoothNeckBridge`
  160 vertices/120 polygons와 위가 좁고 아래가 넓은 taper를 직접 복사한다. 엄마 체형에 X/Y `1.80` fit,
  Z `1.105..1.225` overlap으로만 조절해 연속 피부 메시 안에 병합했고, Mika head rigid world drop은
  `0.035`로 되돌려 목이 실제로 보이면서 턱·블라우스 양쪽과 겹치게 했다. rear-lower face polygon 80개만
  숨기고 앞턱·귀·눈·머리카락 및 local 좌표/weights는 보존한다. 몸통 중앙/카라 `0.045` lift도 유지한다.
  사용자 2D 권위의 dusty-peach cardigan,
  cream blouse, dark-teal calf A-line skirt, dark-brown shoes, 표면 밀착 단추와 왼손목 시계를 반영했다.
  정적 컬러/회색 4면과 24-frame appearance-only turntable 내부 QA, 원본 Mika 전체 좌표/weights·151 bones,
  native hand 좌표/weights exact, `test3.zst` 제외 gate와 목 근접 3면/회전 8면 검수는 PASS했다. 현재 상태는
  사용자가 정적 외형을 승인하여 `USER_VISUAL_APPROVED_STATIC`; rig transfer·걷기·Unity import·production 승격은 다음 게이트 전까지 보류한다.
- 현행 fail-closed gate는 (1) 떠 있는/겹친 garment assembly part 0과 곡면 접점, (2) 사용자가 후속 승인한
  Mika/Yuuka 원본 3-digit SD 손을 변형 손상 없이 그대로 유지하고 receipt에도 정확히 기록하는 것이다.
  사용자는 후속 대화에서 부모/누나가 같은 젊은 chibi 비율로 읽히는 점은 그대로 유지해도 된다고 승인했으므로,
  과거의 역할별 성인 나이 실루엣 요구는 현재 외형 차단 gate가 아니다.

- **Older Sister Proof22 사용자 탈락:** `SisterProof22DirectionGate`와 32-frame GIF도
  `USER_VISUAL_REJECTED_CONNECTIONS`다. 원본 Yuuka 얼굴·눈·머리·3-digit 손 보존은 유효하지만,
  팔–어깨, 목선, 상의–허리, 반바지 좌우·가랑이가 겹친 별도 표면으로 남아 회전에서 틈·단차가 보였다.
  낮은 검은 임시 신발도 2D 권위의 완전한 맨발과 다르다. 다음 누나 proof는 상의 아래의 연속된
  shoulder/torso skin, 허리·좌우 통·가랑이가 하나인 connected shorts, 같은 메시의 piping material region,
  다리와 이어진 완전한 맨발을 정적 4면에서 먼저 통과해야 한다. Proof22는 Unity/GIF/production 근거가 아니다.
- Father의 과거 `FatherAdultMorph6SurfaceGate`는 후면 eye/socket 노출과 성인 비율 수치는 고쳤지만 큰
  Yuuka 눈, 젊은 여성 landmark, bob/helmet hair, boxy suit 때문에 `DIAGNOSTIC_ONLY_STYLE_FAIL`로 봉인했다.
  최신 정적 후보 `FatherProof6VoxelContinuousOfficeGate`는 기존 FatherProof3의 사용자 소유 Yuuka 얼굴,
  짧은 회색 머리, 안경, 표정, 원본 3-digit 손과 118-bone rig를 그대로 유지한다. 실패한 torso·sleeve·cuff·
  forearm·pelvis·trouser volume은 결과 파츠로 남기지 않고 `0.0045` voxel union과 10회 organic relax로 한
  연속 표면으로 재구성했다. 누나 정적 합격본의 `SisterProof46SmoothNeckBridge` 160 vertices/120 polygons를
  같은 body object에 병합하고 셔츠 neckline과 겹쳤다. 셔츠·소매·팔·바지·벨트는 같은 표면이며, 단추·버클·
  시계만 표면 밀착 액세서리다. 4면 컬러/회색과 24-frame appearance turntable, 원본 전체/손 좌표·weights 및
  118 bone-name exact gate는 통과했지만, 사용자가 사다리꼴 상체·두꺼운 골반 띠·상하체가 따로 노는 낮은
  외형 품질을 지적해 `USER_VISUAL_REJECTED_UPPER_LOWER_BODY_QUALITY`로 폐기했다. rig transfer·걷기·Unity
  import·production 승격은 금지하며, 이 voxel-union 몸통은 다음 후보의 베이스로 재사용하지 않는다.
  후속 `FatherProof8ShoulderWaistQualityGate`는 VRoid Studio 2.14.0의 실제 M00 male Body 2617 표면으로
  shoulder fold와 골반 상자 문제를 제거했지만, 사용자가 눌린 얼굴과 가시처럼 가는 팔을 발견해
  `USER_VISUAL_REJECTED_SQUASHED_FACE_SPIKE_ARMS`로 폐기했다. `FatherProof10FaceRoundedArmGate`도 사용자가
  휘어진 팔과 턱 아래 겹친 rear-cranium 음영을 지적해 `USER_VISUAL_REJECTED_WARPED_ARMS_DOUBLE_CHIN`으로
  폐기했다. `FatherProof18SealedArmSingleJawGate`는 head shell·눈·안경·머리의 X `0.91`, Y `0.96`,
  Z `1.10` anti-squash 보정을 유지하되, 턱 아래로 내려오던 기존 rear coverage는 render에서 제외하고
  턱선 위에서 끝나며 머리카락 뒤에 놓인 rounded cranium으로 대체했다. 팔은 휘어진 male-surface forearm을
  제거하고 각 측면마다 shoulder–wrist 한 메시, 15 rings/24 segments, shirt/skin material-only 경계로
  재구성했다. 시작·끝 단면은 몸통과 native hand 안에 묻히며 4면에서 겨드랑이/손목 틈이 없다. native hand
  island 좌표/weights와 118 bone names는 exact지만, 새 rear-cranium에 UV-mapped face material을 잘못 넣어
  뒤통수가 흰 피부색으로 드러났으므로 `USER_VISUAL_REJECTED_REAR_CRANIUM_COLOR_MISMATCH`로 폐기했다. 최신
  `FatherProof19RearHairColorGate`는 형상과 얼굴 피부는 그대로 두고 rear-cranium만 short-hair under-cap과
  동일한 UV-independent `FatherCharcoalHair`로 교체해 흰 후면 패치를 제거했지만, 사용자가 뒤에서 셔츠색
  엉덩이가 보이는 것을 지적해 `USER_VISUAL_REJECTED_SHIRT_COLORED_SEAT`로 폐기했다. 원인은 VRoid authored
  trouser를 source Z `0.940`에서 잘라 Father Z 약 `0.400` 아래까지만 남겼기 때문이다. 최신
  `FatherProof21TuckedShirtTrouserSeatGate`는 trouser의 authored waist/seat를 source Z `1.125`까지 유지해
  Father Z 약 `0.476`까지 올리고, 셔츠 밑단은 source Z `1.040`으로 올려 waistband 안에 넣었다. 따라서
  앞·옆·뒤에서 엉덩이는 전부 charcoal trousers로 이어지고 blue shirt hip fragment는 없다. 후속
  `FatherProof22TaperedTrouserWaistGate`는 authored waistband 립의 좌우 선반이 아직 남아 내부 폐기했다. 최신
  `FatherProof23FlushWaistGate`는 trouser Z `0.425..0.476` half-width를 `0.109..0.108`로 평탄화하고 shirt
  Z `0.440..0.490`을 안으로 tuck해 belt–shirt–trouser가 한 줄로 이어진다. 뒤통수·팔·턱 수정, native hand
  좌표/weights 및 118 bone names exact도 유지한다. 컬러/회색 4면과 24-frame 360도 turntable 내부 QA를
  통과했고 사용자가 `USER_VISUAL_APPROVED_STATIC`으로 승인했다. 승인 blend에서 118본과 원본 body 좌표/weights
  hash를 유지한 채 새 의상/팔다리에 4-influence weight transfer를 적용해
  `Candidates/FatherApprovedV14/father-approved-v14-rigged.fbx`를 만들었다. Unity Humanoid Avatar는 valid/human,
  complete SkinnedMeshRenderer 1개, 18,427/18,427 weighted vertices다. 첫 실제 맵 render에서 공유 clip의 진폭이
  과해 다리가 벌어지는 것을 화면으로 발견해 불합격 처리하고, 승인 정적 rest pose 대비 회전/이동 진폭을
  `0.32`로 감쇠했다. 최종 `FatherApprovedV14MapWalkRuntime4`는 D3D11 24-frame visual-content PASS,
  movement sample `3,495`, 모델 moving frame `2,481`, 8방향 mask `255`, gait phase
  `0.000824..0.999747`, replan/static/interaction/agent penetration 모두 `0`이다. 실제 `Prototype01`과 preview,
  EditorBuildSettings before/after SHA는 동일해 맵 수정은 필요 없었다. 그러나 사용자가 실제 GIF에서 거인
  스케일, 비2족 다리, 기괴한 팔 동작을 확인했으므로 motion V4는
  `USER_VISUAL_REJECTED_GIANT_SCALE_NON_BIPEDAL_ARM_MOTION`으로 봉인한다. `poseStrength 0.32`와 shared human
  clip 감쇠 방식은 재사용하지 않는다. 정적 v14/Proof24 rig 승인만 유지하며 production 승격은 금지한다.
  후속 V13은 shared clip을 완전히 우회하고 `HumanPoseHandler`로 SD 전용 보행을 구현했다. 기존 viewport
  height의 `0.55`, upper-leg `±0.05`, swing-knee `-0.09`, arm down `-0.48`, arm swing `±0.008`이며
  torso/head/hips는 승인 rest pose로 복원한다. 팔을 내릴 때 바지가 같이 끌리던 원인은 nearest-surface
  transfer가 바지에 넣은 arm/hand membership `1,604`개였고, Proof25에서 모두 제거했다. 팔 영향만 있던
  `927`개 바지 정점은 pelvis/thigh/calf 체인으로 재배치했으며 forbidden influence after는 `0`이다.
  새 FBX SHA는 `88734B5F16598B2027FC7F54139F27E17C6912755E2907F09EB97CBC72497094`, Unity Humanoid는
  valid/human이다. `FatherApprovedV14MapWalkRuntimeV13`은 D3D11 moving `2,502`, direction mask `255`, gait
  `0.000826..0.999978`, composite `24`, collision/penetration/replan `0`이다. V13도 사용자 실제 GIF 화면
  승인 전 production 승격은 금지한다.
- Player `PlayerOriginalSurface15PolishedGate`도 원본 3-digit 손·118본·weights는 보존했지만 큰 dome/helmet
  cap, 불명확한 brim, 후면 face/eye aperture 노출, 상의의 큰 V-hole과 열린 어깨 seam 때문에
  `FAIL_CLOSED / DIAGNOSTIC_ONLY`다. Proof16은 몸에 맞는 원본 coherent jacket surface, 후면 scalp/socket
  차폐, 납작한 단일 newsboy crown+brim 경로로 재시도하며 GIF/Unity는 계속 금지한다.

- **동일 날짜 내부 진단 결과:** 수치 topology 통과를 화면 합격으로 오인하지 않기 위해 아래 시도를 모두
  `DIAGNOSTIC_ONLY_*_FAIL`로 봉인했다.
  - Mother `MotherTextureFirst1`: 원본 Mika surface 재사용으로 조립 티는 줄었지만 fantasy dress,
    허리길이 머리, 장식, teen face가 남아 identity FAIL.
  - Father/Mother `HeadOnlyMorphPrototype`: rig/weight hash와 vertex 이동은 검증됐지만 face shell만 바꾸자
    EyeMouth plane/UV/socket/lash/hairline이 따로 남아 검은 눈구멍·pinched mask chin이 생겨 visual FAIL.
  - Player `Proof10TopologyGate`: 각 의상이 1 component/closed/all-quad여도 모자·상의·바지·신발이 상자처럼
    읽혀 style FAIL. `Proof11SilhouetteGate`는 donor-fitted 바지·신발이 개선됐지만 cap 관통, blazer형 lapel,
    검은 V-neck gap, 분리된 원통 hood 때문에 아직 style FAIL 상태다.
  - Mother `MotherConnectedTopology1`: cardigan/blouse/skirt/loafer/hair의 자동 manifold gate는 통과했지만
    직사각 cardigan, tube sleeve, cone/slab 의상, curtain+sphere hair, ball loafer와 약 2.5등신 child read로
    44세 실루엣을 통과하지 못했다.
  - Older Sister `SisterProof4ProportionGate`: 다리 직선화·슬림화와 toe capsule 제거는 개선됐지만 tank/shorts가
    여전히 절차형 volume이고 arm seam·oval-pad foot·부족한 20세 body cue가 남아 style FAIL.
  - Father `FatherCoordinatedHead1`: socket void와 iris 정렬은 개선됐지만 balloon/Wii-avatar face, decal eye,
    pasted ear, helmet hair, remesh chin ripple로 polished anime/46세 외형에 실패했다.
- 따라서 현행 작업은 primitive/cage/voxel 외형을 더 다듬는 단계가 아니라, Mika/Yuuka의 이미 몸에 맞는
  고품질 donor face·ear·hair·garment surface를 직접 crop/reshape하고 필요한 socket/UV/material을 함께
  고치는 단계다. 위 진단본 어느 것도 사용자 제시·GIF·Unity 후보가 아니다.

- 사용자는 절차형 Runtime-2D V2/Final3/Final4 외형을 화면에서 명시적으로 탈락시켰다. 아래의 구조,
  Humanoid, build, walk PASS는 역사적 진단 자료일 뿐이며 현행 외형 후보가 아니다.
- 사용자는 `Downloads/test.7z`와 `Downloads/test2.targz`의 소유권을 명시적으로 확인하고, 그 안의
  Mika/Yuuka 기존 3D 얼굴·눈·몸·손·리그를 직접 수정해 네 가족 캐릭터를 만들도록 지시했다. 따라서
  현재 작업은 새 절차형 얼굴/몸을 만드는 흐름이 아니라, 해당 두 베이스를 격리된 로컬 proof에서 직접
  개조하는 흐름이다. 가족 HighMotion 2D는 여전히 역할별 머리·의상·색·나이·실루엣 authority다.
- archive 안에는 제3자가 검증할 수 있는 license/transfer 문서가 없으므로 이 소유권 기록은 사용자의
  attestation이다. 현재 산출물은 사용자가 요청한 개인용·로컬 시각 proof로만 취급한다. 명시적 화면 승인과
  별도 provenance/shipping 결정 전에는 production/default Unity 경로로 복사, 공개 배포, 판매, 원본 payload
  commit을 하지 않는다.
- `test3.zst`/Sakurako는 계속 완전 제외한다. bundled LICENSE가 NAT GAMES/NEXON 소유·비상업·ripped
  asset이라고 명시하며, 사용자가 제외를 지시했다. 격리 보관본 외에는 어떤 모델/측정/텍스처/리그 입력에도
  사용하지 않는다.
- 현재 직접 개조 연구는 Player=Yuuka, Mother=Mika, Older Sister=Yuuka, Father=Yuuka의 기존 proof를
  모두 탈락 기준선으로 보존하고 위 세 원인을 분리 진단하는 단계다. 큰 opaque EyeMouth mouth plate는 원본 eye/face topology와
  분리된 32-polygon component만 삭제하고 작은 표면 밀착 mouth volume으로 교체한다. 어떤 proof도 사용자
  합격 전에는 Unity 통합 대상이 아니다.

## [현재 오버라이드로 대체됨] 2026-08-24 / Runtime-2D V2 Final3 네 명

- 기존 V1 네 명과 V1 turnaround는 `USER_VISUAL_REJECTED`다. 아래 V1의 Humanoid/Unity/D3D PASS는
  과거 구조 증거일 뿐 시각 승인이나 production 근거가 아니다. 현재 외형 authority는
  `Docs/FAMILY_3D_RUNTIME2D_V2_STYLE_LOCK_2026-08-24.md`, 실제 runtime HighMotion 2D(P0), 현재
  high-resolution neutral art(P1) 순서다.
- Player/Father/Mother/Older Sister는 네 병렬 작업 흐름과 상호 교차검수로 새 topology/atlas/23-bone
  Humanoid 후보를 만들었다. 첫 결과 중 Father/Mother/Older Sister는 놀란 눈, 턱 아래 틈, block bow,
  긴 목과 손발 때문에 내부 시각 gate에서 탈락했고 Final2로 다시 만들었다. Final2 전체 24방향 검수에서
  Father 주머니 outline과 Older Sister 반바지 piping이 측면에서 뜨는 문제를 다시 발견했고, Final3는 둘을
  몸/의상 곡면과 공유하는 filled surface로 교정했다. 독립 재검수 결과 현재 네 역할 모두
  **사용자 합격/불합격 판정용 WIP**로 제시할 수 있으나, 상용 anime-game 마케팅 완성도나 production
  승인을 뜻하지 않는다.
- 현재 isolated 후보와 SHA-256:
  - Player `Candidates/PlayerV4/player-runtime2d-humanoid-v4.fbx`
    `E58A5F8BA8AC4762C6725C72904FD481F34E041448B37B76231592159AF7CBEC`; atlas
    `5C59CD5D3849F6728B9C14DCD9E727B875627B8C7A861FF84D9CF9A50BEC6EFA`.
  - Father `Candidates/FatherV2/father-blender-humanoid-v2.fbx`
    `3BCE9817601FAE8B28DC243A886F7A42F85656147E9B3AFEA946C215BE5F3389`; atlas
    `6632287537E03766978D7FD1F88F013268ED41D226088E611A1FAE3BAD2D06EA`.
  - Mother `Candidates/MotherV2/mother-blender-humanoid-v2.fbx`
    `995E349444A0940727D8C49CD7C1BC20BE6363AE60B578662BE394A6D39AA587`; atlas
    `CAF17B633AECE52400E030319CF63B2E16F93895323B189D0FAD0513B3F342BF`.
  - Older Sister `Candidates/OlderSisterV2/older-sister-blender-humanoid-v2.fbx`
    `81219B4B6E787CF3F736E09B6F02FAEB7AC915AAB359D4534CD25188C4846249`; atlas
    `452201D1643DC20268C71279B5D086D809086F9DFA256B2B4676C08AD6D90A59`.
  위 상대 경로 기준은 `Assets/FamilyCompany/Experimental/Family3DPrototype/`이다.
- Blender fresh-FBX round trip은 네 명 모두 complete skinned mesh 1, armature 1, material 1, UV0 1,
  bones 23, required missing 0, unweighted/invalid-weight 0으로 PASS했다. Unity Final3 all-import receipt
  `Artifacts/Family3DRuntime2DV2/UnityImport/all-import-receipt.json` SHA-256
  `DA247AFD5CCC4A3A661DF568468B74E2427E4B0C2AF555BE6E6CB4E91FDE2BA1`도 네 Avatar valid/human,
  renderer/material/UV 1, bind/skin bones 23, Humanoid missing/mismatch 0을 기록한다.
- isolated Unity build `Artifacts/Family3DRuntime2DV2/BuildRun2/`은 Unity `6000.3.21f1` Windows build
  `Succeeded`이며 build receipt SHA-256은
  `C938F201D9A1190D125DFADDDB48386A50845C21BA8623427E72AC3AEC7A22AB`이다. D3D11 실제 shared-walk run
  `D3D11Run2/qa-receipt.json`은 420/420 visual frames, 13.906058 s, direction pose masks 전부 `63`,
  route/root/audio/P0-P3 lead-foot alternation을 PASS했고 receipt SHA-256은
  `4D675CD7D03EDFF35FD1775024FDDE7C0F763CDF1B6D1BB29B59FADB163CCC54`다. Blender 2D 비교/GIF는
  `VisualReviewFinal3/`, 실제 Unity 걷기 GIF/MP4는 `D3D11ReviewFinal3/`에 있다.
- DeviantArt 공개 preview PNG 세 장과 사용자가 `Downloads`에 제공한 Mika/Yuuka archive는 ignored
  `Artifacts/ExternalReferenceStudy/`에서만 추상 비율, iris/sclera, hair-clump, limb taper, toon 언어를
  관찰한다. 사용자는 Mika/Yuuka archive 소유권을 주장했지만 파일 안에 이를 검증하는 license/transfer
  문서는 없으므로 원본 FBX/mesh/texture/rig는 후보에 복사하거나 Unity import하지 않는다. `test3.zst`의
  Sakurako는 bundled LICENSE가 NAT GAMES/NEXON 소유·상업 사용 금지·ripped asset이라고 명시했고 사용자
  지시에 따라 참고 세트에서 제외·격리했다. 가족 자체 2D가 계속 identity authority다.
- 현재 한계는 측면 깊이, hair/cloth secondary motion, 손발과 의상 일부의 procedural 단순화다. 다음
  gate는 사용자가 비교표·turntable·Unity walk GIF에서 역할별 합격/불합격을 정하는 것이다. 명시적
  승인 전 전부 `productionEligible: false`; production/default/StarterOffice/Downloads는 변경하지 않는다.

## [사용자 시각 탈락 기록] 2026-08-24 / 신규 3D V1 네 명 + Player 실제 사무실 이동 자동 gate PASS

- Player/Father/Mother/Older Sister 신규 구현은 네 turnaround만 identity 입력으로 사용해 Blender에서
  각각 독립 제작했다. 기존 2D, Player V1/V2, Styloo, 기존 mesh/texture/decal/motion donor나 fallback은
  사용하지 않았다. 네 결과는 모두 **isolated candidate**이며 production 교체·승인이 아니다.
- 각 FBX는 complete skinned body 1개, material 1개, atlas 1개, bone 23개, bottom-centre `Root`, explicit
  Unity Humanoid mapping을 가진다. Unity에서 모두 같은
  `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx`를 retarget한다.
- 후보 정본과 SHA-256은 다음과 같다.
  - Player `Candidates/PlayerV3/player-v6-blender-humanoid-v3.fbx`
    `80CEEC5269D229D213DEBF17B90EB99FDB93B9DB60B8D3416AAB779D1A657EA9`; atlas
    `46DD6CA613465C5E65338701AECB8FF029CB22C0059716CEEC5C9ED7ED6D7C8F`.
  - Father `Candidates/FatherV1/father-blender-humanoid-v1.fbx`
    `417D28116037D23895AAA813089BD0EC25E1786370E60FECAE2BAB1B8761591F`; atlas
    `6A271252664216266874DF5FDCD40775DFA3AF2D88747C4664C63E1D4ED334EA`.
  - Mother `Candidates/MotherV1/mother-blender-humanoid-v1.fbx`
    `59F0FB77C23FD9BD5457E2305E86DAFACD9BB3D62F4BE079ADA8D1CC65F85E01`; atlas
    `4FA4D826132C72787CA740E917BB0B29A958C31D47E062D6B7B2C4705722D9A2`.
  - Older Sister `Candidates/OlderSisterV1/older-sister-blender-humanoid-v1.fbx`
    `51EE97D6278038EDA30E24D74E62C75FC4AA00086D0C119BF76F54A2FE0B15D4`; atlas
    `BAC4245933C91D5CDFBEADB9280F670CC7D1F93DA29B52BF9514EAA37B5EF48A`.
  위 상대 경로의 기준은 `Assets/FamilyCompany/Experimental/Family3DPrototype/`이다.
- generic `validate_family_humanoid_fbx.py`를 exactly-one UV layer/active sole UV0 fail-closed로 강화한 뒤
  네 FBX를 모두 재검증해 PASS했다. active UV0은 Player `PlayerV3AtlasUV`, Father `IdentityAtlasUV`,
  Mother `UVMap`, Older Sister `OlderSisterV1AtlasUV`다. Father의 초기 다중 UV0 문제는 실제 D3D 화면에서
  발견했고 sole `IdentityAtlasUV`로 수정한 최종 FBX만 정본이다.
- Unity all-import receipt는 `PASS_VISUAL_AND_MOTION_REVIEW_REQUIRED`다. 네 Avatar가 valid/human이고
  skinned renderer 1개, material 1개, skin/bind bones 23개, required Humanoid mismatch 0을 기록하며
  `productionEligible: false`다.
- isolated identity showroom `Artifacts/Family3DIdentityCandidateV1/BuildRun3/`은 Unity
  `6000.3.21f1` Windows build `Succeeded`다. `D3D11Run4Final`은
  `AUTO_PASS_VISUAL_REVIEW_REQUIRED`: 420 frames/13.9100847 s, visual content 420/420, 네 방향 pose mask
  모두 `63`, route/root continuity·audio mute·P0/P3 해부학적 lead-foot alternation을 PASS했다.
- 최초 두 `ScreenCapture` 계열 run의 검은 프레임은 증거로 거부했다. 최종 run은 camera
  `RenderTexture + ReadPixels`와 frame별 luma gate를 사용했으므로 검은 캡처를 자동 PASS할 수 없다.
- actual-office QA-only adapter는
  `Runtime/Family3DStarterOfficeCandidateQa.cs`, builder는
  `Editor/Family3DStarterOfficeCandidateQaBuilder.cs`, generated scene은
  `Scenes/Family3DStarterOfficeCandidateQa.unity`다. 위 상대 경로 기준은
  `Assets/FamilyCompany/Experimental/Family3DPrototype/`이다. Builder는 `Prototype01` copy와 read-only
  `OfficeTileMigrationPreview`만 explicit build scenes로 사용하고 Editor Build Settings를 영구 변경하지 않는다.
- `Artifacts/Family3DStarterOfficeCandidateQaV1/BuildRun6SinglePassFinal`은 `Succeeded`,
  `productionMutation: false`, `productionEligible: false`다. before/after SHA-256은 `Prototype01`
  `5970EF496ACD81E7A0646A96807448E2283AB96F7D4866C234A09140D5872CD1`, preview scene
  `1EC8C2156D887F083CB5F4EB63BB46D5F9451C3F9CAC8C239688D86F7AD0DA1F`, EditorBuildSettings
  `010B57B9A51DE91C83FC9C7465DECFA0563214C74EA6A7E1DB5A991879890590`으로 각각 동일하다.
- `RuntimeRun6SinglePassFinal`은 Starter ready/4 bindings와 project official MovementLayout QA를 PASS했다.
  Player 8-direction mask `255`, static collision `0`, interaction `0`, penetration `0`; adapter moving sample
  4,165 frames, Player moving 2,651 frames, gait phase `0.000248..0.999476`; composite 3장, luma
  `199..216`, visual-content PASS다.
- adapter는 2D XY → production camera viewport → overlay ray/`Y=0`,
  `yaw=(direction-4)*45°`, live sprite bounds scale을 사용한다. 실제 프레임에서 base camera와 overlay가
  같은 QA layer 30을 이중 렌더하는 문제를 발견해 QA 동안 base culling에서 layer 30을 제외하고 종료 때
  원복했다. 최종 프레임에는 네 후보가 각각 정확히 한 번만 보인다.
- office coverage는 Standing/Navigating 3D만이다. approach/seated/work/egress는 원래 2D presentation을
  복구한다. actual-office 8방향 이동을 직접 구동한 것은 Player 한 명이고, 나머지 세 명은 binding/scale/
  standing을 검증했다. 네 명 모두의 shared walk는 isolated `D3D11Run4Final`에서 검증했다.
- 다음 gate는 원본 해상도 4-view/turntable과 두 D3D final run의 사람 화면 검수 → full-3D furniture
  occlusion/seating 지원 및 검증 → 사용자 승인이다. 그 뒤에도 별도 production migration 승인 전에는
  교체하지 않는다.
- Higgsfield/Unity AI Beta는 더 이상 후보 완성의 blocker가 아니다. 이전 Higgsfield 시험은 10 credits에서
  생성 전 `not_enough_credits`로 끝나 generation/transaction 0건이었고, Unity AI는 rig 없는 static-base
  fallback일 뿐이다. 계정 제한 우회, 무단 credit 사용, package/Cloud 연결은 하지 않는다.
- 실제 production 2D runtime, `StarterOfficeV1` default 경로, 기본 실행본과 Downloads 배포본은 변경하지
  않았다. 현재 네 후보와 모든 receipt는 `productionEligible: false`이며 human visual review required다.

## [퇴역 기록] 2026-08-21 / 8방향 24단계 자연 보행 V3

- 다운로드 APK의 `data.unity3d`를 UnityPy로 직접 읽어 KShopGo `Walk`이 loop, in-place,
  `0.80000007s`, 30fps, dense frame count 26임을 재확인했다. 실제 동작은 한 주기에 24개 시간 구간을
  연속 평가한다. 기존 2D 후보가 0/4/8/12/16/20 여섯 장을 각 133.3ms 동안 그대로 유지한 것이
  똑딱거림의 직접 원인이었다.
- 새 격리 후보는 6개 접지 key pose 사이를 각 4단계로 연결해 방향당 24포즈, 8방향 192장을 만든다.
  physical left/right owner와 정면 다리 교차는 유지하고, 모든 intermediate도 완전한
  `pelvis→hip→knee→ankle→heel/toe` 체인이다. 방향별 24장 모두 raster hash가 다르다.
- V2의 stable upper는 하체만 걷고 상체가 멈춘다는 사용자 판정으로 거부했다. V3는 방향별 여섯 상체
  key pose를 해당 하체 phase와 다시 결합해 반대 팔·다리 counter-swing을 복구했다. 상체와 골반의
  정수 1px sway·bob, pelvis 1.0→knee 0.55→ankle 0.2→foot 0.0 감쇠도 유지한다. 8방향 모두
  unique upper key raster 6/6을 gate로 검증한다.
- 신발이 바지에 묻힌다는 실제 화면 피드백 뒤 앞코·뒤꿈치·갑피 높이와 길이를 함께 확대했다. static gate는
  192/192 hard alpha, upper mismatch 0, minimum pants union 1205px, minimum shoe material 208px,
  minimum torso/pelvis junction 26px로 PASS했다.
- actual Windows Player D3D11/RTX 3080 Ti `StarterOfficeV1`에서 24 east pose 전부와 실제 좌석 경로
  467 moving frame을 소비했다. 남/남서/남동 이동, `seat_player`, Work0~5, foreground occlusion,
  seat/facing/visual-root 오차 0으로 QA V3가 PASS했다. 검토 GIF는
  `Artifacts/PlayerEastLockedArtOfficeSeatingQa/actual-office-all-directions-run8.gif`다.
- production 기본, 정상 새 게임, 가족/NPC, 현재 Downloads 배포 EXE는 계속 `Legacy48`이다. 이 V3는
  external QA catalog일 뿐이며 사용자 GIF 승인 전에는 Assets 승격·배포·커밋·push를 하지 않는다.
- 이전 6포즈 `CaptureAllDirectionsRun4`는 구조 QA만 통과했으나 사용자 화면 판정에서 똑딱거림으로
  거부됐고, 승인 근거로 사용하지 않는다.

## [퇴역 기록] 2026-08-20 / KShopGo·Mixamo 추적 east 6포즈 재제작

- 최종 외형은 빨간 캡 주인공의 2D 스프라이트다. **동작 정본은**
  당시 KShopGo Walk 분석과 다운로드된 Mixamo
  `X Bot@Unarmed Walk Forward.fbx`다.
- FBX는 Downloads와 `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/`에 모델·걷기 클립 모두
  존재한다. KShopGo Walk는 0.8초, 30fps, 24샘플이다. 새 6포즈는 0/4/8/12/16/20 샘플의
  접지→회수/체중이동→전방 통과/착지 직전→반대 접지 순서를 추적해야 한다.
- 사용자는 기존 원본의 다리 교차와 팔 자세는 승인했지만 v10 이후 후보의 신발 이중화, 접지면 뒤틀림,
  하체 역주행, 상체와 하체 진행 방향 불일치를 반복해서 거부했다. v10~v13은 production 후보가 아니다.
- 하체 좌우반전, 신발/종아리 조각 이동, ImageGen이 관절 타이밍을 발명하는 방식은 금지했다.
  ImageGen은 Mixamo 관절 가이드를 잠근 뒤 2D 외형을 정리할 때만 쓸 수 있다.
- 현재 출하 기본은 계속 `Legacy48`이며 승인된 `Player2DV2` 대체 보행은 없다. 작업 범위는 주인공 east
  6프레임뿐이다. 격리 후보는 Mixamo 대응 오버레이, 지지발 고정, swing 회수→교차→전방 착지,
  상·하체 동방향, static gate와 actual Windows D3D11 단독 주인공 타일 맵 gate를 통과했다. 사용자 타일 맵
  화면 승인 전에는 다른 방향/가족/기본 런타임이나 production Assets로 승격하지 않는다.
- production 이동은 speed `1.0`, acceleration `8.0`, cycle distance `0.99380799`와 `Legacy48`을 유지한다.
  사용자 승인으로 east 격리 후보만 speed `1.5`, cycle distance `1.49071199`를 적용해 `1.333 steps/tile`로
  비교한다. KShopGo의 1.2 unit stride를 복사한 값이 아니라 우리 tile-centre 거리의 정확한 1.5배다.
- east 격리 후보를 actual Windows Player D3D11의 정본 `StarterOfficeV1` 가구 배치에도 올려 확인했다.
  정상 새 게임이 먼저 `13×13`, editable furniture `0`, seat `0`의 빈 사무실임을 증명한 뒤 QA/migration fixture
  (layout hash `94AFD18317E198DA`, furniture `69`, editable `17`, seat `4`)만 설치했다. 주인공 단독으로
  `seat_player`까지 실제 navigation `467` moving frame을 이동해 `Working`에 진입했고 seat contact,
  animated anchor, facing, sprite direction, visual-root 오차가 모두 `0`으로 PASS했다. 제품 `Legacy48`, 전역
  speed/stride, 정상 새 게임, 현재 배포 EXE는 바꾸지 않았다.
- 현행 정본 착석은 별도 `SitDown`/`StandUp` 중간 clip을 쓰지 않는 classic atomic 경로다. 캐릭터가 의자 앞에
  발을 고정하고 좌석 방향으로 정렬한 뒤 즉시 `Work0`를 publish한다. 실제 사무실 QA도 이 계약 그대로
  `observedSitDownFrameCount=0`, `Work0~5` 전 프레임, chair foreground occlusion을 검증했다.
- Unity `PlayerWalkMotionReferenceExporter`는 다운로드된 Mixamo clip을 east `+90°`로 평가해
  `PLAYER_WALK_MOTION_REFERENCE: PASS`를 기록했다. clip 길이는 `1.3666668s`, left-contact phase zero는
  `0.2961111s`다.
- 현행 east 격리 2D target은 `(1.49071199/6)*180/1.55 = 28.852490px/pose`를 사용하며 계산된 heel/toe world
  contact drift 최대값은 `0.295020px`다. raw Unity export는 ignored `Artifacts`에 재생성하고,
  `ArtSources/PlayerEastMixamoTraceV2/`에는 파생 target, phase contract, guide와 재생성 source를 추적했다.
  이 PASS는 motion/foot-lock 계약에만 해당한다.
- V13 ImageGen whole sheet, 개별 P1/P2 ImageGen, LockedArtV2 raster warp는 모두 거부했다. 기존 lower donor로
  P2/P5를 만들면 다리 방향·연결이 기괴해진다. V3 phase별 상체만 보존하고 P0~P5 lower를 locked
  `pelvis→hip→knee→ankle→heel/toe` 체인으로 각각 새로 저작했다. 실제 맵에서 얇게 읽힌 source 폭은 관절
  좌표를 유지한 채 허벅지 약 `16.8px`, 무릎 `14.5px`, 발목 `10.6px`로 보강했다. 실제 맵에서 눌려 보인
  신발은 heel/toe를 유지한 채 긴 보폭 재저작 후 최소 색 높이 `8→11px`, 색 면적 `108→207px`, 빨강
  `32→67px`, 흰색 `45→88px`로 보강했다.
- 당시 집 PC 2D 재개 문서는 2026-08-24에 삭제했다.

## 2026-08-20 / KShopGo 기준 연속 이동 + Mixamo 8방향 후보 (폐기된 역사 수치)

> 아래 절의 speed `1.5`/stride `1.2`는 KShopGo world unit을 직접 복사하던 당시 기록이다. 현행 project
> 정본은 위 절의 speed `1.0`/stride `0.99380799`이며 아래 수치를 runtime에 다시 적용하지 않는다.

- APK 실물 재분석으로 KShopGo Walk가 `0.800s`, 30fps, 24샘플, 인플레이스이며 Idle↔Walk 전이가
  exit time 없는 고정 `0.25s`임을 확인했다. `ApplyRootMotion=True` 42/52라는 플래그와 달리 모든 이동
  클립 평균 root 속도는 0이고 시작·끝 XZ가 같다. feet stabilization/linear velocity blending/feet IK도
  모두 꺼져 있었다. 당시 2D 분석 문서는 2026-08-24에 삭제했다.
- 자유 보행은 이제 segment 시작, 코너, 급반전에서 정지 pivot을 기다리지 않는다. logical root는
  `DefaultMoveSpeed=1.5`, `DefaultAcceleration=8.0`으로 계속 적분하고 Sprite 방향은 같은 frame의 실제
  변위를 즉시 따른다. 한 cycle stride는 `1.5 × 0.8 = 1.2 world unit`, cadence는 2.5 steps/s다.
  `ShortShuffleStrideFraction=0`으로 짧은 입력의 0/3 두 프레임 스터터도 제거했다. 제자리 0.06초 pivot은
  막힘·좌석·업무의 최종 facing 정렬에만 남는다.
- `simulation-pure`와 Unity 6000.3.21f1 Bee Roslyn의 Simulation/Presentation.Unity/Editor 컴파일은
  PASS했다. 기존 정지 반전 Player QA와 전환 QA도 연속 반전/전체 gait 계약으로 갱신했다.
- 당시 Mixamo `canonical-protagonist-v1` 64장 후보를 재베이크 대상으로 기록했으나, 이후 실제 화면에서
  3D primitive 외형과 바운스를 확인해 거부했다. Unity 로그인/라이선스 문제는 현재 경로의 blocker가 아니다.
- 이 절은 아래 2026-08-19의 한 타일 한 주기·0.18초 고정발 회전 후보를 이동 정본으로 대체한다.

## 2026-08-19 / 주인공 자연 보행 V4 — 8단계 발 교대·0.18초 고정발 회전 후보

- 기존 네 자세의 한 bitmap을 이동거리 20~30% 동안 끌던 주인공 보행을 `PlayerNaturalWalkPresenter`가
  `접촉 A→발 떼기 A→통과 A→착지 B→접촉 B→발 떼기 B→통과 B→착지 A` 8단계로 재생한다. 각 자세는
  이동거리의 12.5%만 소유한다. phase는 실제 이동 누적 거리만 읽으므로
  한 타일=한 주기와 tile-center root 계약은 바뀌지 않는다.
- 스침 자세는 새 전신 생성물이 아니다. 각 source-exact contact의 머리·목·얼굴·모자·몸통·재킷·팔·손과
  양쪽 하체 픽셀을 그대로 쓴다. 하체를 중앙에서 좌우 두 덩어리로 나눠 동/서는 각 18px, 남/북은 각
  12px 안쪽으로 평행 이동할 뿐 축소·재표본화·보간하지 않는다. 발 떼기는 최대 이동/들기의 50% 이하,
  통과는 전체 이동과 12px(동/서) 또는 9px(남/북) 들기, 착지는 다음 contact의 50% 안쪽 이동을 쓴다.
  접지발과 이동발이 바뀌는 과정을 세 자세로 나누고 두 다리의 원래 굵기는 유지한다.
  `Tools/extract_player_natural_passes_v1.py`와 `PlayerNaturalWalkV1/source-receipt.json`이 입력 SHA, 경계,
  `legScale=1.0`과 발 들기 값을 고정한다.
- 코너에서는 주인공만 0.18초 동안 logical root를 정지시키고, 이전 방향→중간 cardinal→목표 방향의
  source-exact contact를 30/40/30%로 보여 준 뒤 다음 타일 translation을 시작한다. 좌석·작업·idle은 기존
  renderer 소유권을 유지한다.
- ImageGen 통과 자세 3회는 체크 배경, 무릎 과상승, 다리 겹침 또는 정지 자세 회귀로 모두 거부했고 게임
  자산에 넣지 않았다. legacy 전신 passing 혼합 후보는 체형 축소로, legacy 하체 corridor 합성 V1은 발을
  바꿔 디딜 때 다리가 한 덩어리처럼 얇아져 폐기했다. V2 wide-passing은 굵기는 해결했지만 두 발이 모두
  바닥에 붙어 교대 순간이 약해 V3로 대체했다. V3는 발 들기는 읽혔지만 한 스침 bitmap을 이동거리 30%
  동안 끌어 미끄러져 보여 V4 8단계로 대체했다.
- Unity 6000.3.21f1 Windows Player D3D11 실제 8구간 loop는 577 moving frame/69 capture,
  최대 center-segment 이탈 `0.00000053 world`, endpoint/visual-root/final-center 오차 0,
  collision projection/moving sprite violation/build warning 0으로 PASS했다. 사람 최종 화면 승인 대기이며
  승인 전에는 다른 가족으로 확대하지 않는다.

## 2026-08-19 / 주인공 실제 타일 보행 — 크기·접지 정규화 뒤 최종 화면 승인 대기

- 첫 실제 사무실 GIF는 경로 수치만 PASS했을 뿐 시각적으로 실패했다. source-exact contact를 기존과 같은
  PPU 180으로 가져와 legacy 전환 프레임보다 약 1.75배 커졌고, crop 아래 4px 투명 여백 때문에 발이 떠
  보였으며 코너에서 legacy 프레임으로 전환할 때 캐릭터가 작아졌다 다시 커졌다. 이 GIF와 수치를 승인
  근거로 사용하지 않는다.
- contact crop은 불투명 발바닥이 bottom-center pivot에 직접 닿도록 bottom padding을 0으로 바꿨다.
  동/서는 PPU 314, 남/북은 PPU 324로 방향별 불투명 신장을 기존 256px 전환 프레임과 맞췄다. Point,
  mipmap off, uncompressed 및 source-exact/generated pixel 0 계약은 유지한다.
- 수정한 Windows Player D3D11 실제 사무실에서 grid `(1,1)→(2,1)→(3,1)→(3,2)→(3,3)→(2,3)→
  (1,3)→(1,2)→(1,1)`의 인접 8구간을 다시 돌았다. 581 moving frame/68 capture에서 최대 center-segment
  이탈 `0.00000080 world`, endpoint/visual-root/final-center 오차 0, collision projection과 moving sprite
  violation 0, build warning 0으로 PASS했다. 청록 선은 이 QA 실행에만 있는 정확한 tile-center 경로 표시다.
- 수치 PASS는 사람 화면 승인을 대신하지 않는다. 수정 GIF의 크기·접지·코너 전환은 사용자 최종 화면
  승인 대기이며 승인 전에는 나머지 가족으로 확대하지 않는다.

## 2026-08-19 / 가족 보행 전면 정리 — Player source-exact 방향 후보 실행 검증

- 사용자가 기존 보행 생성 찌꺼기 전체 삭제를 승인했다. `FamilyWalkHalfCyclesV2`,
  `FamilyLocomotionRigV1`, `MotherSideWalkV3`, `MotherNorthWalkV2`,
  `CharacterLocomotionIdentityV1`과 연결된 생성기·검사기·구 계약 문서를 제거했다. 현재
  `HighMotion/Frames`와 이동/충돌/착석/방향 전환 런타임은 다른 가족과 non-walking fallback 때문에 유지한다.
- 자동 생성 rigid-part 후보도 D3D11 화면에서 팔·다리 비율이 원본과 달라져 거부하고 source,
  Resources, 코드, QA Player와 로그를 모두 삭제했다. 승인 layered PSB가 없을 때 자동 파츠 분리를
  production으로 승격하지 않는다.
- 최초 승인 범위는 주인공 동쪽 보행 하나였다. `Tools/extract_player_east_contacts_v1.py`가 정본
  `player_pixel_walk4x2_v1.png`의 east 열 두 칸을 생성/보간 없이 crop하고 bottom-center 정렬한다.
  런타임 `PlayerEastContactPresenter`는 기존 `GaitPhase01`의 0.0/0.5 반주기에 두 접촉 포즈를 배정한다.
  idle, 착석, 작업, 퇴장은 기존 `DirectionalSpriteAnimator`가 그대로 소유한다.
- source SHA-256은 `0C23A5D9594FFED9E8263938A11F6268F133B09ECDFFC90BAD4E2545179BC4EB`,
  현재 출력은 204×389 RGBA 두 장이며 receipt는
  `Assets/Resources/FamilyCompany/PlayerEastContactV1/source-receipt.json`이다.
- Unity `6000.3.21f1`에서 Editor D3D11 6 phase와 실제 Windows Player D3D11 6 phase가 PASS했다.
  Player 로그는 `graphics=Direct3D11`, `device=Intel(R) Graphics`, build warning 0이다.
- `com.unity.2d.animation 13.0.0`, `com.unity.2d.psdimporter 12.0.0`은 향후 사람이 승인한 layered PSB
  실험 기반으로 설치했다. 현재 두 접촉 기준선은 이 패키지에 의존하지 않는다.
- 2026-08-19 사용자 화면 승인을 받았다. Player east source-exact contact V1은 사람 게이트까지 통과한
  현재 정본이다. east 파일은 더 이상 자동 파츠/전신 생성으로
  덮어쓰지 않는다. 더 부드러운 rigid cutout이 필요하면 사람이 원본을 12~18 layer PSB로 나눈 뒤 별도
  후보로만 다시 연다.
- 다음 범위인 Player south도 같은 정본 시트의 south 열 두 칸을 새 픽셀·보간 없이 crop해 연결했다.
  현재 출력은 177×401 RGBA 두 장이고 receipt는
  `Assets/Resources/FamilyCompany/PlayerSouthContactV1/source-receipt.json`이다. Unity Editor D3D11 6 phase와
  실제 Windows Player D3D11 6 phase가 PASS했고 통합 Player build warning은 0이다. 같은 통합 Player에서
  east 6 phase를 재캡처해 승인 당시 PNG와 SHA-256 바이트 일치도 확인했다. 2026-08-19 사용자 화면
  승인을 받아 south도 현재 정본이다.
- 같은 4×2 source의 north/west 열도 각각 2장씩 source-exact로 게시했다. 현재 north는 173×396, west는
  204×389이며 generated/interpolated pixel은 0이다. source에 대각선 아트가 없으므로 주인공 이동 중
  southwest/northwest는 west, northeast/southeast는 east 접촉 포즈를 쓰는 표준 4방향 visual mapping을
  적용했다. Editor D3D11은 north/west 12 phase와 diagonal 24 phase, 실제 Player D3D11도 같은 36 phase를
  PASS했다. 최종 build warning은 0이며 east/south/north/west 회귀 24장은 직전 승인/후보와 SHA-256
  바이트 일치다. north/west 및 대각선 mapping은 사용자 최종 화면 승인 대기 상태다.
- 단독 presenter가 아니라 실제 새 게임 사무실의 `OfficeRuntimeAgent`에도 통합했다. 최초 PPU 180 실행은
  경로 수치와 무관하게 캐릭터 크기·접지·전환이 실패했으며, 위 정규화 재검증만 현재 후보 증거다.

## 2026-08-19 / 문서 정합성 감사 — 코드/실행본 대조 후 정정 (문서만 변경)

- 코드·실행본과 대조해 stale해진 문서 주장만 고쳤다. `Assets`, `Tools`, `ArtSources`는 건드리지 않았고
  당시 Codex가 작업하던 퇴역 2D locomotion 문서와
  `Tools/build_family_locomotion_rig_v1.py`도 제외했다.
- `b397af9`의 두 차단 회귀는 더 이상 현재 상태가 아니다. 배치 pending 좌클릭은
  `OfficeLayoutEditModeController.HandlePointer()`가 preview 갱신 뒤 같은 frame에 `ConfirmPreview()`를
  호출하고, 빈 사무실 산책은 `OfficeAutonomyCoordinator`의 destination 후보 경로를 쓴다. 이 사실을
  README, AGENTS, OFFICE_BUILD_EDITOR_V1, 이 문서의 검증 표와 backlog에 반영했다.
- 배포 경로가 문서와 달랐다. 실제 배포본은 `%USERPROFILE%\Downloads\Family`(`befe937e`)이고
  `Downloads\FamilyCompany_Playtest`는 존재하지 않는다. 저장소 build는 clean HEAD `8fa5fa74`다.
  PLAYTEST_BUILD, WINDOWS_AUTO_DEPLOY, HOME_PC_CONTINUATION_GUIDE, README를 실제 경로로 정정했다.
- **미해결(코드 쪽)**: `Tools/FamilyCompanyBuild.Common.ps1`의 `Get-FamilyCompanyDeployDefaults`는 아직
  `TargetPath = Downloads\FamilyCompany_Playtest`와 `RequiredBranch = codex/integration-p0-qa`를 기본값으로
  갖는다. 그 branch는 더 이상 없으므로 `DEPLOY_WINDOWS.cmd`를 인수 없이 실행하면 `WRONG_BRANCH`(35)로
  멈춘다. 지금은 문서에 우회 인수를 적어 두었고, 기본값 자체를 `main`/`Family`로 바꿀지는 사용자 결정
  사항으로 남긴다. `Tools/Test-FamilyCompanyDeployPipeline.ps1`도 같은 옛 값을 인수로 넘긴다.
- 삭제된 퇴역 2D naturalness 문서가 제안한 `Editor/OfficeNaturalnessQa.cs`는 만들어진 적이 없어 문서
  상단에 미구현 제안서 표시를 달았다. `Docs/ASSET_MANIFEST.md`가 참조하는 `remove_chroma_key.py`는 이
  저장소와 `simul` 어디에도 없다.
- 대조로 확인한 정확한 값(변경 없음): Unity `6000.3.21f1`, 시작 `2000-01-03 08:50`, 자본금 `5,000,000`,
  13×13/169셀, starter 가구 69, 저장 스키마 `v10`(v1~v9 이관), 출근 09:00~09:03·퇴근 18:00, 허브 5개
  (회사·인사·사업·연구·투자), 계약 T0~T4, 체력 10,000·25% 임계, 가족 4명 보행 PNG 192장
  (4×8방향×6프레임), CANON의 가족 4명 런타임 시트 경로.

## [퇴역 이력] 2026-08-18 / 가족 4명 foot-anchored 공용 리그

> commit `befe937`과 로컬 `6ae4041`의 기존 PASS는 무효다. 전자는 프레임 내부 머리/발 방향을,
> 후자는 실제 projected support foot을 검사하지 않았다. `split_high_motion_sheets.py`가 각 phase의 상체
> median X를 128로 독립 재센터링해 접지발의 root-relative 역이동을 지웠다. 필요한 값은 phase당
> 19.234993px인데 구 PNG는 1.011~6.471px만 움직였고, explicit support가 아닌 best-case 발 선택에서도
> 가족 32/32루프가 26.260~40.138px 미끄러졌다. `6ae4041`은 push·배포하지 않는다.

- 사용자 지시에 따라 출하 범위는 가족 4명×8방향×6위상=192 PNG뿐이다. 직원 후보 8명은 변경하지
  않았고 가족 4명 실제 Player 사람이 승인된 뒤 같은 rig/profile을 retarget한다.
- `Tools/generate_character_locomotion_v1.py`는 이제 `Tools/build_family_locomotion_rig_v1.py`를 호출한다.
  `ArtSources/FamilyLocomotionRigV1`의 SHA 고정 5방향 분리 leg parts와 결함 전 방향별 identity upper를
  사용한다. ImageGen은 파츠까지만 맡고 final foot coordinates/phase/contact는 공용 코드가 소유한다.
- canonical 방향은 south/north/east/southeast/northeast, west 계열 3방향은 frame 전체 mirror다. P0~P2는
  anatomical left support, P3~P5는 right support다. 출하와 alpha가 같은 cyan/magenta marker 사본으로
  두 발을 명시해 검사기가 프레임마다 다른 best-case 발을 고를 수 없다.
- garment seam corridor에서 기존 standing leg만 제거하고 generated leg를 같은 hip에 결합한 뒤 canonical
  upper를 마지막에 덮는다. 별도 pelvis cap과 상/하체 independent offset은 없다. P1/P4에는 upper와 hip을
  함께 1px 아래로 옮겨 얼굴/의상 픽셀을 변형하지 않으면서 frozen torso 느낌과 seam 찢어짐을 막는다.
- front/back은 fixed-length 평면 IK 대신 foreshortening을 사용한다. 엄마의 불투명 치마 아래 한 발이
  사라지는 것과 과도한 X자/팔자 다리를 함께 피하도록 depth-facing stance를 18px로 고정했다.
- 후보와 게시 runtime의 독립 foot-lock QA는 4명/8방향/32루프/192프레임 전부 PASS다. 최대 projected
  support drift `0.726448px`, alternating contact step `57.669070~57.794742px`, swing world travel 최소
  `86.316402px`, passing lift 최소 `3.234033px`, detached alpha `0px`다. 기존 `.meta`/GUID hash diff는 0건이다.
- 음성 회귀는 정지발을 `38.470px` support drift, 같은 지지발 반복을 `32.407px` contact-step error,
  lift 0을 air-phase failure, P0~P5 left를 ownership failure, 모자 절단을 alpha/identity failure로 거부한다.
- Unity 6000.3.21f1 숨김 batch는 실제 import된 `characters=4 directions=8 frames=192`, stride
  `0.99380800`, `rootStepPx=19.2350`, cadence `2.0125 steps/s`로 PASS했고 `FAST_QA_WINDOWS.cmd -Profile
  editor-broad`도 10.725초에 PASS했다. clean Release build는 `Builds/Windows/FamilyCompany_Playtest`에
  `commit=8fa5fa74`, `WorkingTreeDirty=False`로 존재한다. 남은 것은 배포본 D3D11 Player의 renderer
  support-world trace와 실제 캡처 사람 검토이며, 현재 배포본 `Downloads\Family`는 아직 `befe937e`다.
  이 둘이 PASS하고 배포본 `BUILD_INFO` SHA가 HEAD와 같기 전에는 완료/정상 릴리스가 아니다.
- 정확한 source SHA, 좌표계, phase, threshold, 실패 사례와 재현 명령은
  당시 퇴역 2D locomotion 계약이 소유했다. 해당 문서는 2026-08-24에 삭제했다.

## [퇴역 이력] 2026-08-18 / 가족 보행 identity-lock 재제작·실제 Player 승인 후보

아래 내용은 Generation V1 이전 제작 이력이다. 현재 shipping writer/gate를 설명하지 않는다.

- `FC-WALK-GUARDRAIL-V1`과 `FC-WALK-TWOSTEP-GATE-V1`에 따라 가족 4명×8방향×6프레임을
  identity-locked candidate 61로 교체했다. 방향마다 canonical portrait/body anchor를 고정하고 두 다리만
  결정론적으로 움직인다. 출하 source 192장에는 표식이 한 번도 닿지 않고, 좌/우 다리 표식은 동일 alpha의
  별도 `MarkerReviewV1` 192장에만 있다.
- `Verify-FamilyWalkTwoStep.ps1 -SelfTest`는 반사 행 승인·합성 한 발 반복 거부·전신 반전 치트 거부를
  통과했다. 추적 source는 해부학 표식 32/32와 two-step 32/32, runtime static 32/32, unit 10/10과
  source/runtime/sheet byte 일치를 통과했다. 필수 마지막 줄은
  `FAMILY_WALK_TWO_STEP_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 source=artsources rows=32`다.
- `build_family_walk_half_cycles_v2.py`는 구형 V4/V5/V6/V7/raw import 세대를 제거한 단일
  source→runtime writer다. `--write`는 source와 marker gate PASS 전에는 쓰지 않는다. candidate 62
  재생성은 gate FAIL로 폐기했고 추적 원화에 쓰지 않았다.
- production `StarterOfficeRuntimeBootstrap.CreateActor`는 `LocomotionTransitionsV1`의 다른 세대
  시작/정지/pivot 초상화를 더 이상 가족 actor에 구성하지 않는다. 빈 사무실 연속 경로는 승인 walk/idle만
  재생하고, 다음 cardinal segment 방향으로 planted pivot을 끝낸 뒤 타일 중심 translation을 시작한다.
- 실제 Release Player normal 빈 사무실 1× 08:50→09:50은 30fps 629-frame burst와 8fps 가족별 review
  sheet/GIF로 사람 확인했다. 네 가족의 identity, 짧은 보폭, 양발 교대, 방향 전환을 승인했고 런지·X자
  다리·크로마·잔상·초상화 교체를 찾지 못했다. 1×/2×/4×에서 합계 walk loop 132/134/124였고 모든
  direction mismatch, pivot 전 이동, 중복 pivot, non-cardinal segment, 충돌/겹침, 타일 중심 이탈,
  visual-root offset, transition sprite frame은 0이다.
- 실제 Windows native pointer가 빈 새 게임의 녹색 `(1,1)` 프리뷰 셀을 한 번 클릭했다. pointer commit과
  state mutation은 각각 1회, 자금 `5,000,000→4,986,250`, ledger `1→2`, inventory `0→1`, furniture
  `52→53`, editable `0→1`, grid hash `104C121BBA787A22→2D928B958610B1BF`이며 runtime hash도 같다.
  화분 semantic/render 중심의 타일 중심 오차는 `0.00000000`이다.
- Unity `6000.3.21f1` `editor-broad`와 `player-scripts`가 PASS했다. release 승격은 이 문서가 포함된 clean
  main commit을 다시 빌드하고 동일 파일을 Downloads에서 재검증한 뒤에만 완료한다.

## 2026-08-17 / 화면 진행방향 직접 양자화·V5 두발 보행

- 런타임 `world` 변위는 이미 등각 Tilemap 변환이 적용된 실제 화면 진행 벡터다. 이를 다시 grid 축으로
  역투영한 것이 오른쪽 위로 번역하면서 정면/옆모습을 재생한 최초 방향 오류였다.
- `DefaultWorldVectorToVisualFacingAxes`는 실제 변위를 그대로 반환하고 원화의 화면 heading을 직접
  양자화한다. 결과는 grid `+X→northeast`, `−X→southwest`, `+Y→northwest`, `−Y→southeast`다.
  따라서 pivot과 translation 프레임의 머리·허리·다리 방향이 화면 displacement와 동일하다.
- 월드 변위를 옥탄트로 바꾸던 나머지 소비자(`NormalNewGameSeatStallObserver`, `ScenePreviewJump`의
  reversal QA와 8방향 QA, `OfficeSeatDockingR5eTraceWriter`)도 같은 직접 화면축 어댑터를 거치게 했다.
  매우 짧은 진단 프레임은 예외를 던지지 않는 `ResolveDirectionFromAxes`를 쓴다.
- 검증기가 제품과 같은 변환을 공유해 거짓 PASS를 내지 않도록 기대값을 하드코딩에서 어댑터 독립
  불변조건으로 바꿨다. 2:1 스텝 벡터는 리터럴로 적고, 몸의 좌우 기울기가 진행 방향의 반대일 수 없다는
  조건과 8개 서로 다른 헤딩이 8개 서로 다른 원화 행에 도달해야 한다는 조건을 추가했다.
- 정지·회전·출발/정지 전이 프레임 256개 중 64칸이 셀 상단에서 잘려 정지 중인 가족의 머리 윗부분이
  평평하게 사라졌다. 원인은 생성 아트가 4×4 시트의 셀 경계를 넘겨 그려졌는데 분할이 256px 경계에서
  그대로 잘랐기 때문이다. 잘린 픽셀은 한 칸 위 셀에 그대로 남아 있었다.
- `Tools/Repair-LocomotionTransitionFrameHeads.ps1`이 vendored alpha 시트를 상향 overflow 밴드와 함께
  다시 분할하고, 최대 연결 실루엣만 남겨 위 칸의 발을 버린 뒤 기존 planted-foot 정렬을 적용한다. 43개
  프레임이 1~18행을 되찾았고 전체 256개에서 평평한 상단 행은 0이다. 복구 대상이 아닌 프레임은 실루엣이
  픽셀 단위로 동일하며(onlyOld=0, onlyNew=0), 기존 분할이 1:1 blit에서 남긴 최대 5/255 색 잔차만
  사라진다.
- Unity `6000.3.21f1`에서 `OFFICE_MOVEMENT_FACING_NAVIGATION_VALIDATION: PASS`(seeds=128, paths=1152,
  movingFrames=1970, reverseFacingFrames=0, movingDuringPivot=0, unnecessaryCornerStops=0), 실제 Release
  Player의 normal 빈 사무실 observer 1배속 08:50→09:50 `PASS`(4인 보행, directionMismatch=0,
  prePivotTranslation=0, duplicatePivot=0, currentLook=0, nonCardinal=0, collision=0), 확장 tile runtime
  QA `-familyCompanyTileRuntimeQa` exit 0(좌석·도킹·4방향·배치/회수·자율 시계·HUD·로딩 UI)을 통과했다.
- 미해결: 확장 QA 로그에 `OFFICE_EMPTY_WANDER_FAIL | actor=father|mother candidateCount=19 rejected=19`가
  반복된다. QA fixture 배치에서 두 액터의 산책 후보가 전부 거부되어 매 tick 재시도한다. normal 빈
  사무실 관측에서는 `coordinatorCandidateFailures=0`으로 재현되지 않았다. 사용자가 보고한 "누나가
  제자리에서 도는" 증상은 수정본 60분 관측에서 재현되지 않았고(누나 pivot episode 9회, 옥탄트 24회,
  A→B→A 반복 0회) 사용자 확인이 필요하다.
- 이 작업은 커밋하지 않았고 Downloads 배포도 하지 않았다. 실행본은
  `Builds/Windows/FamilyCompany_Playtest/FamilyCompany.exe`에만 있다.

## 2026-08-17 / 타이틀 BGM 1초 후 무음 — 오디오 listener 소유권 이전

- 프로젝트의 `AudioListener`는 `OfficeTileMigrationPreview.unity`의 Main Camera 하나뿐이었고 첫 씬
  `Prototype01.unity`에는 없다. `ScenePreviewJump.Start()`가 타이틀이 떠 있는 동안 그 씬을 additive로
  워밍하고 `ScenePreviewJump.cs:471`이 로드 직후 같은 listener를 끄므로, 실제 출력은 씬 활성화부터
  listener 비활성까지의 짧은 구간에만 존재했다. 타이틀 BGM이 약 1초 들리다 끊기고 그 뒤 BGM과 모든
  SFX가 세션 내내 무음이 된 원인이다.
- 회귀 조합이다. `6e9d32b`가 preview listener 비활성화를 도입했고 `47aefa9`가 타이틀 워밍을 `Start()`로
  옮기면서 짧은 가청 구간이 생겼다. `door_open`·`door_close` SFX QA는 `PlaySfx` 호출 횟수만 세므로
  listener가 꺼진 무음 상태를 검출하지 못했다.
- `GameAudioCoordinator`가 자기 `DontDestroyOnLoad` 오브젝트에 `AudioListener`를 소유한다. scene load
  콜백과 기존 0.2초 poll에서 자기 것 외의 활성 listener만 끄므로 다중 listener 경고 없이 항상 정확히
  하나가 유지된다. 모든 소리는 `spatialBlend=0`인 2D여서 listener 위치는 결과에 영향이 없고, 씬 파일과
  `ScenePreviewJump`는 변경하지 않았다.
- Unity를 기동하지 않는 Roslyn 경로 `Tools/Validate-ManagementUiV2.ps1`에서 `Presentation.Unity` 전체
  런타임 compile을 포함해 `MANAGEMENT_UI_V2_EXTERNAL_COMPILE`, `MANAGEMENT_UI_V2_EDITOR_VALIDATOR_COMPILE`,
  `MANAGEMENT_UI_V2_STATIC_STRUCTURE` PASS다. 신규 경고는 없다.
- 실제 Player 가청 검증은 하지 않았다. 같은 worktree에서 사무실 배치 작업이 진행 중이라 Unity와 build
  lock을 점유하지 않았다. 다음 명령은 사무실 작업이 끝난 뒤 `FAST_QA_WINDOWS.cmd -Profile player-scripts`를
  실행하고 Player에서 타이틀 BGM 지속과 문·발소리 SFX 가청을 확인하는 것이다.
- 이 변경은 아직 커밋하지 않았다. 같은 worktree에 다른 작업의 미커밋 변경이 함께 있으므로 커밋할 때
  `GameAudioCoordinator.cs`와 이 문서만 명시적으로 선택해야 한다.

## 2026-08-17 / 배치 클릭·빈 사무실 산책 수정 후보 — 실제 Downloads 클릭 승격 보류

- 기준 `b397af9400be6f958e2d57162bf35508863a7a58`의 실제 회귀는 그대로 재현했다. 배치 pending 분기는 녹색
  preview 셀을 갱신한 뒤 return해 같은 좌클릭의 `GetMouseButtonDown(0)`/`ConfirmPreview()`를 소비하지
  못했고, 빈 사무실 observer는 stationary direction transition 48 대 valid walk loop 13인데도 같은 셀
  `current-look`와 destinationless Idle을 검사하지 않아 거짓 PASS했다.
- 배치 pending 분기는 preview 갱신 뒤 실제 좌클릭을 같은 프레임에 정확히 한 번 confirm한다. 성공 로그는
  cash·ledger·inventory·furniture·editable count·grid hash의 전후 값과 semantic/render anchor 오차를 함께
  기록한다. FAST_QA와 내부 runtime desk add/remove는 통과했지만, 다른 전체화면 앱이 전면 입력을 점유하고
  있어 실제 Downloads의 녹색 preview 한 번 클릭 증거와 배포는 아직 보류한다.
- 생산 소유자 `OfficeAutonomyCoordinator`는 실제 새 게임이 빈 사무실일 때 네 가족별로 겹치지 않는 도달 가능
  타일 영역에서 현재 셀과 다른 목적지를 결정적으로 고른다. actor는 각 cardinal segment의 실제 방향으로
  planted pivot을 완료한 다음 translation하고, 이동 프레임은 실제 화면 변위 방향을 즉시 사용한다.
  사용자 녹화에서 보인 반복 점프는 6포즈 원화 위에 `VisualRoot` foot-plant 위치 보정을 다시 더해 표현 속도가
  한 보마다 0~1.5배로 출렁인 것이 원인이어서, 원화·좌석을 바꾸지 않고 중복 전신 보정만 제거했다.
- observer-only normal Player 08:50→09:50은 1×·2×·4× 모두 PASS다. 1×는 coordinator 선택 25·후보 실패 0,
  유효 walk loop 136 대 stationary direction transition 131이며 actor별 loop는 39/37/32/28이다. 2×는
  선택 24·실패 0·loop 134 대 transition 114, 4×는 선택 18·실패 0·loop 114 대 transition 87이다. 세 속도
  모두 current-look·destinationless·같은 셀·중복 pivot·pivot 전 이동·direction/displacement mismatch·
  non-cardinal 이동·충돌·겹침·중복 목적지·타일 중심 이탈·표현 root offset·20분 정지가 0이다.
- `FAST_QA_WINDOWS.cmd -Profile player-scripts`와 `editor-broad`, main navigation Player QA가 통과했다. 렌더링
  전체 타일 runtime QA도 빈 새 게임 확인 뒤 4인 착석, 4방향 교차, 책상 추가/제거, 8방향, 반전 pivot,
  충돌, save/load를 끝까지 실행해 exit 0 PASS했다. clean commit·Release build·Downloads 실제 클릭·동일 후보
  normal 재검증 전에는 배포하거나 `origin/main`에 push하지 않는다.

## 2026-08-17 / 오래된 작업트리·증거 정리

- 필수 Git 기준 작업트리와 `fc_agents/integration_p0`만 남기고 옛 작업트리 34개를 제거했다. clean 작업트리
  27개는 즉시 제거했고, 변경이 있던 7개는 `cleanup-salvage-20260817-*` stash로 복구 가능하게 보존한 뒤
  제거했다. 옛 `Library/Builds/Artifacts`, FastQA runs, 이전 작업방 work/outputs, interim 출력, last-known-good
  복사본을 정리해 약 85GB를 회수했다. 반복 빌드 속도를 위해 현재 `integration_p0/Library/Bee`만 유지한다.

## 2026-08-17 / 빈 타일 사무실·타일 중심 배치·가족 전체 보행 V2 구현 상태

- 실제 새 게임은 13×13 외곽 구조물 52개와 비어 있는 11×11 실내 타일만 생성한다. 좌석·업무 가구·구매
  inventory는 모두 0이며, 기존 4인 가구 배치는 save migration과 출근/좌석 QA 전용 fixture로 분리했다. 빈
  사무실의 가족은 09:00에 actor별 결정적 open-area 타일로 출근하므로 책상이 없다는 이유로 문 밖에서 멈추지
  않는다. `FAST_QA`는 빈 실제 상태를 먼저 단언한 뒤에만 furnished fixture를 설치한다.
- 회사 허브의 기존 `건축·편집` 진입은 `사무실 관리`로 명확히 이름을 바꿨다. 업무·좌석·기기·수납·음료 등
  기존 카테고리별 구매/회전/보관/판매/배치 흐름은 유지하며, 배치 origin은 항상 정수 타일이다. 1×1 가구는
  그 타일의 정중앙, 다중 타일 가구는 점유 footprint 전체의 정중앙만 semantic/render anchor로 허용한다.
- 실제 cardinal cell-centre path가 사용하는 SW/SE/NE/NW 4개 화면 행을 V5 두발 주기로 교체했다.
  순서는 A발 착지·하중, B발 낮은 통과·착지·하중, A발 낮은 통과이며 0↔3 착지발과 2↔5 통과발이 모두
  반대다. 전신 높이와 바닥선 y=247은 6프레임 내내 고정되고 신발 아래 8px가 투명하므로 발이 잘리거나
  바닥에 묻히지 않는다. V5 96프레임은 raw key sheet에서 byte-exact 재생성되며 나머지 진단 행과 합친
  런타임 192프레임·8개 sheet도 `ArtSources/FamilyWalkHalfCyclesV2/`에서 결정적으로 재생성된다.
- warm rebind와 cold 준비 모두 실제 `NavigationPrewarmProgress`를 따른다. 표시 진행률은 raw 값으로 우회하지
  않고 30fps 한 프레임당 최대 0.0127만 전진하며, runtime `IsReady` 전에는 로딩을 닫지 않는다. 준비가 30초
  동안 막히면 원인 경고를 남기고 빠져나오는 fail-open 계약은 유지한다.
- Editor/static에서 empty layout, canonical footprint-center, 보행 V2 32/32 행은 통과했지만 이것은 실제
  구매 클릭과 빈 사무실 자율 산책의 정상 증거가 아니다. 위 차단 회귀가 해소된 동일 clean HEAD의 Release
  Player를 다시 검증한 뒤에만 Downloads와 `origin/main` 승격을 논의한다.

## 2026-08-17 / 엄마 북쪽 보행 V2 검증 완료·배포 후보

- 기존 엄마 북쪽 6프레임은 0/3 상체 실루엣 차이 5.6%, 발 영역 중심 `128.17→128.11px`로 팔·치마가 거의
  고정되어 있었다. 파일 수와 기존 coherence gate는 통과했지만 사용자 화면에서는 발을 끄는 동작으로 읽혔다.
- ImageGen과 명시적 포즈 가이드로 반 주기 0·1·2를 제작하고 3·4·5를 픽셀 정확 좌우 반전했다. 지지발은
  `R,R,L,L,L,R`, 바닥선 y=247이며 0/3 변화는 상체 30.1%·치마 29.2%·발 78.2%다.
- 생성 원본은 `ArtSources/MotherNorthWalkV2/`, 재현 도구는 `Tools/build_mother_north_walk_v2.py`, 의미 회귀는
  `Tools/test_mother_north_walk_v2.py`다. 전용 5/5, generic animation 9/9, 전체 walk 96/96, 엄마 sheet/frame
  48/48 일치를 통과했다.
- Unity `6000.3.21f1` clean Release Player가 정북 실제 이동에서
  `mother_north_walk_0..5` 여섯 imported sprite를 모두 정확한 이름으로 렌더했다. 0/3은 지지발·반대 팔이
  좌우 반전되고, 1/4는 회수, 2/5는 반대 통과 포즈이며 치마 밑단 변화와 발 하단 잘림 0을 실제 D3D12
  768px closeup으로 확인했다. QA는 프레임/스프라이트를 강제하지 않고 기존 직접이동·충돌·거리 기반 gait를
  사용하며 command-line opt-in 밖에서는 NPC 제어에 영향이 없다.
- 당시 furnished 기본 레이아웃의 clean Release observer-only 일반 새 게임은 1x·2x·4x 모두
  08:50→09:50 PASS였다. 이 역사 결과는 이후 도입한 빈 새 게임의 자율 산책이나 실제 구매 클릭을 검증하지
  않으므로 현재 회귀의 정상 근거로 사용하지 않는다.
  `actorQaControl=false`, route injection=false, clock jump=false, docking force=false이며 네 가족 모두 seat
  arrival 1회·Work 6/6·20 game-minute stall 0이다. 커밋된 clean HEAD의 `FAST_QA player-scripts`도
  37.966초로 SLO 60초를 충족했다.
- runtime/art는 `5e3cf8efd54cda4223af51bfd682b7690ba3e34f`, 실제 Player 검증기는
  `104547967959b1728973cd9bf53a910fb39cb553`에 기록했다. warm Library를 같은 worktree에서 보존해 최종
  clean Release는 약 26초, 반복 `player-scripts`는 약 25~38초 범위다. Downloads 승격과 `main` push는
  이 문서를 포함한 clean HEAD의 최종 동일성 검증을 통과한 뒤에만 실행한다.

## 2026-08-16 / 일반 새 게임 좌석 정지·타일 보행 수정·배포 완료

- Downloads의 `8b9e3313928545f98b4fc60427da76901271fc96` 배포본은 일반 새 게임 09:07의 첫
  `FinishingWork`에서 플레이어 컨트롤러가 출근 좌석을 해제해 캐릭터가 의자 앞에 서는 회귀를 재현했다.
  기존 seating `FAST_QA`는 `BeginQaControl`과 `QaBeginSeatedWork`로 일반 일정·이동·좌석 handoff를
  우회했으므로 이 회귀의 정상 실게임 증거가 아니었다.
- 일반 경로는 수동 E 업무로 얻은 좌석만 플레이어 컨트롤러가 해제하고, 출근 atomic seat→Work 6프레임
  handoff 동안 autonomy가 좌석 업무를 선점하지 않게 했다. 고주사율의 작은 실제 변위도 이동으로 인정하도록
  visual displacement 임계값만 `1e-10`으로 낮췄으며, path budget·도착 허용치는 바꾸지 않았다.
- 가족 보행 방향은 이미 투영된 실제 `world` displacement를 그대로 화면 heading으로 사용한다. 화면 오른쪽
  위 이동은 Northeast, 왼쪽 아래는 Southwest 원화를 사용한다. 생산 경로와 legacy pathfinder는 대각선·
  corner easing·중간 지름길을 제거하고 모든 cell center를 순서대로 지나는 4방향 cardinal 경로로 통일했다.
- 기본 이동 속도는 `1.00 world unit/s`, 6프레임 2보 주기는 실제 투영 타일 한 칸과 같은
  `0.99380799 world unit`이다. 한 타일에 정확히 한 주기·두 발걸음이므로 약 `2.01 steps/s`이며, translation은
  선형으로 유지하고 누적 실제 이동거리만 보행 위상을 전진시킨다. 상하 root offset은 사용하지 않고 V5 원화의
  두 착지발·두 통과발만 움직이므로 방방 뛰는 이동을 만들지 않는다.
- 게임 시간 배속만 빨라지고 캐릭터는 1x로 움직이던 원인은 actor motion이 `unscaledDeltaTime`을 사용한 것이었다.
  runtime actor는 이제 `Time.deltaTime`을 사용하며 1x·2x·4x 모두 같은 game-time 이동 거리를 유지한다.
  프레임당 visible motion은 `0.08s` 조각으로 제한해 빠른 배속에서도 충돌·점유 동기화를 보존한다.
- Unity `6000.3.21f1` 정적 이동 검증은 128 seeds / 1,152 paths / 1,970 moving frames를 통과했고
  reverse-facing, moving-during-pivot, unnecessary-corner-stop은 모두 0이다. 실제 Windows Player의 observer-only
  일반 새 게임은 route injection·clock jump·docking force 없이 1x·2x·4x 각각 08:50→09:50을 통과했다.
  네 가족 모두 assigned seat arrival 1회와 Work 6/6을 기록했고 20 game-minute stall은 0이다. 1x에서 누나
  09:08, 플레이어 09:10, 아빠 09:22, 엄마 09:24에 atomic seat에 도착했고 모두 다음 1 game-minute 안에
  6프레임 Work loop를 완료했다. 같은 후보의 seating transition FAST_QA도 4인 atomic seat/Work 6/6,
  primary 28/28, safe egress 4/4, penetration 0으로 통과했다. 가족 프레임 복원 뒤 animation asset strict
  gate는 12명·96 walk loops·576 frames 전부 PASS다.
- runtime 수정은 `44702af0d40f255bb598d62c3b09a1e3cd25d752`에 커밋했다. Unity `6000.3.21f1`
  Release candidate와 Downloads 승격본을 각각 별도로 실행해 일반 새 게임 1x·2x·4x를 모두 재검증했고,
  observer-only·route injection false·clock jump false·docking force false 상태에서 네 가족이 모두 seat arrival
  1회·Work 6/6·20 game-minute stall 0을 기록했다. `FAST_QA player-scripts`는 44.647초로 SLO 60초를 지켰다.
  최종 release identity는 Downloads의 `BUILD_INFO.txt`/`DEPLOY_MANIFEST.json`을 정본으로 하며 검증된 clean
  HEAD만 `origin/main`과 `C:\Users\godho\Downloads\FamilyCompany_Playtest`에 함께 승격한다.

## 2026-08-16 / 90도 코너 정지 제거

- `RequiresStationaryPivot` 임계값을 2옥탄트에서 3옥탄트로 올렸다. cell-centre 4방향 routing에서 일반 코너는
  항상 정확히 2옥탄트라 기존 임계값이 모든 코너에서 액터를 정지시키고 45도씩 두 번 회전시킨 뒤 재출발시켰다.
  이제 90도 코너는 걷는 속도를 유지하고 135도 이상만 발을 심고 회전한다. 근거는
  [DECISIONS.md](DECISIONS.md)의 같은 날짜 항목에 있다.
- `editor-broad` PASS 18.561초, `player-scripts` PASS 23.565초(SLO 60초 충족). 기존 안전 지표를 완화하지
  않았다: seeds=128, paths=1152, movingFrames=1970, reverseFacingFrames=0, movingDuringPivot=0,
  maxFacingError=29.2740도, unnecessaryCornerStops=0.
- 이동 중 표시 방향은 여전히 한 프레임에 스냅한다. 옥탄트당 고정 시간 blending은 런타임 불변식과 QA 단언
  7곳, 그리고 `ReverseFacingFrames == 0` 같은 집계 지표를 함께 바꿔야 해서 적용하지 않았다. 미완료 항목이다.

## 2026-08-16 / 반복 개발 루프 정본화

- [ITERATION_LOOP.md](ITERATION_LOOP.md)를 짧은 반복 루프의 정본으로 추가했다. 변경 종류별 명령, warm 캐시
  보존 규칙, `BUILD_WINDOWS.cmd`를 반복 확인에 쓰지 않는 경계를 정한다. 릴리스 절차는 그대로
  [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)와 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)가 정본이다.
- 실측 근거: `build-20260815-035423-unity.log`의 자동화 전체 133.4초 중 103.707초가
  `Asset Pipeline Refresh ... InitialRefreshV2(ForceSynchronousImport)`였고 같은 로그 239행에
  `Require frontend run. Library/Bee/1900b0aE.dag couldn't be loaded`가 남았다. warm `Library/Bee`의 normal
  incremental player build는 6.93/6.94/7.00초, forced clean release-config build도 16.00/19.58/19.44초다.
  느린 원인은 빌드 옵션이 아니라 worktree마다 warm `Library`를 버리는 것이다.
- 이 진단 당시 Family Company worktree는 36개였고 13개가 각자 `Library`를 가졌다. 2026-08-17 정리에서
  옛 작업트리 34개와 누적 산출물을 제거했으며, 현재는 필수 Git 기준 작업트리와 warm
  `fc_agents/integration_p0` 두 개만 남는다.
- 문서와 도구가 어긋난 지점을 기록한다. [AGENTS.md](../AGENTS.md)는 정본 브랜치를 `main` 하나로 규정하지만
  `Tools/FamilyCompanyBuild.Common.ps1`의 `Get-FamilyCompanyDeployDefaults`는 `RequiredBranch = 'codex/integration-p0-qa'`를
  요구한다. 로컬 정본 체크아웃에는 `main` 브랜치가 없고 `agent/contract-lifecycle-v0-3`에 머물러 있다.
  둘 중 어느 쪽을 정본으로 삼을지 정하기 전에는 배포 자동화를 `main`에서 그대로 실행할 수 없다.
- 이번 작업은 문서와 계약만 변경했다. Unity, Player, build/deploy는 실행하지 않았다.

## 2026-08-16 / R18 최종 arrival 통합 및 배포 완료

- 최종 arrival descendant `ce9e3ae4d94a7365c0447103d2ad904013ef58a1`를 payload guard·UI·tooling이 포함된 clean integration `d2fa777373e8f0376a5aca4899fdfe0c0fecd43a`에 한 번만 통합했다. merge-base는 `45f22430168cf3b3def1f50b147583a0cc3eb624`, 누적 R18 변경은 `OfficeRuntimeAgent.cs`와 `OfficeSeatingTransitionPlayerQa.cs` 2개이며 overlap/conflict는 0이다.
- R18은 독립 static PASS와 Unity `6000.3.21f1` capture-free Windows Player exit 0 PASS를 받았다. 네 actor 모두 Work 0..5를 관측했고, 같은 좌석의 atomic seat/root/pelvis 정렬과 microslide는 0이며 exit·turn·first-walk·endpoint stationary·safe-egress·furniture drift/penetration도 0이다.
- 통합 tree에서 Roslyn Simulation/Save/Infrastructure/Presentation/Editor compile, production fixture 158 scenarios·15 controls, negative oracle, offline/simulation, Management UI full compile, payload guard 14 fixtures와 tracked-tree 0위반을 다시 통과했다. R18 first-parent 범위 밖 UI·guard·tooling blob은 그대로다.
- 과거·회귀 Windows 실행 payload는 repo 밖 text/hash evidence를 먼저 보존하고 허용된 payload root에서 Recycle Bin 우선 방식으로 제거했으며, 이전 GitHub 감사의 history·tags·Releases·Actions executable payload는 모두 0이다. `da5c6e7f9f9d48f0eada245cff727435536c91dd`의 fail-closed CI guard가 `git add -f`와 이름·확장자 변경 Player bundle의 재유입을 차단한다.
- 최종 Windows build와 Downloads 승격은 2026-08-16에 수행되었다. 배포본
  `C:\Users\godho\Downloads\FamilyCompany_Playtest`의 `BUILD_INFO.txt`는 `commit=8b9e3313928545f98b4fc60427da76901271fc96`,
  `unity=6000.3.21f1 c02631ffc030`, `configuration=Windows Release`,
  `qa=FAMILY_COMPANY_SEATING_TRANSITION_QA PASS exit0`을 기록하며 이 SHA는 `origin/main` HEAD와 일치한다.
  `DEPLOY_MANIFEST.tsv`/`.sha256`도 같은 폴더에 있다.
- 이 배포본의 `BUILD_INFO.txt`가 증명하는 QA는 seating transition QA 하나다.
  [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)가 요구하는 네 가족
  `player` 09:00 / `older_sister` 09:01 / `father` 09:02 / `mother` 09:03 입장·착석 독립 oracle의 통과 여부는
  배포본 옆 evidence에 기록되어 있지 않다. 다음 배포 전에 이 oracle 결과를 `BUILD_INFO.txt` 또는 같은
  폴더의 비실행 evidence로 남겨야 한다.

## 2026-08-16 / Git tracked Windows Player payload 예방 guard 후보

- `.gitignore`가 canonical/nested `Builds/Windows`, known `FamilyCompany`/legacy `Company` Player 이름과 Playtest archive의 일반 accidental add를 막는다. 이는 보안 경계가 아니며 `git add -f`는 아래 CI가 별도로 검사한다.
- `Tools/Verify-NoTrackedPlayerPayload.ps1`은 NUL 종료 `git ls-files -s -z` index와 `git cat-file --batch` blob bytes를 대소문자 무시 경로 검사와 함께 사용한다. 알려진 배포 루트·EXE·Data·archive뿐 아니라 실제 Unity Windows Player PE, `UnityPlayer.dll`, CrashHandler identity와 실제 `*_Data` serialized/boot/Managed topology를 이름·확장자와 무관하게 차단한다. 일반 Unity package/plugin/source/art/font/audio, 임의 DLL·`*_Data`, DGGL은 차단하지 않는다.
- archive는 확장자가 아니라 ZIP/7z/RAR magic을 우선 식별한다. ZIP은 .NET으로, 7z/RAR은 7z 또는 bsdtar로 member bytes까지 검사하며, 확장자를 `.bin`으로 바꾼 Player bundle도 차단한다. archive를 검사할 수 없거나 512 MiB inspection limit을 넘으면 fail-closed한다. 전용 GitHub Actions workflow는 PR, push, 수동 실행에서 self-test와 현재 tracked tree 검사를 모두 수행한다.
- temp Git repo self-test 14건은 모든 target을 `git add -f`하고 각 baseline PASS→target 단독 oracle→index restore PASS를 검증한다. faithful PE/serialized/archive bytes로 standalone Player EXE·DLL·CrashHandler·Data, conventional/surface-renamed unpacked bundle, ZIP/7z와 `.bin` rename, malformed RAR, 공백·한글·대소문자·특수 선행 경로를 다루며 legitimate plugin/DGGL/source/art/font/audio/source archive 오탐은 0이다. 실제 Unity 6000.3.21f1 template 3종과 실제 `globalgamemanagers` identity probe, 현재 tracked tree 6,592개 0위반, workflow negative exit 1 전달도 통과했다. Unity, Player, build/deploy는 실행하지 않았다.
- 이전 remote audit에서 origin/main/history/tags/Releases/Actions executable payload가 모두 0이었으므로 history rewrite/force-push는 계속 불필요하며 이 후보도 remote ref/release/history를 수정하지 않는다. 서버 차단을 완결하려면 merge 후 repository ruleset에서 `No tracked Windows Player payload / Verify tracked Player payload is absent`를 required check로 지정해야 한다.

## 2026-08-16 / 회귀·실패 실행본 영구 삭제 정책

- [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)를 build/deploy의 영구 fail-closed 정본으로 추가했다. user-visible regression, failed gate, stale/unverified provenance, self-PASS-only candidate는 current 또는 Downloads에 존재할 수 없다.
- 판정 시 exact-root fence와 unrelated build 보호를 먼저 확인하고, SHA/log/manifest 같은 비실행 evidence를 payload 밖에 보존한 뒤 EXE, `*_Data`, `UnityPlayer.dll`을 포함한 전체 실행 payload를 즉시 삭제한다. 이름 변경·격리·LKG suffix만으로 회귀 payload를 보존하지 않는다.
- 회귀 payload의 재승격은 금지한다. 수정 뒤 모든 관련 regression oracle, 기존 필수 gate, 독립 gate를 통과하고 새 commit/input fingerprint/build ID를 가진 새 payload만 처음부터 빌드할 수 있다.
- Windows release의 독립 필수 oracle은 fresh 08:50에서 네 가족이 `player` 09:00, `older_sister` 09:01, `father` 09:02, `mother` 09:03에 실제 입장·이동·assigned seat 착석을 증명해야 한다.
- 이번 변경은 문서 계약만 추가했다. 기존 build/deploy 자동화가 regression 발견 시 evidence→전체 payload 삭제→검증된 정상 build rollback/없으면 empty를 실제 구현하고 독립 테스트로 증명하기 전에는 current/Downloads 실제 배포에 사용하지 않는다.
- 최종 push 전 remote zero-inventory gate는 `.gitignore`와 local tracked tree, `origin/main` current tree, 모든 active branch/tag tree, draft/prerelease 포함 release asset을 검사해 회귀·구 executable payload와 unknown identity가 각각 0임을 manifest로 증명해야 한다. tracked build는 exact 일반 cleanup commit으로 제거하며 `.gitignore`만 추가하거나 `git rm --cached`만 하는 것으로 끝내지 않는다.
- 과거 object까지 제거하는 history rewrite/force-push는 일반 payload 삭제가 아니다. exact object/ref/release reachability audit, 검증된 offline backup/restore, collaborator clone·worktree·CI 영향과 re-clone 계획에 대한 별도 승인이 있기 전에는 수행하지 않는다. 이번 문서 follow-up은 remote fetch/삭제/push나 rewrite를 수행하지 않는다.

## 2026-08-15 / Management UI validator Windows argv P2 후보

- `Validate-ManagementUiV2.ps1`의 full-runtime Roslyn compile은 source 파일 234개를 Windows argv로 직접 펼치지 않고, compiler identity·reference/options·source-root/발견 순서를 보존한 UTF-8 no-BOM response file을 사용한다.
- response와 validator output은 각각 GUID 기반 시스템 temp fence를 사용하며, path escaping, missing input, compiler failure/launch exception, stale response, 동시 실행에서도 자기 temp만 `finally` 정리한다. Assets·UI runtime/layout·font·art는 변경하지 않았다.
- 실제 입력 파일은 response serialization 전에 절대 경로로 정규화한다. 정규화 뒤에도 첫 문자가 `@`인 raw argument는 Roslyn nested response directive이므로 temp 생성·compiler 실행 전에 결정론적으로 거부한다. 실제 Roslyn의 quoted `@...` CS2011 재현과 상대 `@` 파일의 안전한 절대 경로화 compile을 fixture로 고정했다.
- 수정 전 89,515자 direct argv는 Win32 error 206으로 재현됐고 같은 425개 argument의 91,434-byte response compile은 통과했다. exact-base contract, 공백·한글·`@` path component·quote/backslash, raw leading-`@` fail-close, deterministic bytes, deliberate CS1513, missing compiler/reference, launch exception, stale response, concurrent 2-run, cleanup을 포함한 15개 외부 fixture가 통과했다.
- non-Unity layout/contrast harness, full runtime compile, editor-validator compile, static structure를 통과했다. `ManagementUiV2Validation.Run` 안의 AssetDatabase/font rasterization/GUID/meta/Sprite header 검사는 이번 작업의 Unity 실행 금지 때문에 실행하지 않았으며, 향후 승인된 단일 Unity slot에서 별도 실행해야 한다.

## 2026-08-15 / clean integration HEAD Windows 자동 배포 준비

- 기존 `BUILD_WINDOWS.cmd` → `Build-FamilyCompanyWindows.ps1` → `WindowsPlayerBuild.BuildWindowsX64`의
  비-Development Windows x64 경로를 재사용하고, clean `codex/integration-p0-qa` HEAD 변경만 debounce해
  Downloads로 승격하는 명시적 start/stop watcher를 추가했다. 서비스·재부팅 자동 시작은 등록하지 않는다.
- 실제 `Unity.exe` ProductVersion과 project revision을 `6000.3.21f1_c02631ffc030`으로 함께 검사한다.
  machine-wide build lock을 소유한 뒤 다른 작업방 Unity/QA player가 유휴가 될 때까지 기다리며 강제 종료하지 않는다.
- candidate의 EXE/Data/UnityPlayer.dll/BUILD_INFO/deploy manifest/runner를 먼저 검증한다. Downloads target은
  같은 볼륨 rename으로만 교체하고 기존 실행본은 timestamp와 이전 commit이 붙은 LKG 한 개로 남긴다.
  실행 중 player는 종료하지 않고 `AwaitingPlayerExit` candidate를 재사용한다. AppData 저장 데이터는 범위 밖이다.
- 격리 dry-run 15건은 공백·한글, dirty exit 31, conflict exit 32, unchanged skip, debounce, duplicate watcher
  exit 24, CMD exit code, 불완전 candidate, 승격 전·후 실패 복구, LKG, 실행 중 EXE 감지를 통과했다.
  `downloadsTouched=false`, `unityLaunched=false`였다. 실제 최종 player build와 Downloads 배포는 그 뒤
  2026-08-16에 `8b9e3313`으로 수행되었다.
- 후속 애니메이션·이동 hitch 커밋을 통합하고 최종 QA를 통과한 clean HEAD에서
  `START_WINDOWS_DEPLOY_WATCH.cmd` 또는 `DEPLOY_WINDOWS.cmd`를 명시적으로 실행한다.

## 2026-08-15 / P0 의자·이동·성능 1단계 통합 후보

- 기준 `9109a8c1`에서 전용 `codex/integration-p0-qa`를 만들고 의자 `0c6e8983`, 이동 `b692d24a`,
  성능 `9ea1312e`, 그 직계 문서 자식 `8ee72cd4`를 이 순서로 중복 없이 통합했다. 원본 브랜치와 `main`은
  변경하지 않았다.
- `OfficeRuntimeOccupancy` 충돌은 출근 ingress 상태, canonical 4×4 회전 ground mask, 미등록 콘텐츠의
  full-cell fallback, cached continuous-grid transform/AABB broad-phase, layout `Revision`을 모두 보존했다.
  actor 이동·reservation은 revision을 바꾸지 않는다.
- `OfficeRuntimeWorkstationService`에서는 의자 `VisualRoot`를 움직이는 옛 정렬 API를 되살리지 않고 명시적
  `OfficeSeatInteractionAnchors`만 유지했다. 자유 보행은 실제 변위 기반 8방향/`flipX=false`, 착석은
  LeavingSeat safe anchor까지 좌석 claim·facing·depth를 유지한다.
- Unity `6000.3.21f1` 격리 Editor QA는 chair foreground, seat egress 64건, seat occlusion 8방향,
  movement 128 seeds/1,152 paths/1,970 moving frames, path cache revision/invalidation, furniture collision
  10,368건/52 profiles, hybrid depth 120 permutations와 warmed 100회 allocation 0B를 모두 통과했다.
  채택 로그의 동시 루트 Unity는 각 1개, 다른 작업방 루트는 0개였고 종료 뒤 integration 소유 프로세스는 0개다.
- 후속 타이핑·마우스·물마시기 애니메이션 커밋은 아직 포함하지 않았다. 최종 Windows build와 D3D11 결합
  플레이 검증은 그 커밋들을 추가 통합한 뒤 수행한다.

## 2026-08-15 / 의자·좌석 상태 안정화 후보 (`codex/chair-seat-stability`)

- 착석은 의자 Transform을 쓰지 않는다. 가구 semantic root와 `VisualRoot`는 배치 때 기록한 parent,
  local/world position·rotation·scale을 유지하며 런타임 불변식 감시가 무단 변경을 검출·복구한다.
- 좌석은 approach, alignment, pelvis, hand, egress anchor를 명시적으로 제공하고 예약 claim을 통해 한 좌석의
  동시 점유를 거부한다. 진입은 Approach→Align→Rotate→Sit, 퇴장은 Finish→Stand→Leave로 분리한다.
- Windows D3D11 4명 동시 검증에서 furniture world position `0.000000px`, rotation `0.000000°`, scale
  `0.000000000`, character logical root `0.000000px`, pelvis-seat `0.000px`, seat-cell mismatch `0`, chair step
  `0.000px`, foreground penetration `0`을 기록했다. `(2,4)/(2,3)` 워크스테이션을 `(4,4)/(4,3)`으로 옮긴
  뒤 anchor와 occupancy 재구축도 통과했다.
- 기존 Northwest 타이핑 아트의 hand-keyboard 간격은 player `91.534px`, older_sister `78.188px`, father
  `79.401px`, mother `64.989px`로 3.5px 계약을 통과하지 못한다. 좌석 안정화와 분리된 필수 후속이며 현재
  전환 QA는 이를 `KNOWN_FAIL`/exit 97로 보존한다.
## 2026-08-15 / 실제 변위 방향·가구 geometry·출근 ingress

- 자유 보행의 최종 표현 방향은 실제 frame displacement의 8방향 양자화 결과다. 경계 인접 방향에만
  4°/0.075초 hysteresis를 허용하며, 좌우 cardinal 이동과 두 octant 이상 전환은 즉시 반영한다.
- `DirectionalSpriteAnimator`가 실제 변위·속도, motion/display direction, phase/clip, 최종 Sprite 이름과
  `flipX`를 같은 frame trace로 공개한다. 8방향 원화는 mirroring하지 않으며 최종 소비 단계에서 `flipX=false`를 강제한다.
- `OfficeRuntimeOccupancy`는 모든 알려진 편집 가구의 `OfficeFurnitureGeometryQuery.Shared` 4방향 ground
  mask를 path와 collision에 직접 사용한다. 정본 query에 없는 과거/미등록 저장 콘텐츠는 부분 legacy mask를
  재사용하지 않고 전체 셀 차단으로 fail-closed한다.
- 출근자는 첫 live route segment의 문 밖 2.5배 지점에서 나타나 단일 ingress gate를 예약한 뒤 입장한다.
  4인이 동시에 요청해도 한 명씩 진입하며, 기존 workstation seat claim/socket 소유권은 변경하지 않는다.
## 2026-08-15 / 플레이 중 내비게이션 정지 제거 (통합 대기 브랜치)

- `codex/perf-lag-rootcause`는 `OfficeRuntimeOccupancy.Revision`에 종속된 정적 이동 그래프를
  `(permittedSeatId, agentRadius)`별로 소유한다. 이 revision은 가구 배치·회전·회수처럼 정적 layout을
  다시 구성할 때만 바뀌며 actor 이동·reservation 같은 동적 점유는 캐시를 폐기하지 않는다.
- Starter Office의 빈 seat 권한과 가족 seat 4개, 169셀/키, 4방향 간선은 플레이 진입 전 Loading에서
  coroutine으로 전부 사전 계산한다. 진행률을 `ScenePreviewJump` Loading UI에 반영하고 매 4노드마다
  프레임을 양보한다. layout rebuild도 같은 준비가 끝나기 전에는 runtime ready를 열지 않는다.
- 동일 1배속 Development 시나리오에서 정적 reachability flood는 `90회/8,733 방문 노드`에서
  `0회/0 방문 노드`로, main-thread p99는 `184.631ms`에서 `22.468ms`로 줄었다. 최종 격리
  Release/D3D11 정상 구간 wall max는 1배속 `23.943ms`, 4배속 `36.965ms`이며, 두 배속 모두 플레이 중
  50ms 이상 프레임은 없다. 빌드→1배속→4배속을 직렬 실행했고 각 플레이 측정 중 루트 플레이어는
  최대 1개, Unity/임포트/빌드 background worker와 다른 작업방 루트는 0개였다.
- `OfficeHybridContinuousDepth`는 재사용 workspace를 사용하며 warmed 100회 정렬의 managed allocation은
  0B다. 의자 presentation 정렬은 수정하지 않았고 계측상 최대 `0.166ms`로 정지 원인이 아니다.
- 정적 캐시 invalidation, first/warm call, 이동·충돌·상호작용·depth, Windows Release build 검증을
  통과했다. 기존 `StaminaRuntimeIntegrationValidation` 전체 시나리오는 exact baseline에서도
  `Actor departed before the 25% threshold`로 실패한다. 현재 브랜치는 병목 제거 뒤 저장 단계까지
  진행하지만 다른 가족의 transient Performing 상태를 byte-for-byte 비교하는 기존 가정으로 실패한다.
  순수 stamina runtime-loss normalization과 GameState save 검증은 각각 통과한다.

## 2026-08-14 / 공용 업무 능력과 인사 roster

- 가족과 향후 채용 완료 직원은 기술개발·기획·창작·사업·운영·협업 6능력과 잠재력을 같은
  `WorkforceCapabilityState`로 사용한다. 별도 Speed는 없으며 업무별 10,000bp 프로필이 진행·품질·학습을 정한다.
- MainNavigationV2 인사 탭은 현재 고용된 가족 4명만 데이터 기반으로 표시한다. 잠재력은 숫자가 아니라
  S/A/B/C/D/F 등급만, 현재 스트레스와 스트레스 저항은 서로 다른 현재 상태로 표시한다.
- Save schema v10은 능력·분야별 XP·fixed-point remainder·스트레스 증가 배율을 저장한다. v1~v9의
  Speed/Stamina/Mental은 이관에서만 운영/스트레스 저항 초기값으로 읽으며 신규 권위 계산에서는 사용하지 않는다.
- 세부 수치와 XP/이관 경계는 `Docs/WORKFORCE_CAPABILITIES_V1.md`가 정본이다.

## 2026-08-14 / Windows Fast QA candidate

- `FAST_QA_WINDOWS.cmd`는 변경 분류, project-local lock, validation manifest, cache fingerprint, 순수 Simulation harness, Editor validation, scripts-only/normal Fast QA player build, D3D11 capture 재사용을 제공한다.
- 출력은 `Artifacts/FastQa`로 격리되며 release build/deploy 경로는 변경하지 않는다.
- 60초는 기능 PASS와 분리된 SLO다. cold import와 clean release는 별도 측정/최종 gate다.
- gameplay, seating, UI, stamina, stats, Save 구현 파일 변경은 0이다.

## 2026-08-14 / Shared stamina + placed-facility recovery

- All four family members start from one 10,000-unit profile. Integer GameTime drains 75% across a
  normal typing workday and creates recovery intent only at the 25% remaining threshold; profile
  overrides remain data-driven for later character differentiation.
- Recovery consumes the build editor's live capability query and existing runtime claim lifecycle.
  Only placed, reachable, available water/vending/lounge offers are selectable. Restroom remains
  fail closed because no definition/facility exists.
- A sticky stamina session gates routine autonomy refreshes while preserving attendance/mandatory
  and contract priority. Successful Performing completion releases the facility, returns to the
  exact assigned seat, and resumes the exact task/remaining minutes. Save schema v10 stores semantic
  stamina and workforce capability state and migrates earlier legacy state at the saved integer minute.
- Unity 6000.3.21f1 pure, 1x/2x/4x, save/load, prototype/micro-action, build capability, and PlayMode
  integration QA pass. `Artifacts/StaminaRuntimeQa/four-family-overhead-bars.png` captures all four
  bars and `summary.txt` records the completed runtime round trip.

## 2026-08-19 / Player Baked Walk V2 south 승인 후보

- `company_unity_walk_final_analysis_33d9b5a.md`의 구조 변경안에 따라, 기존 납작 이미지의 중앙 하체 분할을
  더 조정하지 않고 Editor 전용 PSB paper-doll + Unity 2D IK + PNG bake 경로를 추가했다.
- 주인공 `south` 한 방향만 384×512/324 PPU의 17개 rigid layer와 8개 포즈로 제작했다. 얼굴·빨간 모자·머리·
  재킷·몸통은 승인된 contact 픽셀을 유지하며, 좌우 팔·골반·허벅지·종아리·신발 ownership은 PSB에서 고정한다.
- 베이크 결과는 런타임에서 단일 `SpriteRenderer`만 사용한다. PSB, rig prefab, `LimbSolver2D`, `IKManager2D`,
  AnimationClip은 `Assets/FamilyCompany/Editor` 아래의 전용 authoring 입력이며 Player payload에 넣지 않는다.
- 기본 런타임은 `Legacy48`이다. `PlayerNaturalWalkV1`은 `-familyCompanyPlayerNaturalWalkV1`, 완성된 V2는
  `-familyCompanyPlayerBakedWalkV2`가 있어야만 활성화된다. 8방향 catalog가 없으면 V2 flag는 fail closed한다.
- D3D11 숨김 Editor bake와 south static QA가 PASS했다. 8 PNG, 고정 canvas/PPU, hard alpha, waist 연결,
  material alpha 단일 연결을 검사했다. 최대 support drift는 projected `0.000042px`/2D `0.000044px`,
  0→4 contact-step 오차는 `0px`, 양쪽 passing lift는 `18px`다.
- 아직 **production 승격 아님**: south 사람 승인, 나머지 7방향 독립 제작, 64프레임 catalog, actual normal
  Windows D3D11 Player trace/캡처가 남았다. 게임/Unity 전면 창, Windows build, Downloads 배포는 실행하지 않았다.
- `FC-WALK-GUARDRAIL-V1 확인: 0/3 해부학적 앞발 교대, 2/5 낮은 통과발 교대, 짧은 보폭, 동일 실루엣, 별도 전환 그림 금지, actual normal EXE 판정 전 미배포.`

## 2026-08-20 / Mixamo Humanoid Walk V2 fail-closed 재구성 (거부된 연구 기록)

- 최초 X Bot bake를 분석해 64장 중 pose 0/4/5/6이 8방향에서 byte-identical이고, 나머지도 2~3개
  방향 변형뿐인 원인을 확인했다. `AnimationMode.SampleAnimationClip`이 미리 넣은 root yaw를 덮어썼고,
  `continueOnValidationFailure=true`가 방향별 4.124~13.043% 높이 실패를 warning으로 삼킨 상태였다.
- 베이커는 sample 뒤 yaw를 적용하고, 8 direction row hash/pose hash를 fail-closed 검사한다. 크기는 clip
  보폭에 맞춘 `0.45522`가 아니라 8포즈 투영 실루엣 중앙값 380px로 정하며, runtime stride/8의 support
  foot 궤도는 전체 RGBA frame 정수 픽셀 평행이동으로 결합한다.
- Mixamo X Bot 표면은 pipeline probe로만 남기고 `canonical-protagonist-v1`이 같은 뼈에 빨간 뉴스보이
  캡·흰 후드 윈드브레이커·줄무늬 셔츠·남색 바지·운동화의 닫힌 볼륨을 붙인다. 자동 분리 PSB/2D IK
  종이인형 후보는 final art가 아니다.
- Humanoid 검증 profile을 paper-doll profile과 분리했지만 우회 모드는 제거했다. hard alpha/canvas,
  pelvis, material component, support drift, contact step, passing lift와 방향/포즈 고유성을 모두 통과해야
  한다. 후보는 `PlayerBakedWalkHumanoidV2Candidate`에 격리되고, 전체 PASS 뒤에만 promotion 명령이
  production `PlayerBakedWalkV2` 64장과 catalog를 갱신한다.
- `PlayerBakedWalkV2PlayerQa`는 당시 D3D11 actual player 검증용으로 작성됐다. 당시 pipeline은 bake→검증→
  promotion→Windows build→player QA를 수행했지만 후보 전체가 거부된 뒤 PowerShell entry point와 C#
  production promotion을 영구 차단했다. 현행 gate나 재실행 명령이 아니다.
- Unity 6000.3.21f1 Roslyn 독립 컴파일은 Editor 변경과 Player QA 모두 PASS했고 PowerShell parser도
  PASS했다. 실제 Editor D3D bake는 현재 Codex sandbox가 Unity Licensing Client의 BIOS/MAC WMI 조회를
  `Access denied`로 막아 미실행이다. 로컬 Personal entitlement와 headless entitlement 파일은
  2026-09-19 갱신 상태라 사용자 로그인/라이선스 부재가 아니라 sandbox 경계다.
- **production 승격 아님**: 생성됐던 candidate/runtime payload는 거부됐고 ignored `Artifacts`로 격리했다.
  따라서 런타임 기본은 `Legacy48`이며 Humanoid production promotion은 실행할 수 없다.

## 기준선과 릴리스 판정

| 항목 | 현재 값 |
| --- | --- |
| 문서 정리 기준 코드 | clean local `main` HEAD |
| 원격 기준 | push 후 `origin/main == main`; 그 전에는 `git status --short --branch`로 차이를 확인 |
| Unity | `6000.3.21f1` |
| 시작 씬 | `Assets/FamilyCompany/Scenes/Prototype01.unity` |
| 최종 통합 main SHA | 이 문서를 포함한 clean `main` HEAD; 배포본의 `BUILD_INFO.txt`와 대조 |
| 최종 Windows build SHA | 최종 빌드의 `BUILD_INFO.txt`가 유일한 정본 |
| 최종 통합 QA | compiler, Unity static, D3D PlayMode PASS; Windows player는 배포본 `BUILD_INFO.txt`와 실행 로그로 판정 |

실제 배포 SHA는 실행본 옆 `BUILD_INFO.txt`와 `git rev-parse HEAD`가 같아야 한다. 과거 기능별 SHA는 역사 증거일 뿐 현재 릴리스 식별자로 사용하지 않는다.

## 현재 런타임 정본

| 영역 | 현재 동작 |
| --- | --- |
| 새 게임 | `2000-01-03 08:50`, 가족 4인, 자본금 5,000,000원 |
| 출퇴근 | 가족만 `09:00`~`09:03` 1분 간격, 문 밖 spawn과 단일 ingress 예약으로 입장, `18:00`부터 퇴근 |
| 직원 8인 | 시작 인원이 아닌 향후 채용 후보. 고용 전 런타임 출근 금지 |
| 사무실 | 새 게임은 13×13, 외곽 bay 52, 편집 가능 가구·좌석·재고 0. furnished `StarterOfficeV1`은 migration/QA fixture 전용 |
| 외곽 출입구 | `(8,0)` threshold 1칸. `entrance_door`는 호환 ID이며 door leaf/jamb/lintel 애니메이션이 아님 |
| 메인 UI | `MainNavigationV2`: 회사·인사·사업·연구·투자 5개 허브 |
| 계약 | 고객 등급 `T0 → T1 → T2 → T3 → T4`, 순차 해금과 하락/회복 |
| 사무실 편집 | 배치·회전·이동·회수·재고·저장. 회사 허브에서 진입 |
| 저장 | 전체 `GameSaveDto v10`, `v1`~`v9` 읽기/이관; OfficeGrid 하위 스키마 `v4`, 가구 재고 하위 스키마 `v1` |
| 이동·애니메이션 | 실제 frame displacement 기반 8방향, 인접 경계 hysteresis, 거리 기반 걷기, canonical 가구 회피 |
| 렌더 | 1920×1080 reference, native scale 1, pixel snap, 180 PPU, 캐릭터 scale 1.55 |
| Windows 실행 | 저장소 상대 경로 `BUILD_WINDOWS.cmd` / `RUN_WINDOWS.cmd`; `BUILD_INFO.txt`로 SHA 확인 |

근거 구현은 `GameTime`, `PrototypeStateFactory`, `OfficeAttendanceRules`, `StarterOfficeRuntimeBootstrap`, `MainNavigationHudPresenter`, `ContractGrowthValidation`, `GameSaveDto`, `OfficeNavigationMotionRules`, `DirectionalSpriteAnimator`, `PixelClarityProfile`에 있다.

## 현재 통합됨 (clean `main` HEAD 기준)

- `MainNavigationV2`가 런타임에 연결되었고 거부된 V1 경로는 제거되었다. 회사 허브는 사무실 편집기, 사업은 계약/제품, 투자는 주식으로 연결된다.
- 계약 고객 성장은 day-one T0, T1~T4 순차 해금, 평판/실패 기반 하락과 T0 회복을 순수 시뮬레이션 규칙으로 처리한다.
- 사무실 편집기와 재고 저장은 v8에서 도입되었고, 현재 전체 저장 스키마 v10에 그대로 통합되어 있다. 별도 여섯 번째 하단 탭은 만들지 않는다.
- 플레이어, 가족 출퇴근, 계약 이동이 공유 office locomotion 규칙과 실제 변위를 사용한다.
- 배치 가구의 canonical 4방향 geometry가 occupancy/path의 정본이며 legacy/unknown 저장은 전체 셀 fallback한다.
- 출근 입구는 문 밖 비가시 spawn과 단일 ingress reservation으로 직렬화되어 같은 진입점 중첩을 막는다.
- native render/pixel snap/viewport clarity 기준과 캐릭터 scale 1.55가 적용되었다.
- 외곽 bay 52개, 단일 threshold, 가족 09:00~09:03/18:00 출퇴근 규칙이 적용되었다.
- 주식 코어의 시장 시간, 7+7 호가, FIFO, 수수료·세금, 저장 결정론과 투자 허브 진입점이 유지된다.
- 저장소 상대 경로 Windows 빌드/실행 스크립트와 `BUILD_INFO.txt` 생성 절차가 있다.
- 네 가족의 착석 방향 잠금, 키보드 손 접촉, 의자 하체 가림, 안전한 이석 경로가 통합되었다. 자유 보행만 실제 변위 방향을 사용하며 착석 중에는 좌석 방향 잠금이 최종 권한이다.
- 네 가족의 체력과 머리 위 바가 통합되었다. 25% 임계치 전에는 체력 때문에 일어나지 않고, 임계치 이후 실제 배치·접근·capacity가 유효한 회복 시설만 claim해 수행·해제·원래 업무 복귀한다.
- 가족과 향후 고용 직원은 기술개발·기획·창작·사업·운영·협업 6능력의 공용 모델을 사용한다. 인사 UI는 가족 4명의 실제 상태와 잠재력 문자 등급만 표시한다.
- UI Remaster V3는 프로젝트 내 Maplestory Light/Bold와 670자 glyph 검증을 공용으로 사용한다. Title→New Game, Loading, 5개 허브, People, 계약·건축·주식 경로는 같은 Windows D3D11 런타임에서 검증한다.
- `FAST_QA_WINDOWS.cmd`는 simulation-pure, editor-validation/broad, scripts-only cache, player-startup, D3D capture 프로필과 SLO 수치를 기록한다.

## 열린 기술 부채와 제품 backlog

1. `PlayerEastWalkLockedArtAuthoring`이 완료된 `ArtSources/PlayerEastMixamoTraceV2/`의 계약을 직접 읽어
   P0~P5 lower를 각각 새 physical-owner pelvis→toe chain으로 그린 격리 후보를
   `Artifacts/PlayerEastMixamoLockedArtV3/`에 만들었다. 상체 `y=0..176` 원본 픽셀은 6장 모두 mismatch 0이며,
   v4~v13 lower·거부된 ImageGen·raster-warp 결과는 입력으로 쓰지 않았다. 이 후보는 아직 production art가
   아니다. normal new-game 13×13 타일 맵의 실제 D3D11 격리 QA에는 주인공만 표시해 PASS했고 사용자 타일 맵
   GIF 승인 대기 상태다.
2. 타일 맵 GIF의 회수→교차→전방 착지, 상·하체 동방향, 보강된 하체 두께와 KShopGo식 연속 체중이동을
   사용자가 승인하기 전에는
   `Legacy48` 기본값을 바꾸지 않고 다른 방향·가족으로 확대하지 않는다.
   기존 production은 정확히 `2 steps/tile`, `120.75 steps/min`, visible height 대비 step `27.5%`다.
   사용자 승인 뒤 `speed 1.5`, `stride 1.49071199`의 east 격리 후보를 실제 적용해 `1.333 steps/tile`,
   step/height `41.2%`로 만들었다. target 관절과 foot-lock도 함께 재저작했으며 전역 이동값은 바꾸지 않았다.
3. 걷기 승인 뒤에만 필요하면 Mixamo `Standing Idle`, `Sitting Idle`/`Sitting`, `Typing`, `Sit To Type`,
   `Type To Sit`을 필요한 순서로 받는다. 걷기 gate에 추가 클립을 선행 조건으로 만들지 않는다.
4. 직원 후보 8인은 고용 시스템이 생긴 뒤에만 출근시킨다. 시작 roster나 09:00~09:03 가족 출근에 섞지 않는다.
5. 소파/다인 좌석은 group atomic claim, 짝 이동, 취소/퇴장 해제, non-NorthWest pose 승인과 idle/emote QA를 추가한다.
6. 오피스 확장은 현재 StarterOffice를 보존하며 단계별 면적/가구 해금으로 구현한다. 과거 요청서의 숫자를 검증 없이 새 정본으로 삼지 않는다.
7. Utility AI의 생산 선택은 `OfficeAutonomyCoordinator`가 소유한다. Shadow scoring 결과를 생산 동작으로 오인하지 않는다.

## 검증 상태

| 범위 | 기준 | 결과 |
| --- | --- | --- |
| Simulation/Editor 전체 회귀 | clean `main` HEAD | FastQA `simulation-pure`, `editor-validation`, `editor-broad` PASS |
| Workforce/Save v10 | clean `main` HEAD | skills=6, grades=S-F, v1~v9 migration, 1x/2x/4x PASS |
| UI V3/Maplestory | clean `main` HEAD | assets=24, characters=670, missingGlyphs=0; 1280×720·1392×768·1600×900/1000·1920×1080 PASS |
| Windows D3D11 UI | `9144fa0e` 실제 native pointer | PASS — 녹색 preview 1클릭이 commit 1회·mutation 1회, 자금 `5,000,000→4,986,250`, furniture `52→53` |
| Windows D3D11 사무실 | `9144fa0e` normal 처음하기 | PASS — 08:50→09:50 4인 보행, `currentLook=0`, `duplicatePivot=0`, `nonCardinal=0`, `collision=0` |
| 가족 4명 보행 리그 | `8fa5fa74` | 진행 중 — foot-lock QA 192/192, Unity batch, `editor-broad` PASS. 배포 Player trace와 캡처 사람 검토 미실시 |
| 주인공 2D Walk V2 / v4~v13 | local rejected research | REJECTED — 다리/팔 일부는 선호됐지만 발목 이중화, 역주행, 하체 방향 불일치와 KShopGo 위상 미추적으로 전체 후보 거부. production 미승격 |
| Player east Mixamo Trace V2 | tracked derived motion contract | PASS — long-stride derived owner/phase, root `28.852490px/pose`, target foot-lock `0.295020px`; raw export는 ignored Artifacts 전용이며 게임 raster/GIF 승인을 뜻하지 않음 |
| Player east LockedArtV3 isolated candidate | ignored `Artifacts/PlayerEastMixamoLockedArtV3` | STATIC + ACTUAL TILE PLAYER PASS — 6 poses, root `28.852490px/pose`, target contact drift `0.295020px`, upper mismatch 0, soft alpha 0, missing joint 0, material component 6/6, east shoe 12/12, unique hash 6/6. 신발 최소 색 높이 `11px`, 색 면적 `207px`, 빨강 `67px`, 흰색 `88px`. 최대 interior outline `13px`, hip-junction exact black `0/6 poses`, 최초 leg split은 pelvis 아래 최소 `17px`. normal new-game 13×13 타일 맵의 Windows D3D11에서 player만 표시, editable furniture 0, PPU 180, scale 1.55, 격리 speed `1.5`, stride `1.490712`, cycle distance `1.498756`, VisualRoot offset 0, P0~P5 캡처 PASS. 후보는 `1.333 steps/tile`, cadence `120.75 steps/min`, step/visible-height `41.2%`다. `Legacy48` production 기본·전역 speed/stride·배포 EXE는 그대로이며 Assets 승격은 미실시 |
| Player east ImageGen / LockedArtV2 | tracked rejected evidence | REJECTED — 기존 3회와 home-PC 2회 모두 owner/contact 또는 exact canvas/identity 이탈. shipping frame 0 |
| Mixamo Humanoid Baked V2 | rejected research | REJECTED — 3D primitive 인상, 정체성 불일치, 과한 바운스. production 재실행 금지 |
| 최종 portable Windows build | 최종 `main` | **BLOCKED** — 배포본 `Downloads\Family`가 `befe937e`로 HEAD보다 뒤. 위 보행 gate 통과 후 HEAD로 재배포해야 함 |

과거 개별 PASS는 해당 기능의 회귀 근거다. 최종 결합 SHA의 PASS를 대신하지 않는다.

## 최종 릴리스 체크리스트

1. `git diff --check`, C# compiler, 순수 검증 harness, Unity D3D PlayMode/render QA를 통과한다.
2. 저장 v1~v9→v10, 새 게임 v10, 편집 재고, 계약 성장, 주식 계좌, 출퇴근, 실제 변위 회귀를 확인한다.
3. `BUILD_WINDOWS.cmd`로 새 실행본을 만들고 `BUILD_INFO.txt`와 현재 HEAD가 같은지 확인한다.
4. [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)의 네 가족 09:00/09:01/09:02/09:03 oracle과 독립 gate를 통과시킨다.
5. 검증된 새 identity의 폴더 전체만 현재 배포 경로 `%USERPROFILE%\Downloads\Family`에 배포하고 staging은 같은
   부모의 `.Family.deploy-staging`을 쓴다. `Tools/FamilyCompanyBuild.Common.ps1`의
   `Get-FamilyCompanyDeployDefaults`는 아직 옛 이름 `FamilyCompany_Playtest`와 존재하지 않는 branch
   `codex/integration-p0-qa`를 기본값으로 갖고 있으므로, 배포는 `-TargetPath`/`-RequiredBranch main`을 명시해
   호출한다. FAIL/UNKNOWN이면 evidence 보존 후 해당 payload를 삭제하고 rollback하거나 current를 비워 둔다.

## 다른 PC에서 이어하기

가족 캐릭터는 `Docs/FAMILY_3D_CHARACTER_HANDOFF_2026-08-24.md`의 3D 재현·검수 순서를 따른다.
일반 저장소/실행 절차는 아래와 같다.

```powershell
git switch main
git status --short --branch
git pull --ff-only origin main
.\BUILD_WINDOWS.cmd
.\RUN_WINDOWS.cmd
```

빌드가 이미 있더라도 `Builds/Windows/FamilyCompany_Playtest/BUILD_INFO.txt`의 commit이 `git rev-parse HEAD`와 다르면 최신 실행본으로 간주하지 않는다. 상세 절차는 [HOME_PC_CONTINUATION_GUIDE.md](HOME_PC_CONTINUATION_GUIDE.md)와 [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)를 따른다.

위 `BUILD_WINDOWS.cmd`는 그 PC의 실행본을 처음 한 번 만들 때와 배포 후보를 확정할 때만 쓴다. 이후 한 곳을
고치고 확인하는 반복 작업에는 `FAST_QA_WINDOWS.cmd`를 쓰며, 근거와 변경 종류별 명령은
[ITERATION_LOOP.md](ITERATION_LOOP.md)가 정본이다.

회귀·실패·출처 불명·self-PASS-only 실행본의 처리와 재빌드 조건은 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)를 반드시 따른다.

## [퇴역 기록] 2026-08-23 네 가족 동일 2D 보행 신규게임 빌드 요청 상태

- 실제 신규 회사 사무실은 `OfficeGridLayouts.CreateNewGameEmptyOfficeV1()`이며 내부 구매 가구는 0개다.
  바닥과 비가동 외곽 벽만 존재하고 저장 불러오기를 거치지 않는 상태가 정확한 시작 상태다.
- 일반 Windows 빌드의 `OfficeRuntimeCharacterArtCatalogBuilder`는 아직 네 사람 모두 production
  `HighMotion/Frames`를 다시 묶는다. 따라서 일반 빌드만 실행하면 Player V6·아빠 R14·엄마 R17 조합이
  자동으로 들어가지 않는다.
- 새 공용 24포즈 대조에서 아빠 R14는 Player V6 접촉·지지·낮은 통과 계열로 읽혔고 기존 단독 D3D11
  증거도 mismatch 0이지만, 새 공용 visual review는 아직 unsigned다.
- 엄마 R17은 원본 전신에 Player cadence/stride/slot order만 적용한 후보라 팔이 거의 고정되어 Player V6의
  반대팔 스윙과 같지 않다. 누나는 최신 사용자 판정상 유효 후보가 0개다.
- `Tools/Test-FamilyWalkFourFamilyCleanStartInputsV1.ps1`을 추가하고 실행했다. 결과는
  `BLOCKED_WRONG_OR_MISSING_INPUTS`, checks `167`, failures `5`, buildExecuted `no`다. 잘못된 네 가족 EXE,
  production/기본/Downloads EXE는 만들거나 변경하지 않았다.
- 후속 skeletal rig, 누나 per-pose V2, 엄마 per-pose V2도 확대에서 각각 분리 손/막대 다리, 동일 큰 앞발
  체인 또는 포즈별 얼굴·몸 폭·배경 불일치로 폐기했다. 누나는 여전히 유효 24포즈 후보가 0개이며
  fail-closed 신규게임 빌드는 계속 `buildExecuted=no`다.

## 정본 문서 경계

- 인물·출퇴근·사무실 시각: [CANON.md](CANON.md), [ART_STYLE.md](ART_STYLE.md)
- 구조·저장·Unity 경계: [ARCHITECTURE.md](ARCHITECTURE.md)
- 반복 개발 루프와 캐시 규칙: [ITERATION_LOOP.md](ITERATION_LOOP.md), [FAST_QA_WINDOWS.md](FAST_QA_WINDOWS.md)
- build/deploy 회귀 삭제: [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)
- 내비게이션·편집: [MAIN_NAVIGATION_HUD_V2.md](MAIN_NAVIGATION_HUD_V2.md), [OFFICE_BUILD_EDITOR_V1.md](OFFICE_BUILD_EDITOR_V1.md)
- 계약: [CONTRACTS_V0_3.md](CONTRACTS_V0_3.md), [CONTRACT_CLIENT_PROGRESSION_V1.md](CONTRACT_CLIENT_PROGRESSION_V1.md)
- 주식: [SIMUL_MARKET_PORT.md](SIMUL_MARKET_PORT.md), [STOCK_MARKET_LANDSCAPE_V1.md](STOCK_MARKET_LANDSCAPE_V1.md)
- 역사 구현 증거: `History/Reports/` — 현재 상태 정본 아님

## 2026-08-28 Father V19 one-package walk 후보

Higgsfield/Meshy의 메시·bind skeleton·skin weight·action 613을 한 작업에서 받은 Father V19 후보를
Experimental에 반입했다. 127프레임은 42프레임 보행 3회였으므로 Unity importer는 `1..43` 한 주기만
사용한다. 다른 리그로 retarget하거나 절차 보행/팔 보정/웨이트 일괄 삭제를 적용하지 않는다.

최종 격리 build는 `FatherV19MeshyOnePackage613MapBuildV3ColorDetail`, 실제 맵 runtime은
`FatherV19MeshyOnePackage613MapRuntimeV4ColorDetail`이다. 실제 Father agent 4방향 2회전 1,344프레임과
169 captures를 완료했다. stride `0.7950477`에서 접지 발 median world speed는 left/right
`0.0933/0.0678`, torso lean mean `2.31°`, 손 반대 스윙 상관 `-0.939`다. 같은 맵 위치의 회전 간
root position/yaw 오차는 `0/0°`, phase delta `0.0000108`이다. production scene, preview,
EditorBuildSettings SHA는 before/after 동일하다.

V3에서 색이 옅었던 원인은 원본 GLB의 albedo emission 중복, specular factor `2.0`, 실제 Sun+QA light
중복이었다. V4는 원본 texture/UV/mesh를 유지하고 emission off, metallic `0`, smoothness `0.22`, 단일
candidate light와 uncompressed albedo import를 사용한다. actor highlight clamp는 `27.522% -> 0.675%`,
평균 채도는 `26.91 -> 38.00`으로 복구됐다.

상태는 `VISUAL_CANDIDATE_READY_USER_APPROVAL_REQUIRED`, `productionEligible=false`다. 상세 출처,
실패 원인, 구조 검사, GIF 경로는
[FATHER_V19_MESHY_ONE_PACKAGE_WALK_QA_2026-08-28.md](FATHER_V19_MESHY_ONE_PACKAGE_WALK_QA_2026-08-28.md)를 따른다.

## 2026-08-28 Father V19 실제 3D 책상 착석·업무 후보

사용자는 V19 실제 맵 걷기와 복구된 색을 `좋아잘된당`으로 승인하고 실제 책상에 앉아 일하는 단계까지
요청했다. 새 proof는 승인된 one-package action 613 보행을 그대로 두고, 실제 `seat_father` 경로가
locomotion을 끝낸 뒤에만 같은 Avatar의 neutral seated pose와 두 손/두 발 endpoint IK를 적용한다.

최종 격리 build/runtime은 `FatherV19MeshyOnePackage613MapBuildV10Full3DDeskWorkFinal` /
`FatherV19MeshyOnePackage613MapRuntimeV12Full3DDeskWorkFinal`이다. 실제 phase
`Idle>Navigating>ApproachingSeat>AligningSeat>RotatingToSeat>Working`, sample 1,051,
work 361, captures 132를 완료했다. 책상·CRT·키보드·전화기·의자는 QA layer의 runtime 3D 소품이며
production 가구 catalog나 Transform을 바꾸지 않는다. 통짜 등받이, 셔츠와 같은 의자색, 5발 받침,
몸 뒤 손과 옆으로 빠진 발은 폐기했고 `-45/0/+45°` 실제 맵 비교에서 두 손·두 다리가 가장 잘 읽힌
`-45°`를 기본값으로 고정했다.

상태는 `FATHER_V19_FULL_3D_DESK_WORK_PROOF_COMPLETE`, `productionMutation=false`,
`productionEligible=false`다. 전체 이동→착석→업무 GIF와 두 손 타건 확대 GIF에 대한 사용자 판정이
다음 gate다. 상세는
[FATHER_V19_FULL_3D_DESK_WORK_QA_2026-08-28.md](FATHER_V19_FULL_3D_DESK_WORK_QA_2026-08-28.md)를 따른다.
