# 第一站独立核查图片素材

2026-09-17，使用内置 imagegen 生成并人工查看三张新情境图，复制到 `app/Assets/EndoscopyTheme/Resources/ClinicalEvidence`。每张实际尺寸 1672×941；保留原生成图，不经裁剪移除答案，也不替换用户的 7680×3840 全景。

| 文件 | 内容与用途 | 题面 UI | 正确依据 |
|---|---|---|---|
| door.png | 左侧打开的门与门洞，右侧清洗工位 | 中性区域编号 | 门与门洞；当前关门条件不满足 |
| equipment.png | 左右各自的槽与独立机器 | 左消化、右呼吸，为情境设定 | 必须同时核对两组；只看一组不足 |
| flow.png | 从左到右四个槽及独立干燥工位 | 五个中性工位标签，第 3、4 顺序交换 | 第 3、4 工位均选中，判断需要纠正 |

题面 PNG 没有正确/错误标题、勾叉、答案轮廓或流程箭头。编号、情境标签、选中轮廓、答后依据均由 UI 单独绘制。配置、归一化区域与分级提示集中在同目录 `lesson.json`，坐标从左下角起，范围 0–1。所有案例在界面明确标为 AI 生成教学情境；用途和流程标签是题目设定，不能作为现场测量或合规证明。

生成来源（本地原始输出）：

- 门：`C:/Users/i1204/.codex/generated_images/01a0adf1-2a0d-7f01-a9ec-9ea6efed1b6b/exec-6ae11a00-cff4-4899-9547-cd5fc7f0a0c8.png`
- 设备：`C:/Users/i1204/.codex/generated_images/01a0adf1-2a0d-7f01-a9ec-9ea6efed1b6b/exec-5958511e-0ee5-4284-a0e1-2340df470042.png`
- 流程：`C:/Users/i1204/.codex/generated_images/01a0adf1-2a0d-7f01-a9ec-9ea6efed1b6b/exec-41e6c171-7c15-4f31-ae86-bc8e151b0a77.png`

## 门图生成要求摘要

单幅 16:9 写实医院内镜清洗消毒室；左侧门扇明显打开，可透过门洞看到相邻空间；右侧蓝色柜体与不锈钢清洗工位；干净的灰墙、蓝色地面、真实灯光与材质。无人物、无正误标题、无勾叉和红绿判断、无箭头或答案标注。以下两段保留工具调用的原始提示词；门图本段为要求摘要。

## 设备图原始提示词

Create ONE landscape 16:9 photorealistic architectural teaching case image for a VR lesson in an endoscopy reprocessing room. Single continuous room view, not a collage. Hospital light gray walls, blue cabinets, brushed stainless counters, blue terrazzo floor, premium believable lighting. Clearly show TWO separate sink workstations on left and right, and TWO distinct automatic reprocessing machines, one beside each sink station. Each station must have its OWN recognizable sink and its OWN separate machine; leave a small visual gap between stations. Camera centered frontal with a little perspective, all equipment visible, clear visual pairing spatially, no crossed plumbing or shared machine. Left workstation occupies left half; right workstation right half. No humans, no endoscopes in patients, no gore. This is an unlabeled assessment picture: absolutely no correct/incorrect labels, no green or red overlays, arrows, checkmarks, captions, titles, logos or watermarks. No illegible text. Unmarked neutral fixtures. A teaching disclaimer and machine-purpose labels will be added by software. High detail, restrained realistic hospital finish. Output the single clean image only.

## 流程图原始提示词

Create ONE landscape 16:9 high-resolution photorealistic teaching image of a Chinese endoscopy reprocessing room workbench for a VR learning assessment. SINGLE continuous scene, not a collage, no infographic. A wide frontal slightly elevated architectural view of a long blue-base stainless steel workbench with FOUR distinct sink basins arranged left to right, each with one simple faucet, and a clearly separate DRY work surface at the far right with a simple air drying device. All four basins plus the dry work surface clearly fit in frame, no fifth basin. Grey clean walls, blue terrazzo floor, realistic hospital ceiling light, crisp premium materials, sparse unobtrusive surroundings. Keep all five positions along the central horizontal band so software can place labels above them. NO labels or written text of any kind, NO correct/incorrect marks, NO arrows, NO colored light trails, NO checkmarks, NO title, NO watermarks, NO people, NO gore. This is a neutral image whose workstation labels and the route to evaluate are overlaid by the lesson software, never pre-baked. No invented medical actions. Output one wide room scene only.

## 编辑器预览

运行 `tools/render_clinical_evidence.ps1`，默认输出 `artifacts/clinical-evidence/1/2/3-method/question/feedback.png`（实际文件例如 `2-question.png`）。脚本仅启动 Android 目标隔离编辑器做渲染，不进 Play Mode、不调用任何 APK 构建或设备部署。预览采用真实全景模块和实际 UI；普通按钮的直接调用仅存在于 Editor 预览工具，生产输入仍为共享凝视控制器。
