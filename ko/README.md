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
- **상대론 스타보우 비주얼** *(선택)* — `c` 근처에서 도는 화면 효과입니다. 흑체 도플러 색온도 편이
  (전방 청색편이 / 후방 적색편이, 픽셀마다 `D = 1/[γ(1 − β cosθ)]`)에 플랑크 정밀 눈-대역 밝기 곡선,
  **aberration** — 별과 *행성*이 진행 방향으로 뭉칩니다(은하 카메라 워프 + 후방 라이브 디테일 카메라,
  선플레어·Scatterer 대기도 함께 이동), 그리고 하늘 그라데이션 밴딩을 없애는 HDR 카메라 스택.
  함께 움직이는 함선은 편이시키지 않고, 맵 뷰는 항법 그대로 유지됩니다. 하늘 디테일은 스톡 설정
  화면(난이도 옵션 → Relativity)에서 고릅니다. `Shaders/relativityvisual.bundle`에 셰이더 번들이
  필요하며, β 하한 아래와 워프 중에는 꺼집니다.
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
`rp1RetirementDilation`, `feltGravityComfort`/`Threshold`/`Max`, `dopplerVisual`, `dopplerForceHDR`,
`dopplerColorStrength`, `dopplerAberration`, `dopplerBodyWarp`, `dopplerVesselMask`,
`dopplerSuppressScattererTAA`, `dopplerHeadlight`. 한 줄을 지우면 그 값의 코드 기본값을
쓰므로, cfg 없이도 동작합니다. 비주얼의 룩 자체는 고정(제작자 보정)이고, 고급 곡선 키들은
ModuleManager 전용으로 남아 있으며, aberration 하늘 디테일은 스톡 설정 화면에 있습니다.

고β에서 그라데이션 계단이 보이면 스카이박스가 DXT 압축본입니다 — 텍스처 교체 모드로 무압축(PNG)
스카이박스를 쓰거나, MM 전용 키 `dopplerDither`를 올리세요.

**비주얼과 TAA(시간적 안티앨리어싱)는 같이 쓰지 마세요.** TUFX/PPv2의 TAA는 이전 프레임
히스토리를 재투영하는데, 모션 벡터가 없는 프레임별 상대론 워프와 충돌해 고β에서 우주선 실루엣
반짝임으로 나타납니다. 포스트 프로세싱 프로필의 AA를 **SMAA나 FXAA**로 두세요 — 둘 다 문제없음이
확인됐고, 비주얼 자체에도 실루엣 전용 엣지 AA 패스가 들어 있습니다.

**Scatterer의 자체 TAA는 자동으로 처리됩니다.** Scatterer에는 자체 TAA가 내장돼 있고(*그쪽*
설정에서 기본 켜짐) 같은 충돌을 일으킵니다 — 프레임별 지터가 증폭된 하늘을 자글거리게 합니다.
비주얼이 켜져 있는 동안만 이 모드가 Scatterer TAA를 잠시 꺼두고 끝나면 돌려주므로, 평상시
(아광속 이하) 플레이에서는 Scatterer의 AA가 그대로 유지됩니다(`dopplerSuppressScattererTAA`로
끌 수 있음). Scatterer의 SMAA 옵션은 영향 없이 함께 쓸 수 있습니다.

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
