# 本轮内置生图提示词

## 2026-09-23 分幕体验图像

本轮生成提示与参考角色见[机器可读提示记录](experience-20260923/generation-prompts.json)，保留文件的尺寸/哈希见[图像清单](experience-20260923/image-manifest.json)，用途及限制见[资源报告](experience-20260923/README.md)。D01和V10拒绝稿及V01–V03旧图已清理，提示/哈希留档于[清理记录](experience-20260923/IMAGE_CLEANUP_20260923.md)。

- D01：旧大厅章节概念图；无运行消费者，现已删除，不作当前设计或运行证据。
- V09：0920原图image4为编辑目标，去红色标注供分离热点使用；生成标签不作为原厂证据，原图保留。
- V10：0920原图image5为编辑目标，重制人物教学底图。首稿黑底光晕不适用，图文件已删除、失败原因和哈希保留于清理记录；第二次只要求清理背景，最终v2为不透明浅底，不声称透明精灵。
- 用户提供的人员FBX优先作为RE-06三维来源；本轮图片不是新的人物模型或医院实拍。

## V07 储存柜带教情境（2026-09-21）

方式：内置imagegen；无输入图，按旧稿场景2镜头一的文字要求补充最新稿未展开的储存教学，不冒称0920原照片。用途仅ST-01带教示范，独立核查不加载。原文件`exec-62b3413f-4a9c-4bae-bf04-df531f475e6d.png`保留；工程副本`ClinicalCourse/FullScriptVisuals/storage-cabinet-teaching-v1.png`，1536×1024。

Use case: photorealistic-natural. Asset type: realistic teaching photograph for a hospital endoscopy supervision VR application, NOT a UI, diagram, or legal evidence. Create one 3:2 landscape photograph of a tall enclosed hospital flexible endoscope storage cabinet in a quiet clean storage room. The cabinet itself dominates the frame, full height including feet visible, front three-quarter viewpoint with corrected verticals and realistic proportions. White powder-coated metal exterior, two fully CLOSED clear glass doors with visible gasket seals and handles, subtle ventilation grilles built into its upper and lower body. Through the unobstructed glass clearly show a smooth dry stainless steel interior and exactly THREE flexible gastrointestinal endoscopes separately suspended from proper top supports. Each has a recognizable black control body with angulation knobs near the top, a long insertion tube hanging STRAIGHT DOWN, and its universal cord hanging down separately to a supported connector; distal tips freely suspended well above the cabinet floor. No coiling, no touching floor, no tangled cords. Plausible real clinical instrument forms, no invented robotic gadgets. Interior and instrument surfaces visibly clean and dry, natural soft reflections not droplets. Neutral hospital light, subtle contact shadows, pale practical room walls and grey hospital flooring. No people, no hands, no hospital logo, no brand/model names, no labels or text, no documents, no certificates, no scores, no colored arrows, no tick marks, no highlights, no floating cards. No stylized game greybox. This is a generated teaching illustration of visible cabinet structure and hanging arrangement only, not proof of ventilation performance, internal channel dryness, sterility or compliance. Prioritize clarity of all three suspended instruments and the full cabinet over extra room decor.

## 记录表格按原图加工 v2（2026-09-21）

输入：DOCX image3.png，经哈希核对一致；角色为编辑目标。内置imagegen；旧V03过期数据屏已清理，本次加工空表格以便准确叠加运行数据。

Use case: precise-object-edit. Asset type: high-resolution flat monitor-screen texture for a realistic Chinese hospital VR application. Input image 1 is the ONLY visual authority and edit target: the actual record-query reference image extracted from the user's Word script. Extract and faithfully reconstruct ONLY the central hospital endoscope traceability software window, front-on and filling the canvas, without the hospital photo behind it or screenshot border. Preserve the reference design: translucent pale blue beveled rounded outer frame, off-white main surface, small blue endoscope icon at top left, modest black title 医院内镜追溯系统, smaller subtitle 清洗消毒记录查询, compact bright blue table heading band, five columns and five slim rows with subtle gray rules, light pagination strip, two small blue buttons at lower right, five small blue navigation icons along bottom. This is a faithful cleaned reconstruction, NOT a redesign. NO modern dashboard cards, NO giant tile choices, NO huge blue title banner. Keep exact five column headings: 诊疗日期 / 患者标识 / 内镜编号 / 清洗消毒时间 / 操作人员. Keep exact bottom nav labels 数据总览 / 洗消管理 / 诊疗管理 / 设备管理 / 系统设置. Keep buttons 导出Excel and 打印. All data cells must be EMPTY: remove the reference's patient identifiers, dates, times, names and status badges; runtime will overlay exact simulated data. Blank pagination number area too. Do not invent characters, dates, scores, charts or extra controls. No glow effects stronger than the reference. Crisp legible Chinese labels, straight aligned table boundaries, professional realistic hospital desktop software. Output wide 16:9, preferably 2048x1152, flat screen-only full-frame texture with opaque pixels, no physical monitor, no perspective, no hospital background.


方式：内置imagegen；未使用CLI、API密钥或第三方图像模型。输入图为风格/内容参考，不是需要原样复制的编辑目标。输出原文件保留在Codex generated_images目录，工程内保存副本。

## 已清理的大厅场景参考生成提示（仅历史记录）

输入：剧本文档`word/media/image1.jpeg`。

Use case: photorealistic-natural. Asset type: environment reference image and distant architectural backdrop for a room-scale hospital VR training project. Input image 1 is the user's script reference for architecture and materials, not a document to copy. Generate a very realistic high-resolution Chinese hospital outpatient atrium closely following this reference: tall white structural columns, expansive gridded glass curtain wall, bright softly diffused daylight, white upper balconies with glass balustrades on both sides, a low white reception counter with a pale aqua front, light grey polished terrazzo floor, realistic turquoise self-service kiosks to either side, central circulation axis kept clear. Camera at standing adult eye height 1.65m, natural architectural photography with corrected verticals, broad 16:9 landscape, not fisheye and not a panorama. Quiet unoccupied interior, no people. Faithful practical hospital architecture, natural finish variation and believable scale, not a futuristic fantasy building, not a game greybox. No floating UI, no panels or captions, no highlighter marks, no branding or readable signage, no logos or watermark. The intended look is the reference's real hospital, not an abstract training screen. Final deliverable one clean image.

## 办公室记录屏幕

输入：剧本文档`word/media/image2.png`和`image3.png`。

Use case: ui-mockup. Asset type: photorealistic hospital workstation application SCREEN TEXTURE, viewed exactly front-on; this image will be placed on an actual 3D monitor in VR. Image 1 and image 2 are script style references: blue and white Chinese hospital endoscope traceability software. Generate a credible conventional clinical desktop software screenshot closely matching their visual language, not a game menu. Landscape 4:3 rectangular screen, edge-to-edge application with square-corner window frame, no outside room, no monitor bezel, no perspective, no shadow behind floating cards, no glass hologram, no dark teal fantasy interface. Crisp simplified Chinese, conventional small toolbar above a spacious readable white table. Top blue title bar exact text '医院内镜追溯系统'. Below it text '清洗消毒记录查询', a small tasteful '模拟训练数据' label. Table blue header has EXACT six columns in this order: '诊疗日期', '患者标识', '内镜编号', '开始时间', '结束时间', '操作人员'. Table five rows, exact cell contents:

```text
2026-09-20 | SIM-001 | DEMO-GI-001 | 08:10 | 08:45 | 训练人员A
2026-09-20 | SIM-002 | DEMO-GI-002 | 09:00 | 09:35 | 训练人员B
2026-09-20 | SIM-003 | DEMO-GI-001 | 10:10 | 10:45 | 训练人员A
2026-09-20 | SIM-004 | DEMO-GI-002 | 11:00 | 11:35 | 训练人员B
2026-09-20 | SIM-005 | DEMO-GI-001 | 13:10 | 13:45 | 训练人员A
```


Faint grey row dividers, subtle alternate pale-blue rows, generous legibility. Footer left exact '共5条记录', bottom right exact '示例时长不作为处理标准'. No completed/disinfected/checkmark/compliant status, no correctness answers, no patient photo or clinical imagery, no personal real data. Important exact transcription all requested Chinese and alphanumeric text; no extra paragraphs, no website mockup, no decorative cards. Output one polished realistic application screen texture.

## 水磨石地面纹理

输入：剧本文档`word/media/image1.jpeg`。

Use case: photorealistic-natural. Asset type: seamless tileable game material base-color texture for real 3D hospital lobby floor, not an environment image. Reference image 1 defines the hospital floor material: subtle pale grey finely speckled terrazzo polished slabs. Generate a perfectly flat top-down orthographic square texture of light warm-grey fine terrazzo stone, very fine irregular mineral flecks and slight natural tone variation. No perspective, no people, no objects, no room or walls, no text, no logo. Entire square filled with the material, seamless on all four edges. Neutral diffuse lighting; no cast shadows, no window reflections, no baked lighting, no bright specular highlights. No grout lines because slab grid is modeled separately. Realistic restrained hospital-grade finish, finely detailed, not coarse gravel or marble veins. One texture only.

## 办公桌木纹

输入：剧本文档`word/media/image2.png`。

Use case: photorealistic-natural. Asset type: seamless tileable game material base-color texture for hospital office desk laminate. Image 1 is the script style reference for the light natural wood desk beneath the workstation, not an edit target. Generate a perfectly flat top-down orthographic square texture of a pale natural oak laminated desk surface, fine long grain mostly vertical, lightly warm beige neutral finish, realistic subtle wood pores, hospital office furniture rather than luxury rustic timber. Seamless edges. Entire frame is texture. No room, no monitor, no perspective, no panel, no text, no borders, no objects, no dark knots or dramatic streaks. Neutral diffuse lighting with no shadows or reflections. One texture only.

## 已清理的办公室场景参考生成提示（仅历史记录）

输入：剧本文档`word/media/image2.png`及此前生成的记录界面（文件现已清理）。

Use case: photorealistic-natural. Asset type: hospital endoscopy administrative office reconstruction reference photo for image-to-360 and Gaussian scene generation. Input image 1 (from the user's script) is a STYLE reference for the believable hospital workstation office surrounding the interface. Input image 2 is a STYLE reference only for the blue-white application displayed INSIDE the physical monitor. Generate one clean high-resolution architectural interior photograph of a normal Chinese hospital endoscopy center office. Standing adult eye level 1.65m from the entrance, wide natural 16:9 landscape with corrected vertical lines and believable scale, not fisheye or equirectangular, no collage. A light oak desk against the back/side wall with a real black monitor showing a conventional blue-and-white endoscope records application, keyboard and mouse, ordinary black swivel office chair, compact office printer, pale grey metal document cabinet with neat binders, a few closed paper logbooks on the desk, daylight through a large window with simple blinds. Neutral off-white hospital walls, pale grey tiled floor, practical white acoustic ceiling panels and rectangular LED lights, modest institutional furniture. Keep generous clear walkable floor from the entrance to the desk, furniture against walls, no clutter. An office for reviewing records, NOT a clinical procedure room: no patient, no people, no examination bed, no operating equipment or surgical scene. Photorealistic material detail, restrained natural light, slight everyday wear, not luxury advertising, not greybox game geometry. No floating UI, no translucent holograms, no game HUD, no annotations, no arrows, no decorative panels, no watermarks, no branding, no readable real patient data. The reference's floating login card must NOT appear; only a real physical monitor. One environment reference image, not a texture atlas.
## V08 储存柜模拟清洁登记空白纸面（2026-09-21）

依据旧稿O-S06的柜侧每周清洁登记文字要求；未找到对应原始登记表图片，因此不是医院原表复刻。新生成，不使用参考图片。只生成空白纸面和表线，日期、对象、内容、人员和来源标签全部由程序填入；带教与独立情境分别供数。

工具原文件：`C:/Users/i1204/.codex/generated_images/01a0c39e-3d54-7ce0-83e3-09a9d3a4165b/exec-c4dbff09-90ce-4adb-868a-a618b9657c6d.png`。

运行资产：`app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/storage-cleaning-register-v1.png`。实际表线位置与提示略有差别，消费者按成图实测像素覆盖，不编辑图像。

完整提示词：

```text
Use case: product-mockup. Asset type: photorealistic blank paper register background for an in-world Chinese hospital VR training document, landscape 1536x1024. A straight-on orthographic close-up of one clean white paper record sheet held in a thin muted grey plastic document sleeve on a pale neutral surface. Paper nearly fills frame, all edges visible, flat and square to camera, no perspective skew. Quiet realistic paper fibres, tiny edge shadow, no decorative UI cards. There is NO printed text, handwriting, numbers, signatures, hospital logo, seals, checkmarks or symbols anywhere: software will overlay all exact training data and source labels. Keep a blank title space centered above the table. Draw only the thin dark grey rules of a compact traditional medical log table: table outer bounds at x=8% and x=92%, y=28% and y=83% of the image. Exactly FIVE equal-width columns and FIVE equal-height rows (one header row plus four data rows), perfectly straight fine rules. Thus vertical rules x=8%,24.8%,41.6%,58.4%,75.2%,92%; horizontal rules y=28%,39%,50%,61%,72%,83%. Leave all cells completely empty, no extra lines. Top margin y=8%-25% empty for runtime title and record-range text; bottom margin y=85%-94% empty for provenance. Very soft neutral daylight, matte non-glare paper; subtle believable physical document appearance, not a web dashboard, not an illustration, not a spreadsheet screenshot. Do not invent any medical content.
```

## 房间选择画廊示意图（2026-09-23）

方式：Codex内置ImageGen，未使用输入图。输出为一个3×3格的插画图集；运行时按UV区域裁出七张小图。它只表达房间类别，不对应实际布置、设备清单或现场状态，不能作为真实场景照片、模型或运行画面证据。

工具原文件：`C:/Users/i1204/.codex/generated_images/01a0cda1-2e74-7b10-ba93-b969623f9585/exec-d786a9f2-5054-4938-bcf2-9c384b97c0b5.png`。

运行资产：`app/Assets/EndoscopyTheme/Resources/FullScriptRooms/RoomGallery/room-preview-atlas-v1.png`。

完整提示词：

```text
Use case: VR room-selection gallery imagery
Asset type: one square atlas for seven room-preview thumbnails in a clinical training application
Primary request: create a single square contact sheet with a precise 3-by-3 grid. The first seven cells contain separate, clearly distinguishable room illustrations in this exact order: top row left hospital lobby/reception; top middle small staff office with desk and computer; top right endoscope storage room with clean cabinets and storage racks but no visible endoscopes; middle row left patient waiting area with chairs and a glass divider; middle center gastrointestinal endoscopy treatment room with examination bed and monitor; middle right respiratory endoscopy treatment room with examination bed and monitor, distinct from the previous room; bottom left instrument washing and disinfection room with stainless work surfaces and a sink. Bottom middle and bottom right cells are blank warm-white, no scene.
Style/medium: polished, clearly illustrative architectural editorial painting, soft dimensional brushwork, modern healthcare interiors, calm warm-white and pale gray base with restrained teal accents; visibly an illustration, never a real photograph, real hospital capture, or rendered 3D scene.
Composition/framing: each occupied cell is a square, independent straight-on eye-level room vignette with clear room-defining furniture, composed inside its own equal cell. Exactly aligned 3x3 grid, generous uniform warm-white gutters between cells and around the outside for clean UV cropping. No overlap across cells.
Lighting/mood: bright, diffused, low-glare interior light, calm and welcoming.
Constraints: no text, no letters, no numerals, no labels, no logos, no watermarks, no people, no patient data, no visible hanging or coiled endoscope, no medical procedure, no product labels, no UI panels or buttons. Do not depict these as authentic facility photos.
```

## C01.E08 画廊带练手势图集（2026-09-23）

方式：Codex内置ImageGen，未使用输入图。资源用于带教当前操作，不代表真实手部追踪姿态或已触发的输入；运行时仍必须收到真正跟踪到的手部动作才会推进步骤。

工具原文件：`C:/Users/i1204/.codex/generated_images/01a0cda1-2e74-7b10-ba93-b969623f9585/exec-a2ce0a01-d832-45f6-8eda-c17942d51340.png`。

运行资产：`app/Assets/EndoscopyTheme/Resources/FullScriptRooms/RoomGallery/gallery-gesture-tutorial-v2.png`。

完整提示词：

```text
Use case: infographic-diagram. Asset type: final production hand-gesture instruction sprite atlas for a Unity VR room-selector. Primary request: a 3-column by 2-row transparent sprite sheet, landscape 3:2, with five separate consistent hand-action diagrams and one fully empty transparent cell. Use six equal square cells. In every occupied cell, confine the complete hand and its tiny interaction target to the centered inner 68% of that cell, leaving a clear transparent safety margin on all four sides; nothing may touch or cross a cell boundary. Cell order, left to right then top to bottom: 1) same right hand with index finger extended, thumb apart, moving toward a short rounded horizontal handle; 2) same right hand gently pinching the short horizontal handle between thumb and index; 3) same pinch holding the handle while a small thin deep-teal double-ended horizontal arrow indicates left-right movement, fully inside the cell; 4) same hand opening thumb and index away from the handle with two small outward motion marks; 5) same right index finger gently poking a separate small room-picture card; 6) completely transparent and empty. Style: polished simple flat vector-like game illustration, anatomically plausible fingers, warm ivory fill, crisp dark teal outline, minimal details, no printed shadows; all five drawings must read clearly at small icon size and look like the same hand. Background: genuinely transparent across every cell and gutter, no white or colored tile. Keep a wide transparent gutter between all adjacent cells. No text, no labels, no numbers, no logo, no watermark, no interface panels, no medical objects, no additional hands, no busy room background. The image will be cropped into square gesture icons at runtime.
```
