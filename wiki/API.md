# Mod API

Relativity slows a ship down with a corrective force and leaves the engine's advertised numbers
alone. That is what makes it safe next to Principia, but it also means nothing you read off a
`ModuleEngines` tells you the real acceleration near c. If your mod predicts burns, plans
trajectories, or autopilots anything from thrust, ask this API instead of re-deriving gamma
yourself.

The whole surface is one static class, `Relativity.RelativityApi`:

```csharp
public const int ApiVersion = 1;
public static double GetGamma(Vessel vessel);            // Lorentz gamma; 1.0 when inactive
public static double GetThrustMultiplier(Vessel vessel); // 1/gamma^3; 1.0 when inactive
```

`GetGamma` is the vessel's Lorentz factor from its barycentric speed. `GetThrustMultiplier` is
`1/gamma^3`: multiply any advertised thrust by it and you get the force that actually accelerates
the ship (why the cube: see [[The Physics]]).

The contract:

- Inactive reads as identity. Below `betaMin`, during warp, on a null vessel or a bad state
  sample, both methods return exactly 1.0. So `advertised * GetThrustMultiplier(v)` is always
  safe to write; you never need to check whether the layer is on.
- Main thread only. The gamma read walks the orbit chain, which is not thread-safe. If your
  math runs on a background thread (fuel-flow sims and the like), capture the value on the main
  thread once per FixedUpdate and hand it over; a volatile field is enough. Our own MechJeb
  adapter does exactly this.
- Cheap to call often. Results are memoized per vessel per FixedUpdate, so a hundred calls in
  one tick cost one evaluation.
- Signatures are frozen. Any breaking change bumps `ApiVersion`, so gate on it and you will
  never bind to a surface you don't understand.

## Hard dependency

If your mod requires Relativity anyway, reference `Relativity.dll` and declare the dependency so
KSP orders the load:

```csharp
[assembly: KSPAssemblyDependency("Relativity", 1, 0)]
```

```csharp
double realThrust = advertised * Relativity.RelativityApi.GetThrustMultiplier(vessel);
```

## Soft dependency (reflection)

Most mods will want Relativity to stay optional. Resolve the type once, check the version, cache
a delegate, and fall back to 1.0 when anything is missing:

```csharp
static Func<Vessel, double> thrustMul;

static void TryBindRelativity()
{
    var api = AssemblyLoader.loadedAssemblies
        .Select(a => a.assembly.GetType("Relativity.RelativityApi"))
        .FirstOrDefault(t => t != null);
    if (api == null) return;                     // Relativity not installed

    var ver = api.GetField("ApiVersion");
    if (ver == null || (int)ver.GetRawConstantValue() != 1) return;  // future surface, skip

    var m = api.GetMethod("GetThrustMultiplier", new[] { typeof(Vessel) });
    if (m != null)
        thrustMul = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), m);
}

static double Multiplier(Vessel v) => thrustMul != null ? thrustMul(v) : 1.0;
```

Call `TryBindRelativity()` once at startup (a `KSPAddon` `Start` is fine). Everything after that
is a plain delegate call, no per-frame reflection.

## Don't double-apply

Since v1.2 Relativity ships adapters that already rescale the readings of kOS, MechJeb and the
stock burn timer (`compatKosThrust`, `compatMechJebThrust`, `compatStockBurnTimer`, all on by
default). A kOS script reading `SHIP:AVAILABLETHRUST` already sees the reduced value; multiplying
it by `GetThrustMultiplier` on top would apply the cut twice. The API is for mods doing their own
thrust math from engine modules, not for consumers of readings that are adapted already. The full
list of what is adapted lives in [[Compatibility]].

## Warp mods: the WarpFlag hook

Integration in the other direction. If your mod moves vessels warp- or jump-style, that motion is
not physical velocity through space, and the relativity layer must switch off during it. Tell it
so:

```csharp
Relativity.WarpFlag.Provider = v => MyWarpDrive.IsWarping(v);
```

Return true while the vessel is warping; the layer then reads identity for it (and the API above
returns 1.0). Two things to know:

- Keep the provider exception-free. If it throws, it gets unregistered with a warning and the
  layer falls back to "not warping".
- `Provider` is a single slot; the last writer wins. If a second warp mod could be installed
  alongside yours, chain the previous value instead of overwriting it.

Blueshift and the KSPIE Alcubierre drive are detected on our side and don't need to register.

## See also

- [[Compatibility]]: what is already adapted, and the status of neighbouring mods.
- [[The Physics]]: where the gamma and the cube come from.
