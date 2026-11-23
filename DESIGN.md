# Last Train — Design Notes

> Studio motto #001: *Code shall remain understandable to whichever AI inherits this mess.*

## One-line pitch

A semi-realistic 1930s steam-and-early-diesel train simulator with survival elements, set on a
seeded, procedurally generated continent invaded by eldritch things. Rendered like a PlayStation 1 game.
Think *Derail Valley* meets *Oregon Trail* meets *Silent Hill*.

## Pillars

1. **The train is home, vehicle and base.** Every decision comes back to "what's on my train, and can it keep moving?"
2. **Travel is the reward.** One-way journey toward a destination. Exploration pays off in awe and unease, not just loot.
3. **Real machines, forgiving UX.** Real mechanics (boiler, brakes, couplings, wear) with good audio/visual cues instead of forty gauges.
4. **Dread over combat.** You avoid, outrun, appease or endure. You never fight the gods.
5. **Repairs are physical.** Diagnose, source parts, do the job step by step. No "click to repair".

## Decisions

| Area | Decision |
|---|---|
| Engine | Godot 4.7.2, C#, Forward+ renderer |
| Look | PS1: ~320x240 internal render, nearest upscale, vertex snapping, colour quantise + dither, heavy fog |
| Era | Mid-1930s. Steam at its peak, first diesel-electrics arriving. Mostly steam, rare diesel |
| World | Fictional, American-flavoured continent. Seeded per run |
| Goal | One-way journey to a destination that matters; open to exploration along the way |
| Tone | Eldritch invasion of a Depression-era world. Quiet, melancholy, dread; the warm cab is the last safe place |
| Vehicle | Component-based locomotive (boiler, firebox, valves, rods, wheels, brakes, couplers...). Steam is the deep repair game; diesel is reliable but impossible to fix with scavenged parts |
| Cab | First-person, clickable levers that **latch** where released (no holding) |
| Modes | On the train and on foot: foraging, hunting, fishing, camping, scavenging |
| Crew | Single operator; optional recruits that act as modifiers (fireman, mechanic, scout) |

## Eldritch rules of thumb

- The old railway is one of the last human structures still "anchored"; leaving the line on foot gets stranger.
- Old-world defences: light, steam, bells, whistles, iron, salt. The whistle is both tool and risk.
- Corruption is shown, not metered: gauges drift, shadows linger in the cab, the track ahead changes.
- Telegraph and radio are information channels *and* horror devices.
- Salvage from wrong places may work better, worse, or both.

## Scope control: build in slices

If slices 1 and 2 are not fun, nothing else matters.

1. **The Engine** — one locomotive, one straight track, PS1 look, working throttle/brake/boiler. *Does driving feel good?* **(next)**
2. **The Line** — procedural rail network and terrain carved around it. *Is travel the reward?*
3. **The Stop** — on-foot mode, one town, scavenging.
4. **The Breakdown** — component repair for a few parts.
5. Hunting, fishing, camping, crew, weather, diesel era, eldritch encounters.

## Generation order (slice 2)

1. Rail network graph first (stations, junctions, splines with gradient and curve limits).
2. Terrain built around the splines (cuttings, embankments, bridges, tunnels).
3. Populate: towns on nodes, depots, signals, loot, hazards.

Terrain-first generation produces unrealistic steep track; do not do it.

## Proposed code layout (nothing written yet)

```
project.godot / LastTrain.csproj
scenes/                   Entry scene(s)
shaders/                  PS1 surface shader + post-process (quantise/dither)
src/Simulation/           Pure C#, NO Godot types. Testable on its own.
src/Render/               Low-res viewport pipeline + material helpers
src/World/                Track and scenery builders
src/Cab/                  Clickable latched levers and cab rig
```

**Rule:** `src/Simulation` must never reference `Godot`. Physics lives there so it can be unit tested
and reused (e.g. for AI or headless world simulation).

## Slice 1 plan and open technical notes

- Render the 3D scene into a low-res `SubViewport` (about 320x240), upscale with nearest filtering, then post-process for colour quantisation and dithering.
- Mesh shader: vertex snapping, unfiltered textures, vertex lighting, fog as draw distance.
- Affine texture warping: Godot spatial shaders lack a no-perspective varying, so this needs a workaround. Revisit.
- Levers: click-and-drag, value latched on release. Throttle is continuous; brake and reverser have detents. Air-brake lag is modelled.
- Simulation tuning numbers will be guesses at first, not data.
- Dev HUD before 3D gauges.
- A floating origin will be needed for long journeys.

## Open questions

- Cab view: how much of the boiler backhead and window view do we want on screen at once?
- Exact eldritch visual language (what does "wrong" look like in 320x240?).
- Destination and what is waiting there.
- Real railway tradition details to borrow (American assumed).
