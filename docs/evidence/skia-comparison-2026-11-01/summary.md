| Scene | DPI | Engine | ms/frame | CPU ms/frame | KiB/frame | WS MiB | Private MiB | Retained managed MiB |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| controls | 1 | after | 0.549 | 0.552 | 5.37 | 109.8 | 71.1 | 19.9 |
| controls | 1 | default | 0.891 | 0.885 | 5.31 | 90.4 | 47.4 | 2.3 |
| controls | 1 | reference | 3.305 | 3.302 | 5.48 | 79.4 | 36.8 | 2.3 |
| controls | 1.5 | after | 1.353 | 1.359 | 5.37 | 112.1 | 73.5 | 30.5 |
| controls | 1.5 | default | 1.701 | 1.688 | 5.31 | 94.2 | 51.2 | 2.3 |
| controls | 1.5 | reference | 7.096 | 7.078 | 5.48 | 83.4 | 40.5 | 2.3 |
| scroll | 1 | after | 0.348 | 0.380 | 3.17 | 97.1 | 64.8 | 15.8 |
| scroll | 1 | default | 0.372 | 0.406 | 5.03 | 82.4 | 45.0 | 1.4 |
| scroll | 1 | reference | 0.369 | 0.401 | 5.03 | 82.3 | 44.9 | 1.4 |

Pixel quality vs official renderer:
- controls DPI 1: FAIL, max 96, RMSE 2.408; before/after exact: not supplied
- controls DPI 1.5: FAIL, max 117, RMSE 2.321; before/after exact: not supplied
- scroll DPI 1: FAIL, max 75, RMSE 2.591; before/after exact: not supplied
