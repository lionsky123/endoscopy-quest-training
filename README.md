# 内镜中心 VR 监督检查项目

实际工程：`D:\quest3\EndoscopyBuild\app`（必须包含 app）。用户自行编译；默认不构建 APK，不安装或启动 Quest。

当前源码 **0.9.0 / C08**：完整复刻用户的 BotanicalGardenQR-Base 工程，沿用开场邀请、小精灵、六站路线、实际步行导航、对话、内容查看和答题；取消图鉴、工具/地图召唤及入册，带路时自动显示路线和当前目标，替换为清洗消毒室主题。全程静音，学习面板只接受真实手部近触。详见 [C08 完整复刻说明](docs/FULL_TEMPLATE_C08.md)。

主入口是模板原 Visitor 场景。旧 CircleLesson 工程已归档至 `archive/app-before-template-C08-20260916`；当前仍打开 `app`，不要打开归档作为新版。本轮体验调整只修改内镜工程；参考工程此前仅按用户要求修复 Quest 构建目标。

设备上多余的 `com.DefaultCompany.app` 已按用户要求卸载，中文应用保留。最近只读核验设备为 0.8.1 / code 13，尚未核对其源码修订；本轮没有部署新版。

主动 Android 构建会校验包名/名称，递增内部 code、生成唯一构建回执，并在 APK 旁写 `.receipt.json`。后续安装核验使用 `tools/inspect-installed-lesson.ps1` 比对实时设备与指定 APK 的 SHA-256；当前没有新回执时须显式指定候选文件，不能猜测 1.apk 是最新版。详见 [设备排查与处理记录](docs/DEVICE_INTERACTION_DIAGNOSIS.md)。
第 1 关最新调整：使用用户新提供的 7680×3840 全景观察房间，加入门、设备分设、流程方向的三张情境正误图和 3 道巩固题。详见 [第 1 关方案](docs/FIRST_LESSON_ROOM_OBSERVATION.md)。
第 1 关现采用顺序动态加载图谱：房间观察 → 门、设备、流程各自的特写与正误对照 → 3 道题。一次只显示当前图谱和一个继续按钮，无主题/媒体选择菜单。保留参考项目圆角样式及已修正的 360×180° 逐眼方向投影。

## 实施文档

- [镜头一教学重设计](docs/SHOT_01_TEACHING_DESIGN.md)：教学目标、精灵脚本与交互设计；实际接入与剩余差距见实现记录。

- [SPEC.md](SPEC.md)：完整项目范围、五场景与18组脚本映射、对应业务依据、检查内容和验收标准。
- [EXECUTION_PLAN.md](EXECUTION_PLAN.md)：按需求依赖组织的阶段、工作包、完成条件、验证方法和交付清单，不设工期。
- [docs/CLIENT_REQUIREMENTS.md](docs/CLIENT_REQUIREMENTS.md)：甲方18组镜头、通用要求、风险候选、资产及逐项完成条件。
- [docs/CONTENT_QUESTIONS.md](docs/CONTENT_QUESTIONS.md)：未决内容、处理方法和完成条件。
- [docs/STATUS.md](docs/STATUS.md)：当前实际状态与原型差距。
- [CONTEXT.md](CONTEXT.md)：项目业务术语。

当前交互与路线以 C08 完整复刻文档及最新用户决策为准；早期资料总结保留当时的素材判断，目标设备等后续决策已在SPEC中更新。

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
