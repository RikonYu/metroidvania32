# Implementation Plan: Metroidvania MVP 1 Framework

## Overview
Build the first playable 2D Metroidvania framework around room-based traversal. The MVP should support grid-snapped rooms, one active room at a time, player movement, bounded camera follow, room exits, runtime transition triggers, and two respawn paths: hazard death to the last safe ground position, enemy death to checkpoint.

## Architecture Decisions
- Room size and positions use Unity world units. A base room is `32 x 16`; larger rooms are integer multiples.
- Room positions snap to `(32a, 16b)`, where `a` and `b` are any integers.
- All rooms live in one scene. Only the active room parent remains enabled; every scene object must belong to a room parent.
- Rooms may touch edges, and that is expected. Any area overlap between rooms is invalid and should warn in editor.
- Room changes happen only through configured `RoomExit` data, not by automatically detecting player bounds.
- Exit `offset` and `length` are world units. Editor draws previews; runtime generates trigger child objects.
- Camera follows smoothly with configurable deadzone, smooth time, and offset. Room transitions and respawns hard-cut the camera to a legal position.
- `ground` is the only layer that records a hazard-safe respawn point. `platform`, `obstacle`, `trigger`, `enemy`, and `hazard` do not.
- `platform` objects use a component-level setting to decide whether they are one-way.
- Hazard death clears player velocity and returns to the last safe ground position. Enemy death returns to checkpoint. Enemy positions and mechanisms are not reset in MVP 1.

## Dependency Graph
```text
Layer/tag conventions
  -> Room data model and editor validation
    -> Room overlap validation and gizmo previews
      -> Room manager active-room lifecycle
        -> Camera bounds and hard-cut support
        -> Runtime exit trigger generation
          -> Room transition flow
  -> Player controller and ground detection
    -> Safe ground recorder
      -> Hazard respawn
      -> Checkpoint respawn
  -> Platform configuration
    -> Player/platform collision behavior
```

## Task List

### Phase 1: Room Foundation

## Task 1: Define Layer and Component Contracts

**Description:** Establish the expected Unity layers and component responsibilities before behavior is implemented. This keeps later scripts from hard-coding ambiguous assumptions.

**Acceptance criteria:**
- [ ] Layer names are documented: `player`, `ground`, `obstacle`, `platform`, `trigger`, `enemy`, `hazard`.
- [ ] Component ownership is clear for `Room`, `RoomExit`, `RoomSpawnPoint`, `RoomManager`, `CamParent`, `MCController`, `PlayerRespawn`, `Checkpoint`, and `PlatformConfig`.
- [ ] Scripts expose inspector fields instead of magic constants for tunable values.

**Verification:**
- [ ] Manual check: layer names and script responsibilities match this document.
- [ ] Build check: Unity compiles after adding empty or minimal component shells.

**Dependencies:** None

**Files likely touched:**
- `Room.cs`
- `MCController.cs`
- `GameController.cs`
- `CamParent.cs`
- New component scripts as needed

**Estimated scope:** Small

## Task 2: Implement Room Data, Snapping, and Bounds

**Description:** Turn `Room` into the authoritative source for room size, grid position, world bounds, and editor snapping.

**Acceptance criteria:**
- [ ] Room size is configured as positive integer multiples of `32 x 16`.
- [ ] Room position snaps in editor to `(32a, 16b)` for integer `a/b`, including negative values.
- [ ] Room exposes world bounds usable by camera and overlap validation.
- [ ] Scene gizmos draw the room rectangle and label useful room identity data.

**Verification:**
- [ ] Manual check: dragging a room snaps correctly on positive and negative grid coordinates.
- [ ] Manual check: changing room size updates bounds and gizmos.
- [ ] Build succeeds.

**Dependencies:** Task 1

**Files likely touched:**
- `Room.cs`
- Optional editor/gizmo helper script

**Estimated scope:** Small

## Task 3: Add Room Overlap Warnings

**Description:** Detect invalid room area overlap in editor while allowing rooms to share edges exactly.

**Acceptance criteria:**
- [ ] Rooms that overlap by area show a visible warning in editor.
- [ ] Rooms that only touch edges do not warn.
- [ ] Warning identifies the conflicting room pair.

**Verification:**
- [ ] Manual check: two adjacent rooms touching edges are valid.
- [ ] Manual check: nudging one room into another produces a warning.
- [ ] Build succeeds.

**Dependencies:** Task 2

**Files likely touched:**
- `Room.cs`
- Optional editor validation helper script

**Estimated scope:** Small

### Checkpoint: Room Foundation
- [ ] Rooms snap correctly.
- [ ] Room bounds are reliable.
- [ ] Overlap warnings work.
- [ ] Project compiles.

### Phase 2: Player and Camera Core

## Task 4: Implement MC Movement MVP

**Description:** Implement basic left/right movement and jumping for a `1 x 2` player using configurable inspector values.

**Acceptance criteria:**
- [ ] `A/D` move the player left/right.
- [ ] `Space` jumps when grounded.
- [ ] Movement speed, jump force/height, gravity tuning, coyote time, jump buffer, and variable jump behavior are inspector configurable.
- [ ] Player reliably detects `ground` for grounded state.

**Verification:**
- [ ] Manual check: player can move and jump in a test room.
- [ ] Manual check: jump buffer and coyote time can be felt when tuned above zero.
- [ ] Build succeeds.

**Dependencies:** Task 1

**Files likely touched:**
- `MCController.cs`

**Estimated scope:** Medium

## Task 5: Implement Camera Follow Within Room Bounds

**Description:** Make `CamParent` follow the player while clamping to the active room bounds.

**Acceptance criteria:**
- [ ] Camera never shows outside the active room.
- [ ] Deadzone width/height, smooth time, and offset are inspector configurable.
- [ ] Camera supports a public hard-cut method for room transitions and respawns.
- [ ] Camera assumes rooms are never smaller than the camera view.

**Verification:**
- [ ] Manual check: moving near all room edges clamps the camera correctly.
- [ ] Manual check: deadzone and smooth settings visibly affect follow behavior.
- [ ] Manual check: hard-cut places the camera immediately in a legal position.
- [ ] Build succeeds.

**Dependencies:** Task 2, Task 4

**Files likely touched:**
- `CamParent.cs`
- `Room.cs`

**Estimated scope:** Medium

### Checkpoint: Movement and Camera
- [ ] Player can traverse a room.
- [ ] Camera follows smoothly.
- [ ] Camera clamp respects room bounds.
- [ ] Project compiles.

### Phase 3: Room Transitions

## Task 6: Define Room Exit and Spawn Data

**Description:** Add serializable room exit configuration and spawn point components to support multiple exits per side.

**Acceptance criteria:**
- [ ] `RoomExit` supports `exitId`, `side`, `index`, `offset`, `length`, `targetRoom`, and `targetSpawnId`.
- [ ] `RoomSpawnPoint` supports `spawnId` and facing direction.
- [ ] A room can configure multiple exits on the same side.
- [ ] Exit gizmos show side, area, and target information in editor.

**Verification:**
- [ ] Manual check: two exits on the same room side render as distinct preview areas.
- [ ] Manual check: spawn points display useful gizmo labels.
- [ ] Build succeeds.

**Dependencies:** Task 2

**Files likely touched:**
- `Room.cs`
- New `RoomSpawnPoint.cs`
- New `RoomExit` serializable type

**Estimated scope:** Medium

## Task 7: Generate Runtime Exit Triggers

**Description:** At runtime, generate trigger child objects from each room's exit data instead of manually placing exit triggers in the scene.

**Acceptance criteria:**
- [ ] Each configured exit creates one trigger child at runtime.
- [ ] Trigger position and size are derived from `side`, `offset`, and `length`.
- [ ] Trigger child references the source room and exit data.
- [ ] Editor preview remains visual only; trigger objects are not required in edit mode.

**Verification:**
- [ ] Manual check: play mode creates trigger children under room parents.
- [ ] Manual check: generated trigger areas match editor gizmos.
- [ ] Build succeeds.

**Dependencies:** Task 6

**Files likely touched:**
- `Room.cs`
- New `RoomExitTrigger.cs`

**Estimated scope:** Medium

## Task 8: Implement Active Room Lifecycle and Transitions

**Description:** Add room activation and transition flow so only the current room is enabled and exits move the player to target spawn points.

**Acceptance criteria:**
- [ ] `RoomManager` tracks the active room.
- [ ] Starting room is configurable.
- [ ] Non-active rooms are disabled.
- [ ] Entering an exit moves the player to the target spawn point.
- [ ] Camera hard-cuts after room transition.
- [ ] Input can be briefly locked during transition.

**Verification:**
- [ ] Manual check: only the active room parent is enabled during play.
- [ ] Manual check: entering multiple exits on the same side reaches the correct target room/spawn.
- [ ] Manual check: camera hard-cuts into the new room without showing outside bounds.
- [ ] Build succeeds.

**Dependencies:** Task 5, Task 7

**Files likely touched:**
- `GameController.cs` or new `RoomManager.cs`
- `RoomExitTrigger.cs`
- `MCController.cs`
- `CamParent.cs`

**Estimated scope:** Medium

### Checkpoint: Traversal Slice
- [ ] Player can move from one room to another through configured exits.
- [ ] Multiple exits on the same side are distinguishable.
- [ ] Inactive room parents are disabled.
- [ ] Camera hard-cuts correctly on transition.

### Phase 4: Respawn and Platforms

## Task 9: Track Last Safe Ground Position

**Description:** Record the player's last hazard-safe position only while grounded on the `ground` layer and in a stable state.

**Acceptance criteria:**
- [ ] Safe position updates only when standing on `ground`.
- [ ] `platform`, `obstacle`, `trigger`, `enemy`, and `hazard` never update the hazard-safe position.
- [ ] Safe position records enough context to restore room and player position.
- [ ] Safe recording uses inspector-configurable stability thresholds where useful.

**Verification:**
- [ ] Manual check: standing on ground updates the safe point.
- [ ] Manual check: standing on platform does not update the safe point.
- [ ] Manual check: safe point remains valid across room transitions.
- [ ] Build succeeds.

**Dependencies:** Task 4, Task 8

**Files likely touched:**
- `MCController.cs`
- New `PlayerRespawn.cs`
- `RoomManager.cs`

**Estimated scope:** Medium

## Task 10: Implement Hazard and Checkpoint Respawn

**Description:** Implement two death routes: hazard returns to last safe ground, enemy returns to checkpoint.

**Acceptance criteria:**
- [ ] Touching `hazard` triggers hazard death and returns to last safe ground.
- [ ] Enemy death hook returns to current checkpoint, with enemy logic left as a placeholder.
- [ ] Respawn clears player velocity.
- [ ] Respawn hard-cuts camera.
- [ ] Respawn does not reset room state, enemy positions, or mechanisms.
- [ ] Optional short invulnerability/input lock prevents immediate repeated death.

**Verification:**
- [ ] Manual check: hazard death returns to last safe ground.
- [ ] Manual check: enemy death test hook returns to checkpoint.
- [ ] Manual check: mechanisms and enemy positions are not reset.
- [ ] Build succeeds.

**Dependencies:** Task 9

**Files likely touched:**
- `PlayerRespawn.cs`
- New `Checkpoint.cs`
- `MCController.cs`
- `RoomManager.cs`
- `CamParent.cs`

**Estimated scope:** Medium

## Task 11: Add Platform Configuration

**Description:** Add per-platform configuration for one-way behavior without making all platform-layer objects behave identically.

**Acceptance criteria:**
- [ ] `PlatformConfig` exposes `oneWay` in inspector.
- [ ] Platform-layer objects can be marked one-way or solid.
- [ ] Platform objects do not count as hazard-safe ground.
- [ ] Player collision behavior respects the one-way setting for MVP needs.

**Verification:**
- [ ] Manual check: a one-way platform can be passed through from below.
- [ ] Manual check: a non-one-way platform behaves as solid.
- [ ] Manual check: standing on either platform type does not update hazard-safe position.
- [ ] Build succeeds.

**Dependencies:** Task 4, Task 9

**Files likely touched:**
- New `PlatformConfig.cs`
- `MCController.cs`
- `PlayerRespawn.cs`

**Estimated scope:** Small to Medium

### Checkpoint: Respawn and Platform Slice
- [ ] Hazard death and checkpoint death route to different destinations.
- [ ] Last safe ground works only on `ground`.
- [ ] Platform one-way setting works.
- [ ] No respawn path resets room state.

### Phase 5: Debugging and Hardening

## Task 12: Add Debug Views and Validation Messages

**Description:** Add lightweight debug output and scene visualization so room, exit, camera, and respawn state can be inspected quickly.

**Acceptance criteria:**
- [ ] Current active room can be identified during play.
- [ ] Camera bounds and room bounds can be visualized.
- [ ] Last safe ground and checkpoint positions can be visualized.
- [ ] Invalid exit target, missing spawn, missing room parent, and room overlap cases warn clearly.

**Verification:**
- [ ] Manual check: intentionally broken exit data produces a useful warning.
- [ ] Manual check: debug gizmos identify current state without entering code.
- [ ] Build succeeds.

**Dependencies:** Task 8, Task 10

**Files likely touched:**
- `Room.cs`
- `RoomManager.cs`
- `PlayerRespawn.cs`
- `CamParent.cs`

**Estimated scope:** Small

## Task 13: Build MVP Test Scene

**Description:** Create or update a small scene arrangement that proves the MVP framework works end to end.

**Acceptance criteria:**
- [ ] Test layout includes at least three rooms.
- [ ] At least one room side has multiple exits.
- [ ] Rooms touch edges without overlap warnings.
- [ ] Test scene includes ground, obstacle, platform, trigger, hazard, checkpoint, and placeholder enemy objects.
- [ ] Test scene exercises hazard respawn and checkpoint respawn.

**Verification:**
- [ ] Manual playthrough: move, jump, transition rooms, hit hazard, trigger enemy death hook, and return correctly.
- [ ] Manual editor check: room snapping, gizmos, and overlap warnings are visible.
- [ ] Build succeeds.

**Dependencies:** Task 12

**Files likely touched:**
- Scene assets under `Scenes/`
- Prefabs or test objects as needed

**Estimated scope:** Medium

### Checkpoint: MVP 1 Complete
- [ ] All room editor rules work.
- [ ] Player traversal works across rooms.
- [ ] Camera follows, clamps, and hard-cuts correctly.
- [ ] Hazard and enemy death routes behave differently.
- [ ] Platform one-way configuration works.
- [ ] Project compiles cleanly.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Room disabling also disables globally needed objects | High | Keep global managers, camera, and player outside room parents; validate all gameplay objects have room parents except explicit global objects. |
| Exit trigger generation and gizmo preview disagree | Medium | Use a shared calculation method for both gizmo drawing and runtime trigger placement. |
| Camera clamp behaves poorly at unusual aspect ratios | Medium | Derive camera half-width from orthographic size and aspect ratio every clamp. |
| Last safe ground records unsafe edge positions | Medium | Add stability thresholds and optional clearance checks around the `1 x 2` player body. |
| Platform behavior conflicts with ground detection | Medium | Treat platform grounding separately from `ground` safe-point recording. |
| Room overlap validation becomes noisy in prefab editing | Low | Only validate scene instances or allow an inspector toggle to suppress prefab-stage warnings. |

## Open Questions
- Should global objects be explicitly marked with a `GlobalObject` component, or is being outside a room parent enough?
- Should transition input lock duration be fixed for MVP or exposed in inspector?
- Should hazard respawn briefly disable hazard collision, or is invulnerability state on the player enough?
- Should room exit target references use direct object references only, or also support string IDs for later save/load?

## Parallelization Opportunities
- After Task 2, Room overlap validation and basic player movement can proceed independently.
- After Task 6, runtime exit trigger generation and camera follow tuning can proceed independently.
- After Task 8, respawn implementation and debug visualization can proceed mostly independently if the shared `RoomManager` API is stable.
