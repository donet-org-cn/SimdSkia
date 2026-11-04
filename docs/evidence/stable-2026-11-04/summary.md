| Scene | DPI | Engine | ms/frame | CPU ms/frame | KiB/frame | WS MiB | Private MiB | Retained managed MiB |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| controls | 1 | after | 0.525 | 0.526 | 5.37 | 110.4 | 71.2 | 20.0 |
| controls | 1 | default | 0.842 | 0.844 | 5.31 | 91.3 | 47.4 | 2.3 |
| controls | 1 | reference | 3.208 | 3.208 | 5.48 | 80.2 | 36.5 | 2.3 |
| controls | 1.5 | after | 1.255 | 1.255 | 5.37 | 114.9 | 75.7 | 30.5 |
| controls | 1.5 | default | 1.591 | 1.583 | 5.31 | 95.0 | 51.1 | 2.3 |
| controls | 1.5 | reference | 6.854 | 6.839 | 5.48 | 84.1 | 40.5 | 2.3 |
| scroll | 1 | after | 0.332 | 0.365 | 3.17 | 98.5 | 64.8 | 15.8 |
| scroll | 1 | default | 0.352 | 0.375 | 5.03 | 84.0 | 45.1 | 1.4 |
| scroll | 1 | reference | 0.357 | 0.375 | 5.03 | 84.0 | 45.1 | 1.4 |

Pixel quality vs official renderer:
- controls DPI 1: FAIL, max 96, RMSE 2.408; before/after exact: not supplied
- controls DPI 1.5: FAIL, max 117, RMSE 2.321; before/after exact: not supplied
- scroll DPI 1: FAIL, max 75, RMSE 2.591; before/after exact: not supplied
