# 文档导航

先按任务选择入口；无需通读所有历史记录。根 [AGENTS.md](../AGENTS.md) 定义当前边界，旧文档里的“当前”“最新覆盖”只对其记录时点有效。

| 要做的事 | 阅读入口 |
| --- | --- |
| 理解当前产品范围 | [完整规格](FULL_SCRIPT_SPEC_20260920.md)的当前范围与相关业务章节、[内容矩阵](FULL_SCRIPT_CONTENT_MATRIX.md) |
| 接续实现或验证 | [合并计划](STARTUP_FLOW_AUDIT_PLAN_20260923.md)的执行清单与对应验收条件 |
| 查看用户确认的房间 UI 专题 | [独立规格](ROOM_UI_REFRESH_SPEC_20260924.md)、[独立实施计划](ROOM_UI_REFRESH_PLAN_20260924.md)；概念预览不代表运行画面 |
| 使用 spec / design / plan 流程 | [项目工作流](WORKFLOW.md) |
| 查启动修复证据 | [启动修复进展](STARTUP_REPAIR_PROGRESS_20260923.md) |
| 查 Quest 大厅故障 | [Quest 诊断](QUEST_LOBBY_DIAGNOSIS_20260923.md)；不将文档中的旧实测当作当前通过 |
| 查素材、原图、模型或缺件 | [图册](full-script-visuals/ASSET_CATALOG.md)、[资源缺口](FULL_SCRIPT_ASSET_GAPS.md)、[模型核查](GASTROSCOPE_SOURCE_AUDIT_20260921.md) |
| 修改 Unity 工程 | [工程规则](../app/AGENTS.md)与当前任务相关源码/测试 |
| 了解术语和材料来源 | [CONTEXT.md](../CONTEXT.md)、[来源登记](SOURCE_REGISTER.md) |
| 查原地改造和过去增量 | [原地重构记录](STATIONARY_REBUILD_20260921.md)、[完整计划历史增量](FULL_SCRIPT_PLAN_20260920.md) |
| 查本次工作流迁移 | [迁移记录](WORKFLOW_MIGRATION_20260923.md) |

## 现行、证据与历史的区别

- 规格说明“应当是什么”；合并计划维护“做哪项、是否通过”；矩阵维护完整业务覆盖；图册维护来源与实际消费者。这些职责不互相代替。
- `STATUS.md`、旧 C09 文档、早期 ADR 和历次接入报告保留各自时点证据。使用其中设计前核对当前规则；它们不恢复步行、独立模式、旧课程前置或旧部署授权。
- 根 `SPEC.md` 和 `EXECUTION_PLAN.md` 是兼容导航入口。替换前的根 README/规格/计划在 [工作流迁移前归档](history/workflow-before-20260923/README.md)，原未完成项保留为历史事实，不自动取消或签收。
- 新进度回填原计划并链接报告；不要在每份文档顶部再堆叠一段“最新覆盖”。业务规格历史段落尚未逐条重写，本次导航不宣称已完成全量业务审查。
