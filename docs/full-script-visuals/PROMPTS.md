# 本轮内置生图提示词

## 记录表格按原图加工 v2（2026-09-21）

输入：DOCX image3.png，经哈希核对一致；角色为编辑目标。内置imagegen，旧V03保留，本次只清理参考窗口和数据格以便准确运行数据覆盖。

Use case: precise-object-edit. Asset type: high-resolution flat monitor-screen texture for a realistic Chinese hospital VR application. Input image 1 is the ONLY visual authority and edit target: the actual record-query reference image extracted from the user's Word script. Extract and faithfully reconstruct ONLY the central hospital endoscope traceability software window, front-on and filling the canvas, without the hospital photo behind it or screenshot border. Preserve the reference design: translucent pale blue beveled rounded outer frame, off-white main surface, small blue endoscope icon at top left, modest black title 医院内镜追溯系统, smaller subtitle 清洗消毒记录查询, compact bright blue table heading band, five columns and five slim rows with subtle gray rules, light pagination strip, two small blue buttons at lower right, five small blue navigation icons along bottom. This is a faithful cleaned reconstruction, NOT a redesign. NO modern dashboard cards, NO giant tile choices, NO huge blue title banner. Keep exact five column headings: 诊疗日期 / 患者标识 / 内镜编号 / 清洗消毒时间 / 操作人员. Keep exact bottom nav labels 数据总览 / 洗消管理 / 诊疗管理 / 设备管理 / 系统设置. Keep buttons 导出Excel and 打印. All data cells must be EMPTY: remove the reference's patient identifiers, dates, times, names and status badges; runtime will overlay exact simulated data. Blank pagination number area too. Do not invent characters, dates, scores, charts or extra controls. No glow effects stronger than the reference. Crisp legible Chinese labels, straight aligned table boundaries, professional realistic hospital desktop software. Output wide 16:9, preferably 2048x1152, flat screen-only full-frame texture with opaque pixels, no physical monitor, no perspective, no hospital background.


方式：内置imagegen；未使用CLI、API密钥或第三方图像模型。输入图为风格/内容参考，不是需要原样复制的编辑目标。输出原文件保留在Codex generated_images目录，工程内保存副本。

## 大厅场景参考

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

## 办公室场景参考

输入：剧本文档`word/media/image2.png`及本轮`office-records-v1.png`。

Use case: photorealistic-natural. Asset type: hospital endoscopy administrative office reconstruction reference photo for image-to-360 and Gaussian scene generation. Input image 1 (from the user's script) is a STYLE reference for the believable hospital workstation office surrounding the interface. Input image 2 is a STYLE reference only for the blue-white application displayed INSIDE the physical monitor. Generate one clean high-resolution architectural interior photograph of a normal Chinese hospital endoscopy center office. Standing adult eye level 1.65m from the entrance, wide natural 16:9 landscape with corrected vertical lines and believable scale, not fisheye or equirectangular, no collage. A light oak desk against the back/side wall with a real black monitor showing a conventional blue-and-white endoscope records application, keyboard and mouse, ordinary black swivel office chair, compact office printer, pale grey metal document cabinet with neat binders, a few closed paper logbooks on the desk, daylight through a large window with simple blinds. Neutral off-white hospital walls, pale grey tiled floor, practical white acoustic ceiling panels and rectangular LED lights, modest institutional furniture. Keep generous clear walkable floor from the entrance to the desk, furniture against walls, no clutter. An office for reviewing records, NOT a clinical procedure room: no patient, no people, no examination bed, no operating equipment or surgical scene. Photorealistic material detail, restrained natural light, slight everyday wear, not luxury advertising, not greybox game geometry. No floating UI, no translucent holograms, no game HUD, no annotations, no arrows, no decorative panels, no watermarks, no branding, no readable real patient data. The reference's floating login card must NOT appear; only a real physical monitor. One environment reference image, not a texture atlas.
