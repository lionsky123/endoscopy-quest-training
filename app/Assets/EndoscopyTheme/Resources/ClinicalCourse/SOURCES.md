# 后五关素材来源与许可

检索与导入日期：2026-09-18。资源只用于教学示意，不能据此声称现场设备、产品或程序合规。

## 通用瓶体

- 作品：Pill bottle。
- 作者：Poly by Google。
- 原页面：https://poly.pizza/m/1rtnvryfQZl
- 页面提供的 GLB：https://static.poly.pizza/3a046de5-187c-48c7-aaa7-6053e79a631c.glb
- 许可：[Creative Commons Attribution 3.0](https://creativecommons.org/licenses/by/3.0/)。原页面许可链接已核对。
- 原文件 SHA-256：`b31b709d89c2aba99c400eec5d830f2e5a1b2a07159857d0a7c5f1d1e9d1f94c`。
- 工程文件：`inspection-bottle.obj`，1,620 个三角面。
- 修改：GLB 网格转换为 OBJ，保留几何、归零中心、按高度归一化；省略原材质，运行时使用统一浅蓝材质。没有绘制或生成新模型。不是真实消毒剂产品包装。

## 器械观察短片

- 作品：a_Example video 3D instruments.mp4，3D-COSI ~ 3D Collection of Surgical Instruments，V1.1。
- 作者：Gijs Luijten、Christina Gsaxner、Jianning Li、Antonio Pepe、Narmada Ambigapathy、Moon Kim、Xiaojun Chen、Jens Kleesiek、Frank Hölzle、Behrus Puladi、Jan Egger。
- 数据集：https://zenodo.org/records/10091715 ，DOI https://doi.org/10.5281/zenodo.10091715
- 直接下载：https://zenodo.org/records/10091715/files/a_Example%20video%203D%20instruments.mp4?download=1
- 许可：[Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/)，Zenodo 记录 API 的 `metadata.license.id` 为 `cc-by-4.0`。
- 原文件 MD5：`1b36f78bb6920bb3faa78091433a6fc6`，导入前已匹配。
- 工程文件：`instrument-observation.mp4`，约 40 秒、960×540、30 fps、H.264 Baseline、yuv420p、无音轨，3,236,533 bytes。
- 修改：完整片段降采样、转码、禁用音轨，无新增临床操作内容。画面为多种通用手术器械的扫描展示，非本课程全部内镜洗消工具。
- 关联论文：Luijten et al., 3D surgical instrument collection for computer vision and extended reality, Scientific Data 10, 796 (2023), https://doi.org/10.1038/s41597-023-02684-0 。

## 毛刷图片

- 工程文件：`brush-inspection.png`。
- 生成方式：内置 imagegen 工具；虚构教学示意，不能作为真实产品照片。
- 提示词：Create one photorealistic training photograph for a VR endoscope-cleaning inspection lesson. Wide 16:9 composition. Two separate long thin flexible endoscope channel cleaning brushes lying horizontally on a clean pale stainless steel inspection tray, one in upper half and one in lower half, entirely visible, handles toward left and small cylindrical nylon brush heads toward right. The upper brush has dense even intact nylon bristles, the lower brush has an obviously bent wire core and a large visible patch of missing frayed bristles. Macro sharp detail, clinical soft cool light, restrained realistic materials, no hands, no people, no liquids, no text, no symbols, no arrows, no green or red highlighting, no answer labels, no logos. This is an illustrative fictional equipment condition comparison, not instructions for clinical use. Make the visibly damaged lower brush clear enough to identify in VR.
- 生成后完整复制到工程，没有二次绘制；已目视核对刷毛与弯曲差异。

P02 复用 `ClinicalEvidence` 原有情境图片，原素材标注继续适用。关卡中保留简短作者/许可署名，本文件保留完整来源。

## 带标签消毒剂 3D 资产

- 来源：用户于 2026-09-20 提供的 `Models.zip`，工程导入目录为 `ClinicalCourse/Disinfectant/`。
- 工程文件：`Disinfectant.fbx`、`disinfectant-labeled.prefab`，保留原始 FBX 材质、法线、金属度/粗糙度及标签贴图。
- 用途：用于第 5 关模型观察，练习从实体标签定位产品身份、浓度、规格、批号和日期等证据。
- 限制：贴图中的产品名、生产企业、联系方式、批号、日期及使用范围均未由项目独立核验；模型和标签不能证明真实产品合规、适配内镜或可直接用于临床。
