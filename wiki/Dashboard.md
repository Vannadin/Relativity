# In-flight dashboard

The dashboard is the mod's face - the split between *nominal* and *effective* thrust **is** the mechanic.
It appears on the stock toolbar and **auto-opens** when the layer activates (β above the gate); it stays
hidden during ordinary in-system flight. The toolbar button opens and closes it at any time, and the
window is draggable.

## The readout

| Row | What it means |
|-----|---------------|
| `β = 0.9230 c` | speed as a fraction of `c`. |
| `γ = 2.598` | the Lorentz factor. |
| `thrust 5.7 %  (×1/γ³)` | effective thrust as a percentage of nominal - the `1/γ³` factor from [The Physics §1](The-Physics#1-thrust-falls-as-1γ-the-light-wall). Braking has the same authority (it is direction-blind). |
| `supplies 38.5 %  rate (×1/γ)` | the proper-time consumption multiplier: life support drains this much slower than the mission clock. |
| `felt gravity 1.52 g  (crew)` | the proper acceleration the crew feel from thrust (un-reduced), in g. Zero while coasting. |
| `crew clock 38.5 %  of KSC's` | how fast the crew's clock runs against KSC's. |

## The two clocks

Below the readout, per vessel:

- **Mission clock** = coordinate time (game UT).
- **Crew clock** integrates proper time `τ = ∫ dt/γ` **from launch**: a true crew-age odometer, shown
  next to the mission clock with the running `crew aged … less` gap.
- The **gap is permanent**: slowing down never catches it back up. The accumulator never resets.
- Stored **per vessel**, and it **keeps advancing while the vessel is unloaded** (on return, the elapsed
  time is integrated with the vessel's background β), so the odometer is right when you come back.
- It is **display-only**: it never manipulates game UT or time-warp. (During warp γ = 1, so it advances
  1:1 with no new gap.)

## Constant-g cruise

A small governor lives in the window: toggle **Constant-g cruise**, type a target in g, and it trims the
thrust limiter as your mass drops so the felt acceleration holds steady. Two things to know:

- It only *caps* thrust, so it holds the target at (near) full throttle - the status line flags
  `THROTTLE UP` if you are throttled down while it governs.
- The `1/γ³` falloff still applies on top: near `c` the felt acceleration drops anyway, target or not.

Set the target at low β, before the burn.

## Doppler visual tuning

If the visual layer is installed, a **Doppler visual** foldout offers the player-facing knobs (force
HDR, colour strength) live in flight. The values are session-only - copy keepers into
`relativity.cfg` ([[Configuration]]). With `debugMode = true` the foldout grows a full dev panel
(frame-time meter, per-feature toggles, the MM-only sliders).

## When it hides

Below the activation speed the window stays hidden (open it from the toolbar if you want the idle
values). Under warp/jump (a warp mod raised `WarpFlag`) and above the sanity ceiling the layer disables,
so the rows read identity.

## Planned

The design spec has more UI than the shipped window: a Simple/Expert split, a light-wall speed gauge,
and a brake-authority cue that fires at the turnover point (`⚠ decel now`). Those are still to come -
today the [[Trip Planner]] is where you work out how early the brake burn starts.
