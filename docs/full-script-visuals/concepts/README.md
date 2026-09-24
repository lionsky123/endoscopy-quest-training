# 房间 UI 视觉概念来源

本目录只保存用户确认的专题视觉方向。概念图不在 Unity `Resources` 内，不是正式 Play/Quest 的改版截图，不作为运行效果、医学资料或验收证据。实际消费者仅为[房间界面专题规格](../../ROOM_UI_REFRESH_SPEC_20260924.md)。

## UI-CONCEPT-01（2026-09-24）

- 工具：Codex 内置 ImageGen，两轮编辑。
- 正式 Play 原始输入：`artifacts/production-play/ui-final-source-20260924-1-04-office-topic-selector.png`，SHA-256 `57A39CDF6F19FAF2A9EE40D8797D3B85575BA70358F8C0A51804938C234434FE`。该图来自 Android 目标 Vulkan 宿主 Play，XR 显示未活动。
- 第一轮烟黑形态中间稿：`C:/Users/i1204/.codex/generated_images/01a0d248-e454-7051-be94-6cd0df7b35cc/exec-6880f843-9a68-4eaf-8e2c-9a4a2fea2959.png`，SHA-256 `AD74BE449FD59F30CA083E6AC2475954E9EEE7402D6BAF47867FA27A72CDED46`。
- 用户确认的最终概念：[room-panel-smoke-lavender-concept-20260924.png](room-panel-smoke-lavender-concept-20260924.png)，SHA-256 `938370FE07C500CBB57D68835235DA0C0052FFA21A5258DE3E55C959D2E7090C`。形态沿用第一轮烟黑稿，颜色按用户反馈将选项与承托背景分开。
- 检查：文件与哈希已核对，概念被专题规格引用；没有运行消费者，没有 Unity/Quest 视觉验证。

第一轮烟黑形态的完整提示词：

```text
Use case: ui-mockup. Asset type: PREVIEW-ONLY design direction B for a contemporary 2026 Unity Quest VR training interface. Edit the provided office screenshot; preserve the exact office room, perspective, computer, cabinets and geometry. Replace only the existing big center green-and-white menu. Show a genuinely current, youthful but understated design: an elegantly slim near-hand-reachable dark smoked-glass spatial ribbon around lower-left-center, about one third of the old panel's area, with a satin graphite rear shell, subtle lilac edge reflection and one small vibrant tangerine or coral focus line. Rich charcoal versus soft white typography, confident modern Chinese type hierarchy; no ornate metal, no paper-white box, no neon, no bright blue or green slabs, no giant pill forms, no bulky rounded cards, no generic app icon grid. The compact ribbon has tiny title '办公室' and three immediately touchable integrated actions with exact labels '现场', '电脑', '文件'; selecting an action itself opens the relevant content, no extra confirmation. A small clearly secondary '房间' action is placed at the left end, not as a separate big button. The room and monitor remain dominant, UI is only a light functional layer; keep medical records opaque/readable when opened. VR near-touch targets still visibly large enough and not dependent on reaching the far real desk. Evoke recent expressive spatial product UI through smart asymmetry, restrained color and soft depth, with no visible developer notes and no fabricated medical records. Single high-fidelity visual concept, not proof of implemented runtime.
```

第二轮选项分色的完整提示词：

```text
Use case: precise-object-edit, preview-only UI concept. This image is a design concept, not an implemented Unity screenshot. Edit only the spatial UI ribbon in the provided image. Preserve the exact 3D office room, camera, perspective, desk, chairs, computer, cabinetry, all panel shape/scale/position, heading and Chinese labels. The user likes this second concept's slim smoked-glass ribbon shape and premium satin texture but says options and background need clearly different colors. Keep the outer ribbon a deep translucent charcoal-violet smoked glass. Recolor the three touchable option surfaces to a distinct soft frosted pale periwinkle/silver-lavender with dark graphite labels, without making them opaque white blocks. Give the selected '现场' option a richly colored muted cobalt-violet surface with white text and a very small warm coral accent stroke, so selected and unselected states are immediately distinguishable. Preserve the visual hierarchy, restrained reflections and subtle material depth; reduce visible outlines. Do not add buttons, icons, labels, glow, gradients, neon or additional overlays. Near-hand targets remain large and legible. The result should feel youthful, polished and editorial, with two clearly separate color families for the dark background and the touch options.
```
