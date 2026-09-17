# C08 工程约定

- 本工程完整复刻用户拥有的 D:/BotanicalGardenQR-Base，再换成清洗消毒室主题。当前有效方案为 ../docs/FULL_TEMPLATE_C08.md。不要重新引入 CircleLesson 或另外设计主流程。
- 沿用模板 Visitor 场景、小精灵、六点路线、导航、对话、学习内容和主动换站逻辑。取消图鉴、掌心工具和地图召唤、入册收藏；带路时自动显示路线和目标。内部 BotanicalGardenQR 命名保留。
- 全程静音。所有学习面板仅真实手部近距离食指触碰，不提供凝视、鼠标、射线、手柄、键盘提交替代。玩家实际走动，无摇杆/WASD或传送。
- 用户自行编译；未明确要求时不构建 APK，不安装或启动设备应用，也不间接触发构建。必要验证仅静态检查、针对性编辑器测试和编辑器渲染。
- 保持中文应用唯一身份 com.endoscopy.inspection；不得回写旧版本号或关闭 Android Composition Layers Support。
- 素材修改优先 Unity SerializedObject/编辑器配置工具，不使用普通 YAML 序列化器覆盖 SerializeReference 表。内容发布后验证六关图片、全景和各关 3/3/0/3/0/4 道题（第 1 关为观察巩固题，总计 13 道）。
- 保留 LICENSE 与第三方声明。不要读取或复制参考项目的凭据、用户设置和代理配置。

- Quest-only: keep the existing Meta Quest build profile and Android target. All Unity batch/editor validation launches must explicitly pass -buildTarget Android; never switch these projects to Standalone/Windows for checks. Selecting a build profile does not authorize an APK build.

- 第 1 关以 ../docs/FIRST_LESSON_ROOM_OBSERVATION.md 为准：新 7680×3840 全景观察房间，特写对应镜头一要求，3 道观察巩固题。后五关不在本轮重做范围。

- 2026-09-17 第 1 关交互：房间观察后逐张动态加载特写与正误图，一次只有当前内容和一个近触继续按钮；禁止恢复主题/媒体选择菜单或提前答题捷径。最终图谱直接接入原有答题流程。
