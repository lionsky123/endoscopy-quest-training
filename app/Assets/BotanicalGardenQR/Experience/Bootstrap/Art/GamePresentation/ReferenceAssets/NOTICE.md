# Meta sample asset notice

This folder contains a deliberately small, presentation-only subset of two Meta sample projects.
The copied files are used as visual inputs for the BotanicalGardenQR visitor prologue and collection fixture;
the original sample scenes, gameplay state, interaction scripts, SDK prefabs, branding, and third-party folders are not included.

## Spatial Lingo

- Source: <https://github.com/oculus-samples/Unity-SpatialLingo>
- Source revision: `84cfd65524e9db26410495ae75fd02d149edb817`
- License: MIT; see `SpatialLingo/LICENSE.txt`.
- Included subset: Language Seed and Dirt Mound model/textures, Language Tree/tree-floor/grass/rock models and textures,
  Strawberry model/textures retained as an audited reference candidate, three berry/tree/foliage sound effects, eight Berry
  squeak variations used by the Fairy's low-frequency idle feedback, and three short Word Cloud interaction cues used by the
  Fairy companion feedback.
- Local modifications: materials, scale, placement, animation timing, and interaction binding are rebuilt by
  BotanicalGardenQR; the Fairy companion cues and idle squeaks are imported as mono for the single spatial AudioSource. No
  Spatial Lingo runtime or lesson-state code is used.

## BotanicalGardenQR offline Fairy voice

- The short Fairy voice clips are project-owned generated assets, not copied from either Meta sample. They were rendered
  locally with Windows `System.Speech.Synthesis` using the installed `Microsoft Yaoyao` zh-CN voice.
- Runtime playback is ordinary Unity WAV playback and requires no Voice SDK, Wit.ai token, account, network connection,
  or online TTS service. The generated voice is not the original Spatial Lingo Golly voice; redistribution of a product
  containing the generated output remains subject to the applicable Windows/Microsoft voice terms.

## First Hand

- Source: <https://github.com/oculus-samples/Unity-FirstHand>
- Reference HEAD when the subset was recorded: `c59f72181b2b762639deaa1b2b37dfe313f19714`.
- License: MIT; see `FirstHand/LICENSE.txt`.
- Included subset: generic direct-touch/play icons and one short wrist-action completion cue.
- Local modifications: color, hierarchy, Chinese labels, world-space sizing, current-SDK interaction, and mono import for the
  Fairy celebration cue are owned by BotanicalGardenQR. First Hand branding, scenes, scripts, legacy interaction prefabs,
  voice-over and third-party content are excluded.

The existing First Hand wrist-action completion cue is also used as the dialogue confirmation sound (2026-09-09), with a quiet frontend mix. Existing Spatial Lingo squeaks accompany explicit Fairy welcome/wonder reactions; no additional media was imported.
