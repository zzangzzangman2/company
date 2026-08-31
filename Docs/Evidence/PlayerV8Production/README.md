# Player V8 production cutover evidence

Validated on 2026-08-31 with Unity `6000.3.21f1` in a hidden Windows Player using actual
Direct3D 11.0 feature level 11.1.

- contract: `FC-PLAYER-V8-PRODUCTION-PRESENTATION-V1`
- visible Player: approved V8 one-package Humanoid only
- scale / standing height: `1.024378657` / `1.857258558`
- authored walk / stride: `1.4 s` / `0.7950477`
- workstation sets: `4`
- directional desk / chair: `4 / 4`; legacy flip `0`
- physical mesh axes / projected tile basis: `90 degrees` / `160,80|-160,80`
- maximum authoritative tile-corner error: `0.0003px`
- seated phase: `Working`
- left / right knee bend: `107.45 degrees / 113.16 degrees`
- actor-to-chair seated offset: `0.13001`
- visible retired Player/workstation renderers: `0`
- result: `FAMILY_COMPANY_OFFICE_V31_WORKSTATION_VISUAL: PASS`

The QA intentionally waits `0.65 s` after entering `Working`, longer than the production `0.42 s`
sit blend, before checking the knees and chair offset and taking the screenshot. This prevents a
fast hidden Player from capturing the character mid-descent.

Screenshot: `player-v8-production-v31-seated-d3d11.png`, SHA-256
`5B81CB6D2F07A699A184C74A9CA0E08BE47605ADD9DAA8110E21B271A20B8826`.
