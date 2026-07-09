# Relativity — 대시보드 UX / UI 스펙

> 준광속 relativity 메커니즘([design.md](design.md))의 표시 스펙. 그 문서와 함께 이관·디브랜딩되었다
> (design.md → Provenance 참고).

**이 문서의 정체.** 이 메커니즘의 정체성은 곧 그 표시(readout)다("플레이어는 β가 오르면서 nominal
thrust와 effective thrust가 벌어지는 것을 본다", design.md §1). 그래서 대시보드는 장식이 아니라 일급
설계 대상이다. 이 문서는 초안 `RelativityDashboard.cs` 스텁(`src/`)을 확장하기 위한 브리프다. 범위는
비행 중 HUD 하나뿐이며 — 셰이더/비주얼 레이어는 다루지 않는다(design.md §2.5).

**이 문서가 해소하는 설계 긴장.** design.md §0은 *체감* 메커니즘에는 수식이 필요 없다고 말하고, §1은
분할 표시가 곧 정체성이라고 말한다. 답은 **2-모드 대시보드**다. 수식 없는 Simple 모드와 전체 Expert
모드.

---

## 1. 표시 항목 집합

| Row | 표시 내용 | Source | Mode |
|-----|-------|--------|------|
| **Speed** | `c` 대비 비율로서의 `β`, light-wall 게이지와 함께 | `RelativityState.Beta` | both |
| **Thrust** | effective / nominal kN 및 % (`1/γ³`) | `ThrustFactor(γ)` | both |
| **Brake authority** | retrograde effective thrust % + "⚠ decel now" 신호 | `1/γ³` (방향 무관) + 선택적 planner | both |
| **Mission clock** | coordinate(UT) 경과 시간 | game UT | both |
| **Crew clock** | proper time 경과 + mission 대비 Δ | vessel별 `∫dt/γ` 누산기 | both |
| **γ** | Lorentz 인자 | `RelativityState.Gamma` | Expert |
| **Life support** | 소비율 `×1/γ` | `ResourceFactor(γ)` | Expert |
| **Radiation dose** | `×1.00 (not dilated)` — 대비 row | 상수 1.0 | Expert |
| **Turn rate** | 자세/회전 권한 `×1/γ` (회전도 느려진다) | `AttitudeFactor(γ)` | Expert |

**radiation contrast row**는 의도적이다. `dose ×1.00`을 `life support ×0.39` 바로 옆에 두면 design.md
§4의 결론을 가르친다 — 빠른 승무원은 덜 늙지만 같은 양의 dose를 흡수하므로, **결속 제약은 굶주림이 아니라
방사선**이라는 것이다. 절대 dose는 life-support mod 자체 UI(예: Kerbalism)에 남겨두고, 우리는 배수 대비만
보여준다.

**dose row는 Kerbalism이 설치되어 있을 때만 나타난다** — Kerbalism은 승무원 방사선을 모델링하는 유일한
프레임워크이므로, 그것이 없으면 대비할 dose가 없고 이 row는 숨겨진다(나머지 Expert 표시는 그대로 나온다).
이것은 특정 mod의 존재 여부에 따라 게이트되는 유일한 row다.

---

## 2. light-wall speed 게이지

**0에서 1 c까지의 선형 바에, 1.0 지점에 하드 월 마커**를 두고, 숫자 `0.923 c`를 함께 표시한다. ~0.9c
위에서는 마지막 세그먼트가 **비선형 꼬리(non-linear tail)**로 채워져서 `c`로의 마지막 접근이 눈에 띄게
결코 완결되지 않도록 한다 — rapidity 없이도 "채울 수 없는 점근선"이 한눈에 읽힌다. (rapidity 스케일링도
검토했으나 덜 직관적이라 기각했다.)

---

## 3. 두 개의 시계

- **Crew(proper) clock**은 `τ = ∫ dt/γ`를 **vessel 발사 시점부터** 적분하므로 진짜 승무원 나이
  주행계이며, coordinate(UT) mission clock 옆에 그 차이와 함께 표시된다.
- 이 격차는 **영구적**이다 — 감속해도 결코 되돌려 따라잡지 못한다(쌍둥이 역설의 귀결). 누산기는 결코
  리셋되지 않는다.
- 세이브에 **vessel별로** 저장된다. 표시는 활성 vessel을 따른다.
- **언로드 상태에서도 진행된다.** 캐치업 시(백그라운드 vessel로 복귀할 때), 경과한 언로드 구간을 vessel의
  백그라운드 β(Principia 또는 on-rails 속도)를 써서 `τ += Δt/γ(β)`로 적분한다. 그래서 복귀 시 주행계가
  정확하다 — vessel이 로드되어 있는 동안만이 아니라. 항행 중인 vessel의 β는 그 구간 내내 거의 일정하다
  (design.md §2.6 i, §6).
- **표시 전용 / 장부 기록.** 이것은 proper time을 적분하고 보여줄 뿐, 게임의 UT나 time-warp를 조작하지
  **않는다**. 시계 *조작*은 뒤로 미뤘고, 수동 주행계는 호환되며 지금 출시해도 안전하다.
- warp 중에는 `γ = 1`이므로 누산기는 UT와 **1:1**로 진행한다(새 격차 없음). 백그라운드에서 계속 돌지만
  warp-mode 패널(§5)에서는 숨겨진다.

---

## 4. Brake-authority 신호

`1/γ³` 페널티는 **방향 무관**이다 — `c` 근처에서 제동은 가속만큼이나 무력하므로, 도착 감속은 터무니없이
일찍 시작해야 한다(design.md §0). 여기에 겹쳐서: **retrograde를 향해 도는 것 자체가 `×1/γ`로 느려진다**
(자세 제어, design.md §2.7). 그래서 선행 시간은 방향 전환(flip)까지 포함해야 한다.

- **Brake authority %** = retrograde effective/nominal thrust = `1/γ³` (같은 인자).
- **`⚠ decel now`**는 **turnover point**에서 발화한다 — 목표까지 남은 거리가 정지 상태까지 제동하는 데
  필요한 만큼으로, 안전 마진을 더해 줄어들었을 때다.
  ```
  fire when   remaining ≤ (d_brake + d_flip) · (1 + margin)      margin default ~5%
  d_brake = (c²/α)·(γ − 1)      coordinate distance to decelerate β → rest  (= the accel-phase dA, planner.md §3.2)
  d_flip  = β·c·(γ · t_turn0)   distance coasted while flipping 180°  (t_turn0 = at-rest flip time; ×γ from attitude ×1/γ, §2.7)
  ```
  성간 스케일에서 `d_flip`은 `d_brake`에 비해 무시할 만하므로, 트리거는 사실상 `remaining ≤
  d_brake·(1+margin)`이다 — 예를 들어 0.9c / 1.5 g에서는 **~0.84 ly 밖**이다. *flip time*은 여전히
  운영상 중요하지만(도착 전에 회전을 마쳐야 한다), 그 거리는 미미하다.
  **"remaining"이 어디서 오는가.** VAB **planner**(planner.md)가 설정한 목표나 플레이어가 고른 목적지는
  정확한 거리를 준다 → 정확한 트리거. 목표가 없으면 레이어는 조잡한 속도 휴리스틱으로 물러설 수밖에 없다 —
  목적지가 얼마나 먼지 모르기 때문이다(design.md §3). 유일한 제동 대상이 항성의 km/s 고유 속도일 때는 휴면
  상태다(비상대론적, `d_brake` ≈ 0).

이 단일 신호는 메커니즘이 아니면 초래할 "왜 멈출 수가 없지?" 소프트락을 막아준다.

---

## 5. 상태 & 레이아웃

가시성은 design.md §2.6 가드를 따른다. 대시보드는 레이어가 활성화될 때(`β > β_min`) **자동으로 나타나고**
그 외에는 숨겨진다. ApplicationLauncher 토글로 강제 표시(계획용)하거나 고정/숨김할 수 있다. 창은 드래그
가능하다.

스텁은 현재 `st.Active`가 false일 때마다 창을 숨기는데 — 이는 "준상대론적"과 "warp 중"을 한데 뭉뚱그린다.
확장은 이 둘을 **분리**해야 한다.

| Condition | Panel |
|-----------|-------|
| `β > β_min`, warp/글리치 아님 | **full dashboard** (Simple 또는 Expert) |
| **warp/jump 중** (WarpFlag up) | **collapsed WARP panel** — 속도를 `c`-배수로만 표시 |
| `β ≤ β_min` (준상대론적) | 숨김 (고정 시 `off (sub-relativistic)`) |
| 비현실적 β (kraken, §2.6 iii) | `disabled — implausible β` |

### Active (준광속 순항) — Simple
```
┌─ RELATIVITY ───────────── ● ACTIVE ─┐
│ Speed   0.923 c  ▓▓▓▓▓▓▓▓░│1c         │
│ Thrust  8.9 / 154 kN  ( 5.8% )       │
│ Brake authority  5.8%   ⚠ decel now  │
│ Mission   12.4 yr                    │
│ Crew       4.8 yr   (−7.6)           │
└───────────────────────────────────────┘
```
**Expert**는 추가로: `γ = 2.59`, `life support ×0.39`, `dose ×1.00 (not dilated)`, `turn rate ×0.39`.

### Warp 감지 (WarpFlag up) — collapsed
```
┌─ WARP ───────────── ◆ ─┐
│ Speed   23.4 c        │
└───────────────────────┘
```
relativity 메커니즘 row는 모두 사라진다(warp 하에서 γ/thrust/supplies/dose/brake는 항등이거나 NaN).
`c`-배수 warp 속도만 남는다. **여기서의 속도는 선택적 warp-speed provider**(WarpFlag를 올린 바로 그 warp
mod)**에서 오며**, relativity 레이어의 물리적 β가 **아니다** — 그 β는 설계상 warp 하에서 항등이다.
provider가 등록되지 않았으면 속도 값 없이 패널만 표시한다(또는 그냥 `WARP`).

---

## 6. 구현 브리프 — `RelativityDashboard.cs` 확장

스텁은 `st = RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v))`를 계산하고 `st.Active`일 때
β/γ/thrust%/supply%를 그린다. 이를 확장한다.

1. **warp를 먼저 라우팅한다.** `WarpFlag.IsWarpingOrJumping(v)`이면 선택적 warp-speed provider를 써서
   collapsed WARP 패널을 그린다 — 등록된 warp mod가 무엇이든 그에 맞춰 읽기 경로를 **VERIFY**하라(특정
   플러그인에 대한 하드 레퍼런스가 아니라 제네릭 provider 훅). 여기서 속도로 `st`를 읽지 **말라**(설계상
   warp 하에서 항등이다).
2. **두 모드.** Simple/Expert 토글(창 헤더의 버튼 또는 launcher 메뉴). Simple = Speed, Thrust, Brake,
   두 시계. Expert는 γ, life-support `×1/γ`, dose `×1.00`을 추가한다.
3. **light-wall 게이지.** 평범한 `β` 레이블을 0→1 바 + 월 마커 + 비선형 꼬리로 교체한다(§2).
4. **두-시계 누산기.** vessel별 `τ += dt/γ` 적분기(γ≈1일 때는, warp 포함, 1:1로 진행). vessel의 세이브
   노드에 영속화한다 — `ProtoVessel`/`VesselModule` 영속화 훅을 **VERIFY**하라. UT, crew, Δ를 표시한다.
5. **Brake row.** `1/γ³`을 brake authority %로 표시한다. `⚠ decel now`를 있으면 planner 피드에, 없으면
   휴리스틱에 연결한다(§4).
6. **Kraken/비활성 텍스트.** 고정 상태일 때는 사라지는 대신 `off (sub-relativistic)`이나 `disabled —
   implausible β`를 표시해서, 레이어가 살아있지만 유휴 상태임을 플레이어가 알게 한다.

planet-pack DB / cfg 델타는 없다. 이것은 기존 force/resource 훅 위에 얹은 표시 + proper-time 누산기다.

## Related

- [design.md](design.md) — 이 대시보드가 표시하는 메커니즘 (§1 readout 정체성, §2.6 가드, §4
  radiation-vs-starvation)
- `RelativityDashboard.cs` (`src/`) — 이 브리프가 확장하는 스텁
