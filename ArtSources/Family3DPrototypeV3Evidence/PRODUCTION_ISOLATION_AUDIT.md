# Family 3D Motion Lab V3 — production isolation audit

Canonical repository used directly:

`C:\Users\godho\Documents\Codex\fc_agents\integration_p0`

The existing dirty working tree was preserved. No reset, clean, checkout, or stash was used.

## External pre/post comparison

Before the first V3 build and after the final Run3 build/QA:

- non-experimental `git status --porcelain=v1 -uall` entries: `174` before, `174` after;
- non-experimental entries added by this work: `0`;
- non-experimental entries removed by this work: `0`;
- production/default executable hash changed: `no`;
- Downloads executable hash changed: `no`.

Protected executables remained byte-for-byte and timestamp-for-timestamp unchanged:

| Path | Bytes | SHA-256 |
|---|---:|---|
| `Builds/Windows/FamilyCompany_Playtest/FamilyCompany.exe` | 667,136 | `48EFAB523AA684C653BD1254A6962D3410127B5C02DC1310F6F16F4810666556` |
| `C:/Users/godho/Downloads/FamilyCompany_Playtest/FamilyCompany.exe` | 667,136 | `48EFAB523AA684C653BD1254A6962D3410127B5C02DC1310F6F16F4810666556` |

## Builder safeguards

- output paths outside `Artifacts/Family3DPrototypeV3/` are rejected;
- an interactive build is refused while an open scene is dirty;
- only the explicit experimental scene is passed to `BuildPipeline.BuildPlayer`;
- global `AssetDatabase.SaveAssets()` is not called;
- generated experimental materials use targeted `AssetDatabase.SaveAssetIfDirty`;
- the build receipt intentionally says production mutation is not asserted internally and relies on
  this external pre/post comparison.

No production family sprite, production scene, default build, Downloads build, commit, or push was
changed.
