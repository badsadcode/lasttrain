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
| Run shape | Long expedition (10+ hours) with saves. New seed per run |
| People | Rare survivors. Mostly empty towns; no working economy |
| Survival | Light: hunger, cold, rest. No thirst/illness micromanagement |
| Platform | PC (Windows/Linux), Steam Deck friendly (assumed) |

## Core loop

> Drive → resources run low or something breaks → choose a stop → leave the train (forage, hunt, fish,
> scavenge, repair, camp) → return → push on into stranger territory.

Two rhythms that make each other welcome:

- **On the train:** tense and mechanical. Pressure, water, gradients, braking, reading the track ahead.
- **Off the train:** slow and atmospheric. Exploring, gathering, repairing, camping.

The longer-term loop is the journey itself: each region is stranger than the last, the train
accumulates wagons, patches and scars, and the destination gets closer.

## Survival (light)

Player needs:

- **Hunger.** Food from foraging, hunting, fishing, scavenged tins. Low hunger slows you and shakes your hands (aim, repairs).
- **Cold.** The cab firebox, campfires and the passenger car keep you warm. Night and weather bite off the train.
- **Rest.** Sleep at camp or in a bunk car. Sleeping is also when time passes and the world shifts.

Train needs:

- **Coal** (or wood in a pinch, burns worse), **water** (towers, rivers via a hand pump),
  **oil** (lubrication, otherwise wear rises fast), **sand** (adhesion on wet or steep track), **parts**.

Survival should create reasons to stop and decisions about *where*, not chores.

## Failure and saving (proposal, not decided)

- **Saving:** at camp, or when the train is stopped and secure. No saving mid-crisis.
- **Player death:** reload the last save. Deaths come mostly from being off the line at night, falls, wildlife, exposure and wrong places.
- **Soft failure is the real pressure:** stranded with a dead boiler, missing track ahead, out of coal far from a depot.
  These should be recoverable, but costly (walk to find parts, burn wagons for fuel, backtrack).
- **Boiler explosion** (low water over a hot firebox) is the one catastrophic mechanical failure. It should be well telegraphed.
- Optional hardcore mode later: one save slot, deleted on death.

## People

Rare survivors: hermits at stations, a family hiding in a depot, a railwayman who won't leave his signal box.
They can trade a little, give information (maps, warnings, rumours), or join as crew. Most towns are empty
and recently abandoned, which is part of the mystery.

## Navigation

On a rail network, routing *is* gameplay.

- Junctions with hand-thrown switches. You stop, get down and throw them.
- Maps are found, bought or drawn. Unknown track is literally unknown.
- Telegraph offices: timetables, line status, and messages that should not exist.
- Dead ends: washed-out bridges, missing track, blocked tunnels. Repair, detour or backtrack.

## Audio

Half the experience in both a train sim and a horror game.

- Chuff synced to speed and cut-off, boiler hiss, safety valve roar, injector sing, rail joints clacking.
- Brakes, couplers and wagons creaking under load: the train should *sound* heavy.
- Wrongness: wind that carries voices, silence where birds should be, a whistle answered from far away.

## Eldritch rules of thumb

- The old railway is one of the last human structures still "anchored"; leaving the line on foot gets stranger.
- Old-world defences: light, steam, bells, whistles, iron, salt. The whistle is both tool and risk.
- Corruption is shown, not metered: gauges drift, shadows linger in the cab, the track ahead changes.
- Telegraph and radio are information channels *and* horror devices.
- Salvage from wrong places may work better, worse, or both.
- **Fog is a signal, not just a draw-distance crutch.** Clear regions show huge distant silhouettes
  (cheap billboards) for awe; thick fog marks corrupted zones for dread.

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
- Affine texture warping: multiply UV by clip-space `w` in the vertex shader and divide in the fragment shader (standard trick). Keep it subtle on track and rails.
- Levers: click-and-drag, value latched on release. Throttle is continuous; brake and reverser have detents. Air-brake lag is modelled.
- Simulation tuning numbers will be guesses at first, not data.
- Dev HUD before 3D gauges.
- A floating origin will be needed for long journeys.

### Slice 1 is done when

- [ ] You can start from cold standing: fire up, build pressure, set reverser, open regulator, move off.
- [ ] Pressure and water respond believably; long cut-off at speed drains the boiler, short cut-off is sustainable.
- [ ] A gradient noticeably slows a heavy train.
- [ ] Brakes have lag; you can stop on a marked spot with practice.
- [ ] Levers latch, have detents where expected, and feel good to drag.
- [ ] The PS1 look holds up in motion (no ugly track shimmer).
- [ ] Tom drives it for ten minutes and wants to keep going.

## Open questions

- What ends a run (failure proposal above needs a yes/no).
- How long is a "region", and how many regions per expedition?
- Can you add, drop and shunt wagons from slice 2 onward, or later?

- Cab view: how much of the boiler backhead and window view do we want on screen at once?
- Exact eldritch visual language (what does "wrong" look like in 320x240?).
- Destination and what is waiting there.
- Real railway tradition details to borrow (American assumed).
