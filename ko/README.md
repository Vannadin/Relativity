# Relativity — KSP용 아광속 특수상대성 게임플레이 레이어

특수상대성의 아광속 절반을 더해 주는 독립형 **KSP 1.12.x** 게임플레이 모드입니다. 어떤 행성 팩에도
묶이지 않습니다.

> **상태: v0.1.0-beta.** 핵심 상대론 비행은 인게임에서 검증됐고, 몇몇 통합 기능은 컴파일은
> 깔끔하지만 아직 플레이 테스트 전입니다 (기능별 상태는 [`CHANGELOG.md`](../CHANGELOG.md) 참고).
> 긴 상대론 미션 전에는 세이브를 백업하세요.

## 개요

아광속 특수상대성 **게임플레이** 레이어입니다 (Tier 0, 비주얼 없음). `c`에 가까워지면.

- **유효 추력이 1/γ³로 줄어듭니다.** 빛이 다가갈 수는 있어도 넘을 수 없는 자연스러운 벽이 됩니다.
  추진제는 정상 속도로 계속 타므로, 줄어드는 것은 연료 계산이 아니라 효율입니다.
- **고유 시간 자원 소모가 1/γ로 느려집니다.** 빠른 승무원은 더 천천히 나이 들고 생명유지 자원을 덜
  쓰는 반면, 방사선 피폭량은 좌표 시간 기준으로 그대로 흐릅니다.

이 모드는 **힘과 소모율만 조절**합니다. 적분기는 건드리지 않으므로 **Principia와 안전하게** 공존하고
스톡 물리에서도 동일하게 동작합니다.

## 기능

- **상대론 추력** — 엔진의 순 추력이 `F/γ³`가 됩니다 (비행 검증 완료).
- **비행 대시보드** — 스톡 툴바에 β, γ, 유효 추력 %, 자원 소모율 %를 표시합니다.
- **두 시계 카운터** — 함선별 미션(좌표) 시간 대 승무원(고유, τ=∫dt/γ) 시간.
- **VAB/SPH 여행 플래너** — 순항 β, 미션 대 승무원 시간, 가속/관성 구간 분해를 미리 보여줍니다.
- **Kerbalism 자원 지연** — 생명유지 자원을 고유 시간(×1/γ)으로 소모하고, 피폭량은 좌표 시간 유지.
- **자세 제어 ×1/γ** — 리액션 휠과 RCS의 회전 권한이 `c` 근처에서 느려집니다.
- **RP-1 상대론 은퇴** — 은퇴 날짜가 승무원의 고유 시간을 반영합니다.
- **안전 가드** — β 하한 아래, 워프/점프 중, 그리고 상한 위(크라켄/NaN 방지)에서 비활성화됩니다.

## 설치

**KSP 1.12.x**와 **Harmony**(`Harmony2` / HarmonyKSP)가 필요합니다.

- **CKAN** (권장) — *Relativity*를 설치하면 Harmony가 자동으로 함께 설치됩니다.
- **수동** — 릴리즈 zip을 받아 KSP 설치 폴더에 풀어 `GameData/Relativity/`에 놓습니다. Harmony는
  따로 설치합니다.

Kerbalism / RP-1 통합은 해당 모드가 있으면 자동으로 활성화됩니다.

## 설정

`GameData/Relativity/relativity.cfg` (ModuleManager 패치 가능) — `betaMin`, `betaSane`, `debugMode`,
`kerbalismDilation`, `kerbalismExcludedRules`, `attitudeExponent`, `attitudeSkipModules`,
`rp1RetirementDilation`. 한 줄을 지우면 그 값의 코드 기본값을 쓰므로, cfg 없이도 동작합니다.

## 호환성

전체 매트릭스는 [`docs/compatibility.md`](docs/compatibility.md)를 보세요. 요약하면 **Principia**는
설계상 안전, **Kerbalism 3.x / ROKerbalism** 지원, **RP-1** 은퇴 어댑터 포함, **Persistent Thrust**는
이번 릴리즈에서 시계만 추적합니다.

## 문서

- [`docs/design.md`](docs/design.md) — 메커닉 (설계 스펙 원본).
- [`docs/dashboard.md`](docs/dashboard.md) — 대시보드 UX.
- [`docs/planner.md`](docs/planner.md) — 여행 플래너 스펙.
- [`CHANGELOG.md`](../CHANGELOG.md) — 릴리즈와 기능별 검증 상태.
- [`ROADMAP.md`](../ROADMAP.md) — v0.1 이후 방향.

## 출처

원래 더 큰 성간 확장 프로젝트의 상대성 레이어로 만들어졌다가 2026-07-01 이 독립 모드로 분리됐습니다.
설계 스펙의 정본은 이제 이 저장소에 있으며, 독립 모드가 메커닉의 핵심 설계를 그대로 가져갈 수 있도록
범용화됐습니다. `WarpFlag`는 워프/FTL 모드를 위한 범용 확장 지점으로 남습니다.

## 라이선스

MIT — [LICENSE](../LICENSE) 참고.
