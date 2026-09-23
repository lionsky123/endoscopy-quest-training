# 大厅高斯接入记录

更新：2026-09-21。属于完整plan R04/B6；不能把资产转换成功当成大厅已运行或Quest已验收。

## 已收到且保全

- `app/_IncomingModels/Lobby/b660f052d2670589c7476bc95309ed56.ply`：793729个SH0高斯点，14个float属性；SHA256 `61ec49842db08f6c06c18213bb6a6811ddf64eb2aef2b37d99859abf39c47913`。
- `app/_IncomingModels/Lobby/ca3d941d16fea1f5832e787f22bb55a5.ply`：323602顶点、630285三角形的彩色网格；不是高斯数据，不当成最终高斯渲染，也不直接挂完整MeshCollider。
- 不需要用户重新生成360图片或重复交付同一个模型；用户的生成服务/源资产许可仍由项目方确认。

## 本次实现

使用[UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting/tree/2c6fed37da67a217367261fcfcd3316d34c73e76)的MIT渲染/导入代码，已将该固定提交的`package/`源码嵌入`app/Packages/org.nesnausk.gaussian-splatting/`，不再依赖Unity启动环境中的外部Git命令，也不跟随main。源码已核对14项必需属性及缺省高阶SH处理，SH0输入可转换；没有为兼容伪造额外方向颜色。

`tools/prepare_lobby_gaussian.ps1`在隔离Unity验证工程中显式使用Android目标，调用`FullScriptGaussianImport.Prepare`。转换前后复核源文件哈希和点数。结果位于`app/Assets/EndoscopyTheme/ImportedModels/LobbyGaussian/`，非Resources，无开机Prefab引用。

转换报告：`artifacts/lobby-gaussian-import/import-report.json`。Medium编码的派生数据约38.4MB，保留全部793729点；高阶SH虽然源值为零，当前包仍分配对应存储，不能把文件大小等同GPU内存占用。暂无压缩后画质/Quest性能结论。

## 渲染验证与边界

按[上游渲染管线说明](https://github.com/aras-p/UnityGaussianSplatting/blob/2c6fed37da67a217367261fcfcd3316d34c73e76/docs/render-pipeline-integration.md)，Unity 6 URP使用Render Graph，MSAA存在已知限制。当前工程Render Graph兼容模式关闭，但使用4倍MSAA。因此预览脚本只克隆临时URP/renderer数据、添加高斯渲染Feature、对该临时管线禁用MSAA；不写回实际管线或PlayerSettings。

`tools/prepare_lobby_gaussian.ps1 -Preview`：Android目标、编辑器Vulkan。首轮原点四方向加1个源单位平移的五视角已实际生成并检查；有色彩与真实场景细节，不是白模。临时X=-90度旋转可得到竖直大厅，但原点视点明显偏高，不能当作真实人眼高度。新增低0.95源单位的四视角用于继续确认。源单位尚未校准为米，截图不是360新生图，也不是XR双眼或Quest运行证据。

![实际高斯渲染，原点朝向大厅端部](D:/quest3/EndoscopyBuild/artifacts/lobby-gaussian-import/lobby-01.png)

![实际高斯渲染，另一朝向](D:/quest3/EndoscopyBuild/artifacts/lobby-gaussian-import/lobby-00.png)

彩色网格实读包围盒：原始XYZ最小`(-4.8228,-4.4742,-1.6075)`、最大`(18.0345,6.2750,2.3888)`；Z轴1%分位约`-1.525`，仅作为地面候选，不等同于单位、水平度或地面碰撞验收。未改原始网格。

第二轮九视角已完成。低视点截图见下方；目测更接近站立观察，但不据此确定米制比例。加入依赖和非预加载检查后的Android EditMode 83/83通过，报告`artifacts/lobby-gaussian-import/tests.xml`，包含旧六站回查、统一提交复盘、逐任务/旧课程分页、输入及资源释放回归；不包含Quest GPU/XR实测。

![降低0.95源单位后的大厅实际渲染](D:/quest3/EndoscopyBuild/artifacts/lobby-gaussian-import/lobby-06.png)

仍待完成：确认实际截图、源轴向与地面高度/尺度，简化碰撞与精灵净通路，按房装载/释放，单通道双眼与UI遮挡检查，有限质量档与Quest性能。未完成前主线大厅仍保留明确开发状态，不能静默覆盖成不可行走的高斯壳。

限制：上游说明高斯不写深度，因此常规透明UI/物体遮挡不能直接推定正确；使用已有不透明碰撞/遮挡代理要在真实坐标核对后实现。主线仍真实步行、真实手部、静音；不构建APK。
2026-09-21晚间覆盖：默认原地版已经通过Stationary/LobbyGaussian资源绑定该793729点高斯，生产renderer加入feature，不再走开发大厅分支。实际入口截图为`artifacts/stationary/01-live-gaussian-lobby.png`。管线逐眼计算/分层合成，当前编辑器实际渲染已验证；双眼头显和性能未验证。原始PLY和旧预览均保留。3倍源尺度与-1.525候选地面对齐只是可调整视觉初值，不是实测医院尺寸。完整详情见[原地重构](STATIONARY_REBUILD_20260921.md)。下文“未绑定”段落仅作早期记录。
