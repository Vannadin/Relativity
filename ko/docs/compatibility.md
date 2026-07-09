# Relativity — 모드 호환성 고려사항

> 이 레이어가 반드시 고려해야 하는 KSP 모드 전체 목록 — 강한 의존성, 통합 대상, 확장점
> 소비자, 그리고 해롭지는 않지만 확인해 둘 만한 상호작용을 담습니다. 상호 참조: [design.md](design.md) §3/§4,
> [dashboard.md](dashboard.md) §4/§5. **(검증됨)** 표시가 붙은 항목은 이 프로젝트에서 소스와 대조해
> 확인한 것이며(커밋 SHA는 §9), 나머지는 통합 시점에 확인할 상호작용 고려사항입니다.

**상태 범례.** `DEPEND` = 강한 의존성 · `INTEGRATE` = 우리가 훅을 건다 · `COORDINATE` = 이중 적용을
피한다 · `EXTENSION` = 우리 `WarpFlag`에 꽂힌다 · `COMPATIBLE` = 나란히 돌아가며 작업 불필요 ·
`CONSIDER` = 충돌은 없으나 확인 필요 · `AGNOSTIC` = 결합 없음.

---

## 1. 핵심 상호작용 (반드시 처리)

| 모드 | 설명 | 상호작용 / 상태 |
|-----|------------|----------------------|
| **Principia** *(검증됨)* | 뉴턴 n-체 적분기, 무게중심 관성 프레임 | `INTEGRATE`/`COMPATIBLE`. 우리는 *힘*과 *비율*만 조절하고 적분기는 절대 건드리지 않습니다. 힘 훅은 Principia의 stage-7 `FashionablyLate` 집계 전에 `part.force`를 씁니다. 무게중심 속도는 공짜로 제공됩니다. **규칙: 함선을 직접 이동시키거나 궤도를 직접 다시 쓰지 말 것** (Principia FAQ가 이를 비호환으로 명시). 대표적인 호환성 주장입니다. |
| **Kerbalism** *(검증됨)* | 생명 유지 + 승무원 방사선, 자체 자원 시뮬레이션 | `INTEGRATE`. 자체 상대론적 시간 지연은 **v3.x에서 제거**되었습니다. 비율 조정 API가 없으므로 → `VesselResources.Sync`/`Profile.Execute`에 공급되는 `elapsed_s`를 ×1/γ로 Harmony 패치합니다. **방사선은 제외**합니다(선량은 좌표 시간 유지, §4). 선량을 모델링하는 유일한 프레임워크이므로 → 대시보드의 선량 행은 이 모드의 존재 여부에 따라 게이팅됩니다. 내부 구현이 유동적이므로(Kerbalism 4에서 폐기 예정) → 버전 고정 + 페일세이프. |
| **Persistent Thrust (PT)** *(검증됨)* | 언로드/워프 중인 함선에 엔진 추력을 적용 | `INTEGRATE`. 백그라운드/워프 추력은 **단일 병목 지점** `OrbitExtensions.Perturb(Orbit, Vector3d deltaVV, double UT)`를 통한 직접적인 궤도 편집입니다 — 언로드 경로(`VesselData.cs:165`)와 워프 하 로드 경로(`PersistentEngine.cs:507`) 양쪽에서 호출됩니다. `ref Vector3d deltaVV *= 1/γ³`를 적용하고 β를 `orbit.getOrbitalVelocityAtUT(UT).magnitude/C`로 구하는 Harmony **prefix** 하나가 양쪽을 모두 커버합니다. 실시간 추력은 이미 우리 힘 훅을 거칩니다(이중 계산 없음). PT는 Kerbalism이 있으면 백그라운드를 그쪽으로 자체 위임하며, Principia 하에서는 사실상 무의미할 가능성이 큽니다(궤도 편집이 덮어써짐). WarpFlag로 게이팅하지 **말 것**(PT는 진짜 추력임). 자세한 내용은 §9. |
| **HarmonyKSP (Harmony 2)** *(검증됨)* | 런타임 패칭 라이브러리 | `DEPEND`. 힘 훅(전략 B), Kerbalism `elapsed_s` 패치, PT `Perturb` 패치, LS 어댑터가 모두 이것을 필요로 합니다. KSP에 함께 패키징된 공용 사본에 의존하고 — 우리 자체 사본을 **번들하지 말 것**. |

## 2. 생명 유지 프레임워크 (자원 절반, §2.2) — *전부 검증됨*

아래 넷 중 방사선을 모델링하는 것은 없으며(그건 Kerbalism 전용으로 남습니다), 공개 소비율
API를 노출하는 것도 없습니다 — 모든 훅은 내부 구현에 대한 Harmony이며, 함선 단위이고, 페일소프트합니다. 정확한 대상은 §9.

| 모드 | 설명 · 라이선스 | 상호작용 / 상태 |
|-----|----------------------|----------------------|
| **Kerbalism** | (§1 참조) | `INTEGRATE` — 주 자원 경로(`elapsed_s`). |
| **Snacks!** | 함선 LS (Snacks/Soil/FreshAir/Stress) · MIT | `INTEGRATE` — 로드/언로드가 하나의 경로를 공유합니다. prefix `SnacksScenario.runSnackCycle(Vessel, ref double elapsedTime)` → `/γ`. 비-Kerbalism 집합 중 가장 깔끔합니다. 유동적(private 코루틴). |
| **USI-LS** (MKS/USI) | 보급품/EC 기반 LS · GPLv3 (코드) | `INTEGRATE` — **예외: 로드 상태에서만 소비**(백그라운드 소모를 전혀 하지 않음). 로드 시 = postfix `ModuleLifeSupportSystem.GetDeltaTime()` ×1/γ (쉬움). 언로드 시에는 UT 타임스탬프 기반 고갈 검사에 공급할 *지속적인 고유 시간 적분값*이 필요한데 — 이는 "경과 시간을 스케일한다"는 패턴과는 본질적으로 다릅니다. |
| **TAC-LS** | Food/Water/O₂/EC + 폐기물 · CC BY-NC-SA 4.0 | `INTEGRATE` — 로드/백그라운드가 하나의 경로를 공유합니다. 4개의 private `Consume*` 메서드의 `deltaTime` 지역 변수를 ×1/γ로 transpile합니다. 백그라운드 소모는 별도의 BackgroundResources DLL이 설치된 경우에만. 유지 관리되는 포크 = **KSP-RO/TacLifeSupport (JPLRepo)**. "linuxgurugamer 포크"는 존재하지 않습니다. |
| **Community Resource Pack (CRP)** | 자원 *정의*만 — **코드 전무** | `COMPATIBLE` — 훅 걸 것이 없습니다. stock/CRP 경로를 위한 자원 이름 집합일 뿐입니다(Food/Water/Oxygen/Supplies/Fertilizer/Mulch/Waste/…). 이름은 의존성이 아니라 설정 가능한 문자열 집합으로 유지합니다. |
| *(아무것도 설치 안 됨)* | 순수 stock, LS 없음 | `AGNOSTIC` — 스케일할 것이 없습니다. 추력 + 대시보드 + 플래너 + 승무원 시계는 여전히 작동합니다. |

EC를 어떻게 다룰지는 Snacks/USI/TAC에서 **의도적인 설계 결정**입니다 — 승무원 지원용 EC는 동일한
경과 시간 레버를 공유하기에 자유롭게 스케일됩니다. 이를 고유 시간으로 볼지(스케일) 기계 동작으로 볼지(스케일 안 함) 결정하고 테스트하세요.

## 3. 추진 (함선이 상대론적 β에 도달하기 전까지 이 모드는 비활성)

| 모드 | 설명 | 상호작용 / 상태 |
|-----|------------|----------------------|
| **KSP Interstellar Extended (KSPIE)** *(검증됨)* | 토치/핵융합/반물질/워프 드라이브 | `COORDINATE` — **중첩:** 이 모드의 Daedalus 엔진은 이미 γ⁻² 추력 감쇠를 적용합니다(엔진 고유). 시간 지연의 이중 적용을 피하려면 이를 감지해야 합니다. 이 모드의 `ModuleEnginesWarp`/Alcubierre/PhotonSail은 `Orbit.Perturb`(자체 확장, PT와 동일한 알고리즘)로 추력을 냅니다. |
| **Far Future Technologies (FFT)** | 핵융합/반물질/펄스 토치 드라이브 | `COMPATIBLE` — 높은 β에 도달하는 자연스러운 드라이브 *공급원*이며, 충돌할 자체 상대론 처리가 없습니다. |
| **Near Future Propulsion** | 이온/플라스마/MPD (대부분 준상대론적) | `COMPATIBLE` — 무해합니다. β_min에 거의 도달하지 않습니다. |

## 4. 워프 / FTL — `WarpFlag` 확장점 (§2.6 ii) — *전부 검증됨*

셋 모두 힘이 아니라 **stock 궤도 상태 벡터를 다시 씀**(`UpdateFromStateVectors`)으로써 함선을
이동시키므로 — 우리 훅은 워프 운동을 결코 보지 못하며, 셋 모두 그 자체로 Principia와 비호환입니다.
이 모드들은 우리를 알지 못하므로 **감지는 우리 쪽 몫**입니다(모듈 이름 조회 + 아래 멤버 읽기).
`WarpFlag`는 미래의 워프 모드가 직접 올릴 수도 있는, 앞을 내다본 확장점으로 남습니다.

| 모드 | 설명 · 라이선스 | 상호작용 / 상태 |
|-----|----------------------|----------------------|
| **Blueshift** | 워프 엔진/링 · GPL-3.0 (코드), 아트 ARR | `EXTENSION` — 모듈 `WBIWarpEngine`를 감지합니다. 공개 `warpSpeed`(β, 단위 c)를 읽거나 `WBIWarpEngine.onWarpEngineStart`/`onWarpEngineShutdown`를 구독 → `WarpFlag`를 올리고 WARP 패널에 β를 표시합니다. |
| **KSPIE warp (Alcubierre)** | KSPIE Alcubierre 드라이브 · 커스텀 KSPIE 라이선스 | `EXTENSION` — `AlcubierreDrive`를 감지합니다. 공개 `IsEnabled`(bool) + `IsCharging`(스풀업) + `warpEngineThrottle`(β, 단위 c)를 읽습니다. |
| **WarpThrust** | **FTL 아님** — 준광속 지속 추력(PT 클론) · MIT | `CONSIDER` (충돌, **`WarpFlag` 아님**) — `Orbit.Perturb`를 통한 진짜 준광속 Δv이므로 `WarpFlag`를 올려서는 안 됩니다(우리 레이어를 억제하는 것은 잘못됨). 대신 이것은 추력/ISP 필드 충돌입니다 — 엔진 ISP/`maxFuelFlow`/스로틀을 실시간으로 변경합니다. 상태는 모두 private입니다(감지는 리플렉션). 공존하거나(워프 중 추력은 그쪽이 소유하게 두고, 우리는 승무원 자원을 지연시킴) 상호 배타적으로 문서화합니다. |

## 5. 물리 / 프레임

| 모드 | 설명 | 상호작용 / 상태 |
|-----|------------|----------------------|
| **SigmaBinary** | 쌍성 / 다중성 시스템 | `COMPATIBLE` — 무게중심 프레임 논리가 유지됩니다. stock와 Principia 프로파일 양쪽에서 무난히 돌아갑니다. |
| **Kopernicus** (행성 팩들의 엔진) | 항성/행성 시스템 로더 | `AGNOSTIC` — 우리는 행성 데이터를 읽지 않습니다. 플래너의 선택적 천체-거리 드롭다운이 천체를 조회할 뿐입니다. |

## 6. 타임워프 & 조종 — *전부 검증됨*

**핵심 발견.** MechJeb과 kOS는 달성 가능한 선회율을 KSP의 최대 각속도 클램프가 아니라
**토크 / 관성 모멘트**에서 유도합니다(둘 다 클램프를 읽는 곳이 전혀 없음을 grep으로 확인). 그러므로
자세 ×1/γ (§2.7)를 숨겨진 각속도 클램프가 아니라 **`ITorqueProvider.GetPotentialTorque`를 통한
토크 감소**로 노출하세요 — 그러면 두 오토파일럿 모두 이를 실시간으로 읽고 자기 일관성을 유지합니다.
숨겨진 클램프도 작동은 하지만(그들의 PID가 안정화됨) 오버슈트 예측이 약간 낙관적으로 남습니다.

GPL-3.0 코드(BTW/MechJeb/kOS) → 이벤트/stock 인터페이스만을 통한 소프트 의존성으로 처리합니다. 그들의 코드를 절대 복사하지 마세요.

| 모드 | 설명 · 라이선스 | 상호작용 / 상태 |
|-----|----------------------|----------------------|
| **Time Control** | 감속/초고속/커스텀 레일 · **라이선스 미확인**(LICENSE 비어 있음) | `CONSIDER` — `TimeWarp.warpRates[]`를 리사이즈/교체하고 `Time.fixedDeltaTime`을 변경합니다. 우리 누산기/따라잡기는 stock 비율 테이블을 인덱싱하지 말고 반드시 **실시간 `fixedDeltaTime` + 실제 UT 델타**를 읽어야 합니다. 소프트 의존 반응을 위해 발견 가능한 이벤트(`OnTimeControlHyperWarp*`, `…FixedDeltaTimeChanged`)를 노출합니다. |
| **Better Time Warp** | 커스텀 비율 테이블 + 무손실 물리 · GPL-3.0 | `CONSIDER` — 비율 배열을 교체합니다. 무손실 물리는 `GameSettings.PHYSICS_FRAME_DT_LIMIT`를 변경하고 지속시킵니다(상수로 읽지 말 것). 동일한 누산기 규칙. stock `onTimeWarpRateChanged`를 구독합니다. |
| **MechJeb** | 오토파일럿 / 기동 실행 · GPL-3.0 | `CONSIDER` — 노드 점화가 피드백 게이팅되므로(*측정된* 정렬을 기다림) 선회가 느려지면 ALIGNING이 길어질 뿐입니다 — 잘못된 선행 시간 없이 우아하게 처리됩니다. 핵심 발견 참조: 토크 기반 감소를 선호. |
| **kOS** | 스크립트 조종 · GPL-3.0 | `CONSIDER` — cooked 스티어링은 측정된 `angularVelocity`에 대한 폐루프입니다(우아함). 스크립트가 `ANGLEERROR`를 폴링하는 대신 `WAIT n.` 선회 *마감 시각*을 하드코딩할 때만 깨집니다 → 사용자용 README 노트. |
| **Kerbal Alarm Clock** | 알람 · MIT | `AGNOSTIC` — 비율 테이블을 읽고 stock `TimeWarp.SetRate`를 호출할 뿐입니다. 새로운 메커니즘을 도입하지 않습니다. |

## 7. 행성 팩 — 전부 `AGNOSTIC`

이 레이어는 행성 팩에 무관합니다: **어떤 팩의 데이터나 이름에도 의존하지 않습니다**(핵심 프로젝트 규칙).
Galaxies Unbound, Extrasolar, Kcalbeloh, Beyond Home, GEP, RSS/RO 등 — 어느 것도 작업을 유발하지 않습니다.
플래너의 선택적 "천체 선택" 거리 모드만이 존재하는 천체를 읽습니다.

## 8. 배포 / 툴링

| 도구 | 상호작용 / 상태 |
|------|----------------------|
| **CKAN** | Harmony에 `DEPEND`를 선언합니다. Kerbalism / Principia / 드라이브 모드(FFT/KSPIE) / PT를 `recommends`/`suggests`합니다. |
| **ModuleManager** | `.cfg` 패치(예: 어댑터 토글)를 배포할 때만 필요합니다. `.cfg` 튜너블(design.md §5)은 어차피 `GameDatabase`를 통해 로드됩니다. |

## 9. 빌드 레퍼런스 — 검증된 어댑터 대상

이번 사이클에 소스로부터 확인한 정확한 훅입니다(빌드 시 고정된 SHA에 대해 시그니처를 검증할 것.
대상이 없으면 모두 페일소프트). 이 중 공개 API를 가진 것은 없습니다 — Harmony가 전 구간에 필요합니다.

| 모드 @ SHA | 훅 타깃 | 조치 |
|-----------|-------------|--------|
| **PersistentThrust** `sswelm` @ `8f2fbb4` (MIT, v1.8.0.0) | prefix `PersistentThrust.OrbitExtensions.Perturb(Orbit, Vector3d deltaVV, double UT)` | `ref deltaVV /= γ³`; γ는 `orbit.getOrbitalVelocityAtUT(UT).magnitude/C`로 구함. β<β_min이면 건너뛰고 β_sane로 클램프. 언로드(`VesselData.cs:165`) + 로드-워프(`PersistentEngine.cs:507`)를 커버. |
| **Snacks!** `Angel-125` @ `4242f92` (MIT) | prefix `SnacksScenario.runSnackCycle(Vessel, ref double elapsedTime)` | `elapsedTime /= γ` (함선 단위). 대안: `ProcessResources` 오버라이드. `RunSnackCyleImmediately`(전역)는 패치하지 말 것. |
| **USI-LS** `UmbraSpaceIndustries` @ `c842b85` (GPLv3) | postfix `ModuleLifeSupportSystem.GetDeltaTime()` (로드) | `__result *= 1/γ`. 언로드: 스케일할 소모가 없음 → 고갈 타임스탬프 검사를 위해 지속적인 고유 시간 적분값을 유지. |
| **TAC-LS** `KSP-RO` @ `f5dd566` (CC BY-NC-SA) | transpile `Consume{Food,Water,Oxygen,Electricity}`의 `deltaTime` 지역 변수 | 각 `Math.Min(...)` 뒤에 `deltaTime *= 1/γ`. 로드 + 백그라운드 커버. 언로드에는 BackgroundResources DLL 필요. |
| **Kerbalism** v3.x (§1 참조) | `VesselResources.Sync`/`Profile.Execute`에서 `elapsed_s` 패치 | `× √(1−β²)`; 방사선 Rule은 제외. |
| **Blueshift** `Angel-125` @ `68eb6d6e` (GPL-3.0) | `WBIWarpEngine.warpSpeed` / `onWarpEngineStart`·`onWarpEngineShutdown` 읽기 | 워프 감지 → `WarpFlag` 올림. `warpSpeed`(c) 표시. 감지만, 패치 없음. |
| **KSPIE** `sswelm` @ `69166e2b` (커스텀) | `AlcubierreDrive.IsEnabled` / `warpEngineThrottle` 읽기 | 워프 감지 → `WarpFlag`; β 표시. 이중 시간 지연을 피하기 위해 Daedalus γ⁻²도 감지. |
| **WarpThrust** `PEKKA-Space` @ `6cf264f8` (MIT) | (공개 상태 없음. `active`/`timeWarp`를 리플렉션) | `WarpFlag`를 올리지 말 것. 공존/충돌 케이스 — 선택적 보조 `Perturb` 패치. |

---

## 의미 있게 지원할 수 없는 것

- **Principia 하에서의 직접 궤도 편집 운동** — 함선을 궤도 재작성으로 이동시키는 모든 모드(PT
  계열, Blueshift, KSPIE warp, WarpThrust)는 우리와 무관하게 이미 Principia와 충돌합니다.
  "그 모드 + Principia"는 실질적인 조합이 아니므로, 우리 PT 어댑터는 **stock** 프로파일만 대상으로 합니다.
- **Principia 자체의 비행 계획 번** — Principia 적분기 내의 고유력 모델이며 stock 추력과는 다른
  경로입니다. 백그라운드 추력 어댑터의 범위 밖입니다.
