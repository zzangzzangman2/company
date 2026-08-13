# 사무실 표현 품질 개선 완료 (2026-08-13)

## 결과

- 현재 `main`에는 보행 원화 커밋 `6b7b020`이 포함되어 있다.
- 현재 원본 재검사: 12명 × 8방향 × 6프레임, walk 96/96 및 전체 192/192 strict PASS.
- 실제 Windows Release Player 전체 QA: 8방향 이동, 급반전 정지-Pivot-재가속, 충돌, 상호작용 목적지, 4인 착석·작업·기립, 계약·저장·불러오기 PASS.
- 4인 타이핑 접촉: 골반-의자 최대 0.000px, 손-키보드 최대 1.195px, 회전 0°, 스케일 변화 0%.
- `1×`에서도 실제 시간이 흐르므로 초기 누나 스트레칭을 포함한 분 단위 자율행동이 정상 종료된다.
- 상단 HUD는 어두운 현대식 3단 카드 HUD로 교체했으며 가족 목록을 런타임 데이터에서 생성한다.
- Starter 사무실을 메뉴 뒤에서 미리 로드하고 첫 프레임부터 구형 렌더러를 숨긴다.
- 참조 0인 구형 `money_rain_office_background_v1.png`와 `.meta`를 삭제했다.

## 공용화 경계

손·의자 접촉은 캐릭터 ID별 위치 상수를 사용하지 않는다. 좌석 포즈 카탈로그의 골반·손 앵커와 가구 카탈로그의 좌석·작업 소켓으로 계산한다. 평면 캐릭터 이미지는 늘이거나 회전하지 않고 손을 키보드에 고정하며, 남는 체형 차이는 충돌·경로에 영향을 주지 않는 의자 표시 pull-out으로 흡수한다. 새 캐릭터는 같은 앵커 계약만 제공하면 같은 런타임을 쓴다.

HUD 가족 카드도 `Family.Members`를 순회하므로 가족 수가 늘어날 때 ID별 HUD 코드를 추가하지 않는다.

## 검증 산출물

- `Artifacts/AnimationCoherence/animation-coherence-current.txt`
- `Artifacts/PresentationPolishQaV4/family-company-player-qa.log`
- `Artifacts/PresentationPolishQaV4/starter-office-modern-hud.png`
- `Artifacts/PresentationPolishQaV4/starter-office-four-seat-work.png`
- `Artifacts/PresentationPolishQaV4/*-work-closeup.png`
- Windows x64 Release: `C:/Users/godho/Downloads/Family/FamilyCompany_Playtest/FamilyCompany.exe`
