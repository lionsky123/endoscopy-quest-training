# 内镜中心 VR 监督检查项目

面向体验者的现场观察、资料查阅、依据判断与检查汇总。以0920新剧本为主、旧稿补全，完整路线为：大厅 → 办公室 → 储存库 → 候诊区 → 消化诊疗室 → 呼吸诊疗室 → 清洗消毒室 → 办公室汇总。

正式体验采用原地坐姿/站姿、真实手部近触与抓取、全程引导学习和静音。完整内容与 Quest 验收尚未完成，实际进度以原计划及验证证据为准。

## 从这里开始

| 目的 | 入口 |
| --- | --- |
| 项目约束与协作边界 | [AGENTS.md](AGENTS.md) |
| 完整需求和逐项覆盖 | [完整规格](docs/FULL_SCRIPT_SPEC_20260920.md)、[内容矩阵](docs/FULL_SCRIPT_CONTENT_MATRIX.md) |
| 当前任务及验收 | [合并计划](docs/STARTUP_FLOW_AUDIT_PLAN_20260923.md) |
| 规格、设计、任务拆解与交接 | [Agent Skills 工作流](docs/WORKFLOW.md) |
| 查找文档、来源和证据 | [文档导航](docs/README.md) |
| Unity 工程补充约定 | [app/AGENTS.md](app/AGENTS.md) |

## 工程与资料

唯一 Unity 工程为 `app/`。本机 `D:/quest3/EndoscopyBuild/app` 通过目录联接指向本工作区的 `app`，两条路径是同一套文件。版本以 [ProjectVersion.txt](app/ProjectSettings/ProjectVersion.txt) 为准，当前记录为 Unity 6000.3.23f1；目标为 Android / Quest。

| 目录 | 用途 |
| --- | --- |
| `app/` | Unity 代码、资源、包与工程设置 |
| `docs/` | 规格、计划、矩阵、来源和验证记录 |
| `tasks/` | Agent Skills 默认计划路径的兼容索引 |
| `tools/` | 现有检查、资源处理和编辑器辅助脚本 |
| `01_原始资料/`、`02_资料提取/` | 原始输入与提取材料 |
| `03_项目总结/` | 资料摘要与来源清单 |
| `artifacts/` | 本地验证输出，不作为长期规则 |

## 获取与打开工程

先安装 Git LFS，再获取完整素材：

```powershell
git lfs install
git clone https://github.com/lionsky123/endoscopy-quest-training.git
cd endoscopy-quest-training
git lfs pull
```

在 Unity Hub 中添加克隆目录下的 `app/`，安装对应 Unity 版本及 Android Build Support（SDK、NDK、OpenJDK）。保持 Android 目标与现有 Meta Quest 配置；不要把仓库根目录添加为 Unity 工程。

构建由用户手动执行。未明确要求时，不构建 APK，不安装、启动或卸载设备应用。源码、候选 APK、设备包和实测结果分开记录。

## 历史与许可

原根目录 README、SPEC、EXECUTION_PLAN 已保存在 [迁移前归档](docs/history/workflow-before-20260923/README.md)。旧 C09、步行、模式选择及构建授权均按历史上下文阅读，不作为当前接入指令；本次迁移记录见 [W01–W03](docs/WORKFLOW_MIGRATION_20260923.md)。

保留 [模板 LICENSE](app/LICENSE)、素材第三方声明、来源和模拟材料身份。模型、图片、音视频和二进制资料使用 Git LFS；缓存、APK 与本地验证输出不随源码仓库交付。
