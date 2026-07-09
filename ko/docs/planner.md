# Relativity — VAB 항행 플래너 (설계 명세)

> `Relativity` mod의 설계 표면 중 하나로, [design.md](design.md)(메커니즘)와
> [dashboard.md](dashboard.md)(비행 중 HUD)와 나란히 놓인다. 이쪽은 **에디터 씬** 짝이다. VAB/SPH에서
> 배를 설계하면서, 발사하기도 전에 그 배의 ΔV와 가속도가 상대성 레이어 아래에서 실제로 어디까지 데려다줄
> 수 있는지를 미리 본다.

**정체.** 에디터 안에서 도는 플래너다. 배의 스톡 ΔV와 최대 가속도, 목표 거리, 비행 프로파일로부터
그 항행의 **도착 시각**(임무 시계 + 승무원 시계)과 **자원 소비량**을 계산한다. 비행 레이어가 적용하는
것과 *동일한* 특수상대성 물리(design.md §2.1/§2.2)를 쓰므로, 계획과 실제 비행이 어긋나지 않는다.

**여기 있어야 하는 이유.** 비행 대시보드는 날고 있는 *동안* 메커니즘을 보여주고, 플래너는 그 메커니즘에
*맞춰* 배를 설계하게 해준다. 플레이어가 엔진과 연료 탱크를 고르는 바로 그 순간에, 설계의 핵심 긴장
(design.md §0: "빨라지기는 어렵지만, 빠른 승무원은 더 오래 버틴다")에 답한다.

---

## 1. 입력

에디터에서 읽어온다 (모든 스톡/프레임워크 접점은 빌드 시점에 `// VERIFY:` 대상이다).

| Input | Source | Notes |
|-------|--------|-------|
| ΔV (ideal) | stock ΔV readout (`VesselDeltaV` / stage ΔV app) | 뉴턴식 `v_e·ln(MR)`. 상대론 매핑(§3)이 필요로 하는 값 그대로다. |
| Max acceleration `α` | Σ active-engine thrust / vessel mass | 이것이 배의 *고유(proper)* 가속도 `F/m`(§3)이다. MVP는 배를 하나의 항성간 스테이지로 취급하고, 다단 정교화는 뒤로 미룬다. |
| Onboard resource amounts | ship part resources | 소비량 / 부족분 점검(§4)용. |
| Nominal consumption rates | installed LS framework (Kerbalism / stock / CRP) | 가장 까다로운 소스이며 프레임워크마다 다르다. MVP는 승무원 수 × 커발 1인당 LS 소비율로 근사할 수 있다. 프레임워크별 `// VERIFY:`. |

## 2. 플레이어 조작

- **목표 거리.** **슬라이더 ↔ 수동 입력을 전환**하는 필드(스톡 KSP 입력 위젯 패턴)에 **ly / AU** 단위
  토글이 붙는다. 여기에 더해, 선택한 천체에서 거리를 채워주는 선택적 **"게임 천체 고르기"** 드롭다운
  (자동 모드)이 있다. 행성 팩이 설치돼 있을 때 쓰며, 행성에 구애받지 않는 수동 입력은 언제나 열려 있다.
  세 모드 모두 함께 나간다.
- **비행 프로파일 토글.**
  - **Rendezvous** (목적지에서 정지할 때까지 감속) — ΔV가 가속과 감속으로 나뉜다.
  - **Flyby** (편도, ΔV 전부를 가속에) — 감속 예산이 없다.

## 3. 모델 — 물리

`ΔV`를 스톡 이상 ΔV, `c`를 광속, `α = F/m`을 최대 (고유) 가속도라 하자.

### 3.1 ΔV로부터 순항 속도 (relativistic rocket equation)

스톡 ΔV는 `v_e·ln(MR)`인데, 특수상대성에서 이것은 누적된 rapidity 자체다. 따라서 도달 속도는

```
β = tanh(ΔV / c)
```

이것은 표준적인 **relativistic-rocket shorthand**이며, *플래너 추정치*로 쓴다. 솔직하게 밝혀둘 caveat이
둘 있다 (이것은 일부러 약속이 아니라 미리보기로 둔 것이다).
- KSP의 뉴턴식 이상 ΔV 적분 `v_e·ln(MR)`을 마치 `ln(MR)`이 rapidity인 것처럼 다룬다. 이것이 정확한 것은
  분사 자체가 상대론적일 때뿐이다. sub-relativistic 분사(현실적인 어떤 Isp든)에서는 참된 SR이 *조금 더 낮은*
  β에 도달하므로, 이 매핑은 높은 ΔV에서 다소 **낙관적**이다. 미리보기용으로는 괜찮다.
- 비행 레이어가 만들어내는 β와 정확히 같지는 **않다**. 비행 모델은 좌표 시간으로 추진제를 태우고 질량이
  줄면서 `α = F/m`이 올라가므로, 그 도달 β는 여기서 둔 이상화(일정한 α, 이상적 로켓) 아래에서만 `tanh(ΔV/c)`와
  일치한다. 플래너는 근사한 추정치로 여기되, 비행 중 수치가 똑같이 떨어질 거라는 보장으로 여기지는 말자.

ΔV 예산은 프로파일에 따라 나뉜다.

- **Flyby:** `β_cruise = tanh(ΔV / c)`.
- **Rendezvous:** `β_cruise = tanh(ΔV / (2c))` — ΔV의 절반이 가속, 절반이 감속에 쓰인다.

`γ_cruise = 1/√(1 − β_cruise²)`.

| ΔV | flyby β | rendezvous β |
|----|---------|--------------|
| 0.5 c | 0.462 | 0.245 |
| 1.0 c | 0.762 | 0.462 |
| 2.0 c | 0.964 | 0.762 |
| 3.0 c | 0.995 | 0.905 |

→ 빠른 *rendezvous* 순항에 도달하려면 flyby의 약 두 배에 달하는 ΔV가 든다. "브레이크가 추력만큼이나
힘없다"는 페널티(dashboard.md §4)가 설계 시점에 드러나는 셈이다.

### 3.2 가속 / 감속 구간 (일정 고유 가속도 `α`)

0에서 `β_cruise`까지의 쌍곡선 (일정 고유 가속도) 운동을 적분한다.

```
proper time    τ_a = ΔV_accel / α            (= (c/α)·atanh(β_cruise) when α is constant)
coordinate time t_a = (c/α)·β_cruise·γ_cruise
distance        d_a = (c²/α)·(γ_cruise − 1)
```

- **Flyby:** 가속 구간 하나 (`τ_a`, `t_a`, `d_a`를 한 번만 계산). 감속 없음.
- **Rendezvous:** 대칭인 감속 구간이 `τ_a`, `t_a`, `d_a`를 한 벌 더 더한다.

여기서 `α`는 요청한 그대로 중요하게 작용한다. 저추력 배는 순항 속도까지 올리는 데 더 오랜 시간(과 더 긴
거리)을 쓴다. 그만큼 항행이 길어지고 — 가속 중에도 승무원 시계가 돌기 때문에 — 자원 총량도 달라진다.

**Constant-α caveat.** `α = F/m`은 연소 중에 *일정하지 않다* — 질량이 줄고(비행 모델 아래에서는 `F`가 γ³로
깎인다), 그래서 단일 `α`는 1차 근사일 뿐이다. `τ_a = ΔV_accel/α` 항등식은 α가 일정할 때만 성립한다. `τ_a`는
가속 구간 ΔV로부터 직접 계산하고, α는 `t_a`/`d_a`의 *형태*를 잡는 데만 남겨두자. mass ratio가 큰 스테이지에서는
가속 구간 *거리* `d_a`가 큰 배수만큼 어긋날 수 있다 — 미리보기용으로는 받아들일 만하지만, 관성 항주 분할이
믿을 만해야 한다면 수치 적분하자.

### 3.3 관성 항주 구간과 총합

남은 관성 항주 거리 `D_coast = D − d_a − d_decel` (d_decel은 rendezvous면 d_a, flyby면 0).

```
coordinate time  t_c = D_coast / (β_cruise · c)
proper time      τ_c = t_c / γ_cruise
```

- **임무 (좌표) 시간**  `T = (1 or 2)·t_a + t_c`
- **승무원 (고유) 시간**  `τ = (1 or 2)·τ_a + τ_c`

**엣지 케이스 — 거리가 너무 짧아 순항 β에 못 미침** (`D_coast < 0`): 가속(+감속) 구간이 이미 거리 전체를
써버려서 배가 `β_cruise`에 도달하지 못한다. 플래너는 이를 표시하고("accel/decel-limited — cruise β not
reached") 3구간 모델 대신 전환점 궤적(중간점까지 가속한 뒤 감속)을 푼다. 이것은 brachistochrone
전이의 항성간 판본이다.

## 4. 자원 소비

비행 레이어는 온보드 소비를 ×1/γ로 스케일한다(design.md §2.2). 따라서 제외되지 않은 자원 `i`의 항행 총합은
단순히 이렇다.

```
consumed_i = base_rate_i × τ      (τ = crew/proper time, §3.3)
```

즉 승무원은 *자신이* 겪는 시간만큼만 소비한다. 자원별로 다음을 보여준다.

- **cruise rate** = `base_rate_i × (1/γ_cruise)` (빠른 동안 느려진 소비),
- **trip total** = `base_rate_i × τ`, 온보드 보유량과 대조,
- **trip total > onboard** 일 때 **부족 경고** ("supplies insufficient: short by X").

제외 항목은 design.md §2.2를 따른다. 엔진 추진제/산화제, ElectricCharge, 방사선 피폭량은 스케일되지
**않으며**, (항행이 다시 쓰지 않는 추진제를 빼면) 좌표 시간 총합으로 표시된다. 피폭량 대비(`×1.00`)도 여기서
드러내둘 값어치가 있다. *임무* 시간이 길어지면 승무원이 덜 늙어도 피폭은 더 쌓인다 — 설계의 "굶어 죽는 게
아니라 방사선"이라는 요점이, 설계 시점에 나타나는 것이다.

## 5. 출력 / 레이아웃

에디터 씬 창으로, **ApplicationLauncher** 버튼에서 연다 (VERIFY: launcher in editor). 일관성을 위해 비행
대시보드의 Simple/Expert 분할(dashboard.md)을 그대로 따른다.

**Simple** — 순항 `β` (light-wall 스타일), 임무 시계, 승무원 시계 + Δ, 그리고 자원별 "얼마나 버팀 / 얼마나
부족" 줄.
**Expert** 는 여기에 — `γ_cruise`, 가속/항주/감속 시간+거리 분해, 순항 소비율 `×1/γ`, 그리고 피폭 `×1.00`
대비 행을 더한다.

```
┌─ TRIP PLANNER ──────────────── VAB ─┐
│ Distance  4.3 ly   ◀━━━━●━━━▶  [ly]  │
│ Profile   ( Rendezvous ) Flyby       │
│ Cruise    0.462 c   ▓▓▓▓░░░░│1c       │
│ Mission   10.1 yr    Crew  9.0 yr    │
│ Supplies  Food ✓  O₂ ✓  Water ⚠ −40kg│
└──────────────────────────────────────┘
```

## 6. 비행 레이어와의 관계

- **순수 수학 공유.** `RelativityState.Gamma` / `ThrustFactor` / `ResourceFactor`를 재사용한다. 항행
  닫힌 형식(§3)은 KSP 없이 단위 테스트할 수 있는 **새 순수 모듈**(`TripPlan`, 빌드 단계)에 산다.
  `RelativityState`와 똑같은 방식이다.
- **선택적 감속 신호 공급.** 플래너는 활성 배에 대한 계획이 존재할 때, 정확한 잔여 거리 / 전환점으로 비행
  대시보드의 `⚠ decel now` 신호(dashboard.md §4, design.md §3)를 채워, 비행 중 휴리스틱을 대신할 수 있다.
  선택 사항이며, 휴리스틱은 그대로 함께 나간다.
- **힘 훅으로부터 독립.** 플래너는 에디터 전용이라 Principia stage-7 타이밍을 건드리지 않는다. 따라서
  비행 훅의 API 리스크를 하나도 지지 않으며 독립적으로 빌드할 수 있다.

## 7. 빌드 단계 접점 (`// VERIFY:`)

- 에디터의 스톡 ΔV (`VesselDeltaV` / stage ΔV app), 그리고 `α`용 스테이지별 추력/질량.
- 온보드 자원 보유량과 명목 LS 소비율 (프레임워크별: Kerbalism / stock / CRP).
- 에디터 씬에서의 ApplicationLauncher 등록, 그리고 스톡 슬라이더↔수동 입력 위젯 패턴.

## Related

- [design.md](design.md) — 플래너가 미리 보여주는 메커니즘 (§2.1 thrust/γ³, §2.2 resource/γ, §5 config)
- [dashboard.md](dashboard.md) — 이것이 미러링하는 비행 중 HUD. §4 감속 신호는 플래너가 공급할 수 있다
- 새 소스 (빌드 단계): `TripPlan.cs` (순수 항행 수학), `EditorPlanner.cs` (VAB 창 + 스탯 읽기)
