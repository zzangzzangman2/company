# Player East Mixamo Trace — 6 Pose Contract

- Mixamo clip length: `1.366667s`
- detected left-contact phase zero: `0.296111s`
- KShopGo reference playback: `0.8s`, 30fps, samples `0/4/8/12/16/20`
- left leg: cyan; right leg: orange; east: +X
- production stride: `0.99380799` world unit; PPU `180`; visual scale `1.55`
- root advance per pose: `19.234993` source px
- computed maximum heel/toe contact drift: `0.765007px` (required `<=1px`)
- q0->q1 locks the heel; q1->q2->q3 locks the toe; q4 recovery; q5 low pass

| Pose | KShop ms | Support | Required event | Root px | Left H/A/T | Right H/A/T |
| --- | ---: | --- | --- | ---: | --- | --- |
| P0 | 0.0 | left | left contact / right toe | 0.000 | (150, 233)/(154, 222)/(164, 230) | (92, 225)/(98, 218)/(106, 233) |
| P1 | 133.3 | left | left load / right recovery | 19.235 | (131, 233)/(135, 223)/(145, 233) | (102, 219)/(106, 213)/(116, 222) |
| P2 | 266.7 | left | left terminal / right low pass | 38.470 | (112, 226)/(118, 219)/(126, 233) | (135, 226)/(139, 219)/(149, 229) |
| P3 | 400.0 | right | right contact / left toe | 57.705 | (92, 225)/(98, 218)/(106, 233) | (150, 233)/(154, 222)/(164, 230) |
| P4 | 533.3 | right | right load / left recovery | 76.940 | (102, 219)/(106, 213)/(116, 222) | (131, 233)/(135, 223)/(145, 233) |
| P5 | 666.7 | right | right terminal / left low pass | 96.175 | (135, 226)/(139, 219)/(149, 229) | (112, 226)/(118, 219)/(126, 233) |

Fail closed: no lower-body mirror, no shoe fragment move, no duplicated contact, and no art promotion before this owner/contact order passes in the east GIF.
