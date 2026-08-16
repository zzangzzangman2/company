# REGRESSION BUILD POLICY

이 문서는 Family Company의 회귀·실패·출처 불명 실행본을 다루는 영구 fail-closed 정책이다. 모든 수동 빌드·검증·배포와 `BUILD_WINDOWS.cmd`, Fast QA, watcher, deploy script 등 자동화에 적용한다. 다른 문서나 과거 자동화 동작이 이 정책과 충돌하면 이 정책을 먼저 따른다.

## 1. 절대 규칙

다음 중 하나라도 해당하는 실행 payload는 candidate, current, last-known-good 또는 Downloads의 플레이테스트 실행본으로 존재할 수 없다.

- 사용자가 실제 화면이나 조작에서 확인한 **user-visible regression**
- 필수 oracle, smoke, static/runtime/visual/performance 검증 중 하나라도 실패한 **failed gate**
- commit, Unity revision, input fingerprint, gate 결과를 독립적으로 확인할 수 없는 **stale/unverified provenance**
- 같은 기능이 소유한 fixture나 harness의 PASS만 있고 독립 release oracle이 없는 **self-PASS-only candidate**

발견 즉시 다음 순서를 한 fail-closed 처리로 수행한다.

1. 삭제 대상을 정확히 식별하고 exact-root fence를 통과한다.
2. 해시·로그·manifest 같은 비실행 evidence를 payload 밖의 증거 위치에 먼저 보존한다.
3. candidate가 current라면 current 승격을 취소하고, 별도로 검증된 정상 build가 있을 때만 rollback한다. 정상 build가 없으면 current를 비워 둔다.
4. EXE (`*.exe`), `*_Data/`, `UnityPlayer.dll`과 함께 제공된 DLL, runner, script, plugin, archive 등 **전체 실행 payload를 즉시 삭제**한다.
5. exact target 아래 실행 payload가 0개임을 재검사한 뒤에만 삭제 완료를 기록한다.

폴더 이름 변경, timestamp/SHA suffix 추가, `quarantine`, `failed`, `candidate`, `last-known-good` 디렉터리로의 이동만으로 current 또는 Downloads에 보존하는 것은 삭제가 아니다. **이름 변경/격리만으로 current 보존을 금지**하며, 회귀 실행본을 이름만 바꾸어 같은 Downloads/current 영역에 남길 수 없다.

## 2. 판정 단위와 용어

- **실행 payload**: EXE, Unity `*_Data` 폴더, `UnityPlayer.dll`, managed/native DLL, 실행 runner, CMD/PowerShell script, plugin, 또는 이를 다시 실행 가능하게 묶은 archive와 같은 배포 단위 전체다.
- **비실행 evidence**: SHA-256 목록, `BUILD_INFO.txt`, deploy/build manifest, gate 결과, console/Player 로그, failure summary처럼 실행 코드를 포함하지 않는 읽기 전용 기록이다.
- **candidate**: build identity가 생긴 순간부터 독립 release gate가 끝날 때까지의 실행 payload다.
- **current**: 사람이 현재 실행할 수 있도록 canonical local 또는 Downloads target에 놓인 payload다.
- **독립 gate**: candidate를 만든 코드 경로나 self-fixture와 다른 runner가 사용자-visible production call graph와 oracle을 판정하는 gate다.

evidence라는 이름으로 EXE/DLL/Data/script/archive를 복사하거나 보존해서는 안 된다. 증거 manifest에는 원 payload를 재구성하는 바이너리를 넣지 않는다.

## 3. Evidence-before-delete 계약

삭제 전에 최소한 다음 비실행 evidence를 payload 바깥에 원자적으로 기록한다.

| 필드 | 필수 내용 |
| --- | --- |
| identity | commit SHA, tree/input fingerprint, build ID, Unity exact version/revision |
| location | canonicalized exact payload root와 발견 시각 |
| classification | user-visible regression / failed gate / stale-unverified provenance / self-PASS-only 중 해당 사유 |
| oracle | 실패한 gate 이름, 기대값, 실제값, 독립 gate 여부 |
| hashes | 삭제할 파일의 상대 경로·크기·SHA-256 manifest |
| logs | 관련 build/deploy/Player 로그의 비실행 사본 또는 해시와 원본 위치 |
| deletion | 삭제 시작/완료 시각, 삭제 후 payload 0개 재검사 결과, 수행 주체 |
| rollback | 복구한 별도 정상 build identity 또는 `none` |

증거 기록 실패, hash 실패, manifest 불완전은 삭제 대상을 정상 candidate로 바꾸지 않는다. 자동화는 성공·승격으로 진행하지 않고 hard failure를 기록한다. 단, evidence 보존을 생략한 추측성 광역 삭제도 금지한다. 사람이 exact target과 최소 identity를 확인할 때까지 current 실행과 추가 승격을 차단한다.

## 4. Exact-root fencing과 unrelated build 보호

삭제는 다음 조건을 모두 만족할 때만 허용한다.

1. target은 manifest/status가 가리키는 **단 하나의 canonical absolute build root**여야 한다.
2. `Resolve-Path`와 동등한 canonicalization 뒤 target이 승인된 build/deploy parent 바로 아래의 정확한 자식인지 확인한다.
3. repository root, workspace root, drive root, 사용자 profile root, `Downloads` 전체, `Builds` 전체, glob, 빈 문자열, unresolved 환경 변수, 상대 경로는 target이 될 수 없다.
4. target 내부의 `BUILD_INFO.txt`/deploy manifest identity와 판정 대상 identity를 대조한다. 불일치하면 자동 삭제하지 않고 fail closed한다.
5. 삭제 manifest에 없는 sibling, 다른 commit의 build, 다른 작업방 output, AppData save, source, repository 파일은 절대 건드리지 않는다.
6. recursive delete는 canonical target 하나에만 수행하며, 삭제 후에도 parent와 모든 sibling이 그대로인지 확인한다.

**Unrelated builds 삭제는 금지한다.** 한 candidate의 회귀가 다른 build를 정리할 권한을 만들지 않는다. 별도 정상 rollback build를 삭제 대상으로 포함하거나, 정리 편의를 위해 parent 전체를 비우는 것도 금지한다.

## 5. Current와 Downloads 처리

- user-visible regression이나 failed gate가 current에서 발견되면 evidence를 먼저 보존하고 current 실행 payload 전체를 즉시 삭제한다.
- Downloads target도 같은 규칙을 적용한다. `FamilyCompany_Playtest.failed.*`, `.quarantine`, `.last-known-good.*` 같은 이름으로 회귀 payload를 Downloads에 남기지 않는다.
- rollback은 **별도 identity의, provenance가 검증되고 모든 당시 필수 gate를 통과한 build**에만 허용한다. rollback 후보 자체가 stale/unverified 또는 self-PASS-only이면 사용하지 않고 current를 비워 둔다.
- 파일 잠금 때문에 즉시 삭제할 수 없으면 자동화는 success나 `AwaitingPromotion`을 기록할 수 없다. current pointer/runner를 먼저 차단하고 hard failure로 남긴 뒤 동일 exact target 삭제를 재시도한다. 잠긴 회귀 payload를 정상 current로 간주하거나 새 candidate 승격을 계속해서는 안 된다.
- AppData의 게임 save는 build payload가 아니며 이 정책의 삭제 대상이 아니다.

## 6. 재승격 금지와 새 build identity

회귀로 판정된 payload는 수정되었더라도 다시 승격하거나 이름을 바꾸어 재사용할 수 없다. 기존 candidate, staging, cache, manifest의 identity를 재활용하는 것도 금지한다.

수정 후에는 다음을 모두 만족해야만 **새 build identity로 처음부터 새로 빌드**할 수 있다.

1. 발견된 회귀와 직접 관련된 모든 regression oracle PASS
2. 해당 변경 범위의 기존 필수 gate 전부 PASS
3. self-fixture와 다른 독립 gate PASS
4. clean committed source와 exact Unity revision 확인
5. 새 commit/input fingerprint/build ID와 새 manifest 생성
6. 새 payload의 independent smoke/runtime 검증 PASS

하나라도 빠지면 새 payload 역시 self-PASS-only 또는 unverified candidate이며 current/Downloads에 둘 수 없다.

## 7. 필수 네 가족 출근 oracle

모든 Windows release candidate와 출근·actor·시간·navigation·seat·trace·preload 변경은 다음 production oracle을 독립 gate에서 통과해야 한다. self-test의 actor count나 route prewarm PASS만으로 대체할 수 없다.

| 게임 시각 | 필수 actor | 필수 production 결과 |
| --- | --- | --- |
| 08:50 | 4명 전체 | fresh state, actors=4, routes=4, runtime ready, exception 0 |
| 09:00 | `player` | 문 밖 release, ingress 실제 이동, assigned seat 도달/착석 |
| 09:01 | `older_sister` | 문 밖 release, ingress 실제 이동, assigned seat 도달/착석 |
| 09:02 | `father` | 문 밖 release, ingress 실제 이동, assigned seat 도달/착석 |
| 09:03 | `mother` | 문 밖 release, ingress 실제 이동, assigned seat 도달/착석 |

09:04까지 네 actor 모두 정확히 한 번 입장해 route progress 또는 assigned seat 착석을 증명해야 한다. actor ID와 scheduler/trace identity가 일치하고, unique seat claim, released ingress, clock progress, `IsReady=true`, Player log exception 0을 함께 요구한다. 한 명만 입장하거나 later actor가 due time 뒤에도 숨겨져 있으면 즉시 user-visible regression/failed gate로 판정한다.

## 8. Build/deploy 자동화의 fail-closed 의무

build/deploy 자동화는 promotion 전과 smoke/runtime 후에 이 정책을 집행해야 한다.

```text
build candidate
  → provenance + required gates
  → independent production oracle
  → PASS: new identity를 current로 원자 승격
  → FAIL/UNKNOWN: evidence 보존 → candidate 전체 삭제 → current rollback 또는 empty → hard fail
```

- regression 발견 시 candidate/current payload 삭제와 rollback을 한 transaction으로 처리한다.
- gate process crash, timeout, missing result, `PENDING`, stale log, denominator 0은 PASS가 아니라 FAIL/UNKNOWN이다.
- self-PASS는 독립 gate를 생략할 권한이 없다.
- post-promotion smoke에서 회귀가 나오면 새 current를 즉시 제거하고, 검증된 이전 build만 복구한다.
- delete/rollback 검증이 끝나기 전에는 watcher가 다음 candidate를 만들거나 승격하면 안 된다.
- status와 종료 코드는 payload 삭제 실패, rollback 실패, evidence 실패를 성공과 구분해야 한다.

현재 자동화가 이 계약을 구현하고 독립 fixture로 증명하기 전에는 실제 current/Downloads 배포에 사용하지 않는다. 이 문서의 추가만으로 자동화 구현 PASS를 선언하지 않는다.

## 9. 최종 push 전 remote zero-inventory gate

어느 PC에서든 checkout, pull, tag checkout, release 다운로드로 회귀·구 실행본이 다시 들어오지 않도록 **최종 feature/release push 전에 remote zero-inventory gate를 반드시 통과**한다. 여기서 zero는 금지 payload가 0개라는 뜻이다. 독립 검증을 통과한 현행 release payload가 명시적으로 허용된 경우를 제외하고, identity나 판정을 확정할 수 없는 실행본은 모두 금지 payload로 센다.

gate는 fresh fetch와 전체 pagination을 사용해 다음 표면을 각각 검사한다.

1. `origin/main` current tip tree
2. `refs/remotes/origin/*`의 모든 active branch tip tree
3. lightweight/annotated tag가 가리키는 모든 tag tree
4. draft, prerelease, archived release를 포함한 모든 remote release asset
5. Git LFS pointer나 archive가 위 표면에서 실행 payload를 간접 보존하는지 여부

검사 대상은 Family Company의 playable/build output 전체다. `FamilyCompany.exe`, 대응하는 `FamilyCompany_Data/`, `UnityPlayer.dll`, 동봉 DLL/runner/script/plugin, portable ZIP/7z와 staging/current/LKG 복제본을 한 payload로 묶는다. source dependency나 의도적으로 추적하는 개발 도구를 build output으로 오분류하지 않도록 exact path, manifest identity, 파일 hash를 함께 판정한다. 분류되지 않은 executable/archive는 gate를 실패시킨다.

remote inventory 결과는 다음을 포함한 비실행 `remote-zero-inventory` manifest로 보존한다.

| 필드 | 필수 내용 |
| --- | --- |
| observation | fetch 시각, remote URL, 검사 도구/version, API pagination 완료 여부 |
| main | `origin/main` SHA와 tree SHA |
| branches | 모든 검사 branch ref와 tip/tree SHA |
| tags | 모든 tag ref, tag object, dereferenced commit/tree SHA |
| releases | release ID/tag, draft/prerelease 상태, 모든 asset ID/name/size/hash |
| payloads | 발견 경로/asset, build identity, 분류, 허용 또는 제거 근거 |
| result | prohibited payload count `0`, unknown count `0`, gate PASS/FAIL |

stale fetch, remote 접근 실패, release API 권한 부족, pagination 미완료, asset hash 미확인, unknown identity는 PASS가 아니다. remote ref 이름 일부만 검색하거나 local working tree만 clean하다는 이유로 zero-inventory를 선언하지 않는다.

`origin/main` 또는 다른 current remote 표면에서 금지 payload가 발견되면 일반 feature/release push를 중단한다. 먼저 exact tracked path/ref/release asset 목록을 가진 별도 cleanup 작업으로 제거하고 remote를 다시 fetch/inventory해 prohibited=0, unknown=0을 증명해야 한다. 오염된 `origin/main`을 정리하기 위한 전용 cleanup push는 최종 feature push와 분리하고 명시적으로 승인받는다. 그 cleanup이 remote에 반영된 뒤에만 최종 push gate를 다시 시작한다.

## 10. `.gitignore`와 tracked-build removal 계약

repository root `.gitignore`는 build/deploy output이 다시 add되지 않도록 root-anchored, path-specific 규칙을 가져야 한다. 최소한 repository의 `Build`, `Builds`, `Artifacts`, Fast QA output, deploy staging/current/LKG 경계를 실제 생성 경로와 대조하고 representative `FamilyCompany.exe`, `*_Data`, `UnityPlayer.dll`, portable archive 경로가 ignore되는지 `git check-ignore -v --no-index`와 동등한 검사로 증명한다.

모든 DLL/EXE를 전역 wildcard로 무시해 legitimate source dependency나 도구를 숨기는 방식은 금지한다. 새 output root를 추가하는 build/deploy 변경은 같은 변경에서 exact `.gitignore` 규칙과 representative ignore fixture를 추가해야 한다. ignore 예외는 소유 문서, provenance, review 근거가 있는 개발 입력에만 허용하며 playable payload 예외로 사용할 수 없다.

`.gitignore`는 이미 tracked된 파일을 제거하지 않는다. 따라서 `git ls-files`, candidate tree, `git ls-tree` 검사에서 build payload가 발견되면 다음을 수행해야 한다.

1. exact tracked roots/files와 blob SHA를 evidence manifest에 기록한다.
2. unrelated source, dependency, 다른 build를 제외한 exact payload만 current tree에서 삭제한다.
3. tracked entry 제거와 필요한 `.gitignore` 보강을 review 가능한 일반 cleanup commit으로 남긴다.
4. cleanup commit의 tree에서 payload path 0개와 representative ignore PASS를 확인한다.
5. 승인된 cleanup push 뒤 `origin/main`을 다시 fetch해 remote tree에서도 0개임을 확인한다.

`git rm --cached`만 실행해 실행 payload를 working tree/current에 남기는 것은 기존의 전체 payload 즉시 삭제 계약을 만족하지 않는다. 반대로 parent build directory 전체나 broad extension glob으로 unrelated files를 삭제하는 것도 금지한다.

## 11. 일반 삭제와 history rewrite/force-push의 권한 경계

current tree에서 tracked payload를 삭제하는 일반 cleanup commit은 과거 commit object를 제거하지 않는다. remote branch/tag/release asset의 명시적 삭제도 각 ref/asset에 대한 외부 파괴 작업이지만, commit graph 자체를 다시 쓰는 history rewrite와는 구분한다. 어느 경우든 이 정책은 자동 실행 권한을 부여하지 않으며 exact target과 승인 범위가 필요하다.

다음은 **history rewrite/force-push**로 분류하며 일반 회귀 payload 삭제 절차로 수행할 수 없다.

- `git filter-repo`, BFG 등으로 기존 commit/tree/blob를 재작성
- 기존 branch commit graph를 바꾼 뒤 `--force` 또는 `--force-with-lease` push
- 기존 tag를 다른 object로 재지정하거나 서명된 tag를 교체
- remote mirror 전체를 rewritten object graph로 교체

history rewrite/force-push는 다음 조건을 모두 완료하고 사용자 및 영향받는 collaborator의 명시적 승인을 받기 전까지 금지한다.

1. 제거하려는 blob/object SHA, 크기, hash, 도달 가능한 모든 branch/tag/release와 최초·최종 commit을 포함한 exact reachability audit
2. 원격 refs/tags/releases와 commit graph를 복구할 수 있는 검증된 offline backup/bundle/mirror, backup hash, restore rehearsal
3. collaborator/worktree/CI/deploy clone 목록과 open branch/PR를 포함한 영향 범위 조사
4. 기존 clone의 rebase/pull이 아니라 폐기 후 **re-clone**해야 하는 절차, 작업 중단 시간, 소유자별 미push 변경 보존 계획
5. signed tag/release URL, CI cache, submodule/LFS pointer, 문서 permalink가 바뀌는 영향과 migration/rollback 계획
6. rewrite 대상 exact refs, 실행 창구, force-push 방식, 검증자, rollback deadline에 대한 별도 승인

승인 전에는 historical object에 payload가 남아 있다는 이유로 force-push하거나 tag를 재작성하지 않는다. 대신 current `origin/main`, active branch/tag tip, release asset을 zero-inventory로 만들고, historical reachability는 별도 FAIL/known-risk evidence로 기록한다. 과거 commit까지 물리적으로 제거해야 하는 보안·법률 사유가 있으면 위 rewrite 절차를 독립 작업으로 연다.

## 12. 수동 작업 체크리스트

1. build identity와 exact root를 기록한다.
2. 관련 regression oracle과 독립 gate를 실행하고 PASS/FAIL/UNKNOWN을 구분한다.
3. FAIL/UNKNOWN이면 current/Downloads 실행을 차단한다.
4. 비실행 evidence manifest를 payload 밖에 보존한다.
5. exact-root fence와 sibling 보호를 확인한다.
6. 해당 payload 전체만 삭제하고 실행 파일 0개를 재검사한다.
7. 검증된 별도 정상 build만 rollback한다. 없으면 current를 비워 둔다.
8. 수정은 source에서 수행하고 모든 oracle을 다시 통과한 새 identity로 새로 빌드한다.
9. 최종 push 전 `.gitignore`, tracked tree, `origin/main`, 모든 active branch/tag tree와 release asset의 prohibited/unknown payload가 0인지 확인한다.
10. history rewrite/force-push가 필요해 보여도 exact audit·backup·collaborator re-clone 영향 승인이 없으면 중단한다.

이 체크리스트를 완료하지 않은 build는 사용자에게 전달하거나 실행을 권하지 않는다.
