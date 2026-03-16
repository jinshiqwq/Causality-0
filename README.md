# Causality-0

🌍 [English](README.md) | [简体中文](README_zh-CN.md)

<p align="center">
  <img alt="platform" src="https://img.shields.io/badge/platform-SCP%3ASecret%20Laboratory-6f42c1">
  <img alt="api" src="https://img.shields.io/badge/api-LabAPI-2ea44f">
  <img alt="runtime" src="https://img.shields.io/badge/runtime-.NET%20Framework%204.8.1-512bd4">
  <img alt="protocol" src="https://img.shields.io/badge/protocol-.c0%20V16-0a7ea4">
  <img alt="timeline" src="https://img.shields.io/badge/timeline-deterministic-1f6feb">
  <img alt="status" src="https://img.shields.io/badge/status-release-brightgreen">
  <img alt="license" src="https://img.shields.io/badge/license-AGPL--3.0-red">
</p>

<p align="center">
  <strong>A deterministic replay engine for SCP:SL rounds</strong>
</p>

<p align="center">
  <em>Rounds should remain replayable after they end</em>
</p>

---

## Overview

Causality-0 is a LabAPI-based replay plugin for SCP: Secret Laboratory.
It records server-side round state into a deterministic timeline, stores it as a `.c0` binary replay, and reconstructs that round in-game with dummy actors, preserved timing, world-state restoration, and seed-aware playback rules.

Current stable release is **V1.0.1**.
The project is focused on reproducibility rather than cinematic approximation.
Whenever possible, playback restores recorded results directly instead of re-simulating fragile live runtime behavior.

---

## Current capabilities

### Deterministic timeline playback

Replay time is driven by frame index and per-file FPS metadata.
That keeps actor movement, interactions, projectiles, and optional voice packets aligned to the same timeline.

### Actor lifecycle support

The replay pipeline now supports:

- players who join after recording started
- players who leave or disconnect before round end
- role changes during the round
- death lifecycle events
- delayed dummy spawning based on track start frame
- playback despawn on recorded leave

### World-state persistence

The replay format now persists world state beyond actor tracks.
Current world reconstruction covers:

- map pickups present at recording start
- pickup create and remove events
- pickup movement persistence
- scorched-earth pickup cleanup before replay rebuild
- pure world pickup recreation from recorded type, position, rotation, and item properties

### Projectile persistence

Projectile tracks are now written into `.c0` files together with owner information.
Loaded replays can restore projectile playback without relying on the original live runtime state.

### Deterministic door playback

Door replay now restores the recorded interaction result directly.
Loaded replays also use recorded spatial context to improve post-load door matching stability.

### Replay compression

Replay files can now be saved as raw or Lzma-compressed payloads.
Loading auto-detects both formats.

### Optional voice recording

Voice packet capture is available but configurable.
Voice playback still works for replays that already contain saved audio data.

### Seed-aware replay loading

Replay files embed the recording map seed.
If the loaded replay seed does not match the current round seed, the plugin can force a restart so the replay can be loaded on the correct map seed next round.

---

## `.c0` protocol

Current replay protocol version is **V16**.

It currently stores:

| Field | Notes |
| --- | --- |
| Map seed | Used to validate world correctness |
| Replay FPS | Saved in the file and restored on load |
| Actor tracks | Position, view rotation, movement state, held item, stats |
| Audio packets | Optional raw voice payloads with timestamps |
| Interaction frames | Door timing, result, and recorded spatial context |
| Lifecycle events | Role changes, death, leave/disconnect |
| World pickups | Initial world pickup snapshot with absolute transform and item state |
| Pickup ops | Add, move, remove |
| Projectile tracks | Projectile frames and owner id |

Replay save containers currently support raw and Lzma envelopes, and load auto-detects them.

---

## What is currently recorded

- player position and view rotation
- movement state and grounded state
- held item and firearm attachment code
- shooting and reload intent
- usable item start and cancel intent
- HP and AHP-like values
- optional raw voice packets
- door interaction timing, result, and recorded door position context
- late join and leave lifecycle changes
- projectile tracks and owner ids
- world pickups, pickup creation and removal, and pickup movement
- role changes and death lifecycle events

---

## Command surface

`c0` is available as a short alias of `causality`.

```bash
causality start
causality stop
causality save <name>
causality load <name>
causality spawn
causality play

c0 start
c0 stop
c0 save <name>
c0 load <name>
c0 spawn
c0 play
```

### Behavior notes

- `start` begins a new recording from the current state
- `save` writes the current replay into `CausalityRecords/<name>.c0`
- `load` reads seed and FPS metadata before playback
- `load` rebuilds world state when the replay seed matches the current round
- if the replay seed does not match the current round, the plugin schedules a restart with the replay seed
- `play` starts deterministic playback of the loaded or recorded timeline
- `spawn` can still be used for manual dummy spawning workflows

---

## Configuration

The plugin now supports configuration through `config.yml` in the LabAPI plugin config directory.

Current config entries:

```yml
default_record_fps: 60
record_voice: false
replay_compression: Lzma
replay_compression_preset: Normal
```

### Current config behavior

- `default_record_fps`
  - sets the default FPS for new recordings
  - affects new recordings only
  - does not change the FPS embedded in existing replay files

- `record_voice`
  - enables or disables saving player voice packets during new recordings
  - when disabled, replay files still record all non-voice data normally
  - loading and playing older voice-enabled replays still works

- `replay_compression`
  - chooses `None` or `Lzma` for new saves
  - loading auto-detects both raw and compressed replay files

- `replay_compression_preset`
  - tunes encoder settings for new compressed saves
  - currently affects only `Lzma`

---

## Core files

- [Causality0.cs](Causality0.cs)
- [Causality0Config.cs](Causality0Config.cs)
- [Core/Timeline.cs](Core/Timeline.cs)
- [Core/Serializer.cs](Core/Serializer.cs)
- [Core/ActorTrack.cs](Core/ActorTrack.cs)
- [Core/ProjectileTrack.cs](Core/ProjectileTrack.cs)
- [Core/LifecycleEvent.cs](Core/LifecycleEvent.cs)
- [Core/WorldData.cs](Core/WorldData.cs)
- [Command/RemoteAdmin/Causality.cs](Command/RemoteAdmin/Causality.cs)
- [Event/PlayerEvent/Verified.cs](Event/PlayerEvent/Verified.cs)
- [Event/PlayerEvent/Lifecycle.cs](Event/PlayerEvent/Lifecycle.cs)
- [Event/PlayerEvent/VoiceChat.cs](Event/PlayerEvent/VoiceChat.cs)
- [Event/PlayerEvent/Interacting.cs](Event/PlayerEvent/Interacting.cs)
- [Event/ServerEvent/Pickups.cs](Event/ServerEvent/Pickups.cs)
- [Event/ServerEvent/MapGenerating.cs](Event/ServerEvent/MapGenerating.cs)

---

## Current limitations

The project now has a formal V1.0.1 release, but some systems are still expanding.
Current known gaps or ongoing work include:

- ragdoll / corpse / death-scene ecosystem persistence
- automatic round recording policy and autosave workflow
- broader interaction replay coverage beyond the current implemented set
- dedicated replay inspection and debugging tools

---

## Roadmap

- [x] Deterministic replay timing
- [x] Replay FPS embedded in replay files
- [x] Seed-aware replay loading
- [x] Late join actor recording and playback
- [x] Leave/disconnect playback removal
- [x] Optional voice recording configuration
- [x] Door interaction recording and deterministic playback
- [x] Replay compression with None and Lzma
- [x] Projectile persistence and playback
- [x] Pure world pickup snapshot and movement persistence
- [ ] Automatic round recording and autosave policy
- [ ] Ragdoll / corpse persistence
- [ ] Broader interaction replay coverage
- [ ] Replay inspection and debugging tools

---

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=MiaoMiao4567/Causality-0&type=Date)](https://star-history.com/#MiaoMiao4567/Causality-0&Date)

---

## License

This project is distributed under the terms of [GNU AGPL v3](LICENSE.txt).
