# Player Walk 2D Generated Source

이 폴더는 2D 런타임 방향에 맞춘 주인공 보행 후보의 추적 원본이다.

- 최종 게임 캐릭터는 2D 스프라이트다.
- Mixamo 3D는 `Unarmed Walk Forward`의 관절 순서와 0.8초 타이밍 참고에만 사용한다.
- `player_walk8dir6_a_chroma_v2.png`: south, southwest, west, northwest × 6포즈
- `player_walk8dir6_b_chroma_v2.png`: north, northeast, east, southeast × 6포즈
- `player_walk8dir6_a_chroma_v3.png`: south, southwest, west, northwest × 6포즈. 상체/외형 참고용 거부 후보
- `player_walk8dir6_b_chroma_v3.png`: north, northeast, east, southeast × 6포즈. 상체/외형 참고용 거부 후보
- `player_walk8dir3half_a_chroma_v5.png`: south~northwest 첫 반주기 3포즈 연구 원본
- `player_walk8dir3half_b_chroma_v5.png`: north~southeast 첫 반주기 3포즈 연구 원본
- 두 원본은 기존 빨간 뉴스보이 캡·흰 후드·줄무늬 셔츠 주인공 시트를 정체성 참고로 사용해
  OpenAI ImageGen으로 2026-08-20 생성했다.
- 배경은 후처리용 녹색 크로마다. 런타임에 직접 넣지 않는다.
- 결정론적 투명화·256px 분리·머리 높이 정규화는
  `Tools/Build-Player2DWalkV2Candidate.ps1`이 담당한다.

이 폴더의 v2/v3/v5는 전부 역사/외형 참고다. v3 하체를 반사한 v4와 이후 v5~v13은 발목 이중화,
physical owner 역전, 상·하체 방향 불일치 또는 KShopGo 위상 미추적으로 거부됐다. 현재 motion 정본은
`ArtSources/PlayerEastMixamoTraceV2/`이며 이 폴더의 lower body를 production donor로 사용하지 않는다.

과거 `FC-WALK-GUARDRAIL-V1` 화면에서 다리 교차·팔 자세 일부는 선호됐지만 support-foot가 미측정이고
보행 owner/방향 결함이 뒤늦게 확인됐다. shipping 기본값은 계속 `Legacy48`이다.

v5 반주기 원본은 `Build-Player2DWalkHalfCycleV5Candidate.ps1` 연구 입력이다. 두 번째 반주기를 하체 반사로
만들었지만 현재 two-step gate가 2/8행만 통과하므로 Unity 런타임에 복사하지 않는다.
