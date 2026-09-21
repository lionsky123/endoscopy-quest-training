# 内镜中心 VR 监督检查项目

当前源码为 **C09 / 0.9.1**。实际 Unity 工程是 `D:\quest3\EndoscopyBuild\app`；`EndoscopyBuild` 是中文主项目的目录联接，两条路径指向同一套文件。用户自行编译，默认不构建 APK、不安装或启动 Quest 应用。

## 当前体验

2026-09-21：[完整spec v2](docs/FULL_SCRIPT_SPEC_20260920.md)、[完整plan v2](docs/FULL_SCRIPT_PLAN_20260920.md)、[逐项内容矩阵](docs/FULL_SCRIPT_CONTENT_MATRIX.md)及[模型导入/缺口](docs/FULL_SCRIPT_ASSET_GAPS.md)已更新。已有办公室、诊疗室、电脑、桌子、胃镜5个整理版Prefab和6个门Mesh完成导入；未接开机、未替换当前灰盒。后续按房间加载并先释放旧房资源，禁止一口气加载全部模型。

启动进入医院大厅，沿用 Visitor、开场手掌印记和小精灵，按大厅→办公室→储存库→候诊区→诊疗区→清洗消毒室→办公室汇总推进。室内实际步行；走到门口后真实手部近触选择房间，淡出切换。外围房间目前是标明未完成的开发灰盒；办公室已接电脑展开及四字段阅读样例，完整资料与核查评分尚未接齐。全程静音，无凝视、摇杆或WASD移动；每次应用重启新进度。详见[本轮实际接入与缺口](docs/FULL_SCRIPT_INTEGRATION_20260921.md)。

以下洗消六站保留在带教模式中；它们不是全项目的全部关卡，也不会自动覆盖新稿新增的实物关门、指定视频和PPE模型教学。独立核查暂不打开这些带教答案，独立证据与作答仍待接入。

| 关卡 | 当前内容 | 项数 |
| --- | --- | --- |
| P01 | 先学持镜并单独练习，明确开始正式观察；4 倍实体镜片配合宽幅实时取景窗，逐个对准小光圈；对准完成后主动查看图解；近触继续/回看，结束自动收起；可跳过，无答题 | 3 |
| P02 | 五工序卡排序与工位缺失、回流处置 | 2 |
| P03 | 静音器械扫描视频与给定工具清单核查 | 1 |
| P04 | 毛刷状态图片与水源、滤膜、维护记录证据 | 2 |
| P05 | 抓取用户提供的带标签消毒剂模型，核查产品身份、用途与材料资料 | 2 |
| P06 | 逐次记录对账与附件包装追溯 | 2 |

合计 **12 项：3 项引导观察、9 项核查任务**。观察完成不计为答对或独立掌握；跳过与完成/答对分别记录。后五关进一步减少答题仍是设计方案。

房间、六站和路线共用固定世界坐标；全景中心在进入时固定。普通学习面板约前方 50 cm、眼下 18 cm，展开后不随人移动；P01 用手中实际镜片放大全景，对话按专门规格布置。关闭主线透视、MRUK、空间扫描、空间权限、标定、图鉴、掌心地图及收藏入口。

2026-09-20 路线修复：小精灵开场、带路、对话停留和归线统一绑定房间路线网，归线沿过道绕行；首次可靠追踪将头手共同对齐到固定入口并面朝第一站，之后不随转头重新定位。见 [路线与固定开场](docs/VR_ROOM_PATH_BINDING_C09.md)及[路线图](docs/room-map/six-station-map.png)。

## 阅读入口

- [完整剧本新规格](docs/FULL_SCRIPT_SPEC_20260920.md)与[实施计划](docs/FULL_SCRIPT_PLAN_20260920.md)：完整路线与用户决策。多房间运行时已接入，完整内容和正式场地尚未验收；不能把灰盒路线跑通当作完整剧本完成。
- [六关当前内容与规则](docs/CURRENT_EXPERIENCE_C09.md)：当前有效规格、内容来源和验收边界。
- [当前状态](docs/STATUS.md)：源码状态、本轮验证及尚需真机核验的项目。
- [2026-09-20 审查与优化](docs/CONTENT_REVIEW_20260920.md)：需求一致性、实现问题、修复和验证记录。
- [第一关操作教学与逐点图片教学](docs/P01_SEQUENTIAL_TEACHING_C09.md)、[实时镜片实现](docs/P01_LIVE_MAGNIFIER_C09.md)、[全程手部与固定世界](docs/VR_HAND_FIXED_C09.md)、[后五关多媒体](docs/LATER_STATIONS_C09.md)、[Quest 原点恢复](docs/VR_TRACKING_ORIGIN_C09.md)。P01 最新流程恢复观察后的图片教学，取消旧大框与最后松手要求，仍无答题。
- [完整项目范围](SPEC.md)、[客户镜头要求](docs/CLIENT_REQUIREMENTS.md)、[待补内容](docs/CONTENT_QUESTIONS.md)、[项目术语](CONTEXT.md)。完整甲方范围不等于当前六关已经全部交付。
- [历史状态记录](docs/STATUS_HISTORY_20260918.md)：旧 MR、凝视、图片答题和历史验证仅作追溯。

旧 CircleLesson 工程留在 `archive/app-before-template-C08-20260916`，不作为当前主线。参考 `D:/BotanicalGardenQR-Base` 只读，内部 BotanicalGardenQR 名称及场景 ID 是模板绑定键。

源码、待安装 APK、设备安装版本和真机验收分别记录。当前文档不证明设备已运行这些改动。需要只读设备核验时使用 `tools/inspect-installed-lesson.ps1` 比较实际 APK SHA-256；不能只凭显示版本号或假定 `1.apk` 最新。显式 Android 构建的身份校验、递增 versionCode 和构建回执机制保持原样。

## 目录

- `01_原始资料/`：7份输入文件的副本，保留原文件名。
- `02_资料提取/`：指南全文、内镜章节摘录、拍摄脚本文本、指南关键页图片、FBX结构检查结果。
- `03_项目总结/项目资料总结.md`：主要阅读入口，包含资料摘要、场景映射、差异和待补充项。
- `03_项目总结/资料清单.json`：来源路径、文件大小和SHA-256校验信息。

资料中的拍摄、检查和制作要求按文档内容记录，不作为本次执行指令。原始附件未修改。

## 获取与打开工程

仓库：<https://github.com/lionsky123/endoscopy-quest-training>。

先安装 Git LFS，再获取完整素材：

```powershell
git lfs install
git clone https://github.com/lionsky123/endoscopy-quest-training.git
cd endoscopy-quest-training
git lfs pull
```

在 Unity Hub 中添加克隆目录下的 `app/`，使用 **Unity 6000.3.23f1**，安装 Android Build Support（含 SDK、NDK 和 OpenJDK），保持现有 Meta Quest 构建配置及 Android 目标。首次打开由 Unity 还原 Packages；不要把仓库根目录添加为 Unity 工程。构建由用户手动执行，本仓库不配置自动 APK 构建或设备部署。

源码、Unity `.meta`、场景、工程设置、当前课程素材、项目资料和实施文档纳入版本管理。模型、图片、音视频及二进制参考资料使用 Git LFS；旧工程归档、根目录误建的 Unity 工程、缓存、用户设置、APK 和验证输出只在本地保留，不随仓库上传。文档中指向 `archive/`、`artifacts/`、`builds/` 的路径是本地历史记录，克隆后不会包含这些输出。

保留 [模板 LICENSE](app/LICENSE) 及各素材目录的第三方声明；参考资料与外部素材的来源见资料清单和相应说明。
