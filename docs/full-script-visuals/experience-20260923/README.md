# 分幕体验预制资源与检查记录

2026-09-23。对应[分幕设计计划](../../STARTUP_FLOW_AUDIT_PLAN_20260923.md#分幕体验设计与实施顺序2026-09-23)。此处只存资源、来源和验证证据；完成状态在原计划维护。所有本目录文件尚未导入Unity，实际运行消费者均为“无”；下列消费者为计划目标。

## 图像

内置image_gen生成/加工，未使用CLI或API密钥。完整提示词见[generation-prompts.json](generation-prompts.json)，文件尺寸/哈希见[image-manifest.json](image-manifest.json)。原工具输出保留于`C:/Users/i1204/.codex/generated_images/01a0cc40-69ac-7a22-a827-46f7dfe235ee/`，项目副本如下。

| 编号 | 文件与来源 | 用途/消费者 | 检查结果与限制 |
| --- | --- | --- | --- |
| V09 | [equipment-hotspot-teaching-v1.png](equipment-hotspot-teaching-v1.png)，1086×1448；0920提取image4 | 设备热点的教学底图；C07.E02拟新增FullScriptEquipmentTeaching | 已查看，大红字/箭头去除，设备整体布局保留；生成过程可能重绘细字与局部，不声称像素忠实。设备标签、屏幕数值不作医疗证据；原图仍须可查 |
| V10 | [ppe-teaching-plate-v2.png](ppe-teaching-plate-v2.png)，1024×1536、不透明RGB；0920提取image5 | 六部位讲解辅助图；C08.E01拟新增FullScriptPpeTeaching | 已查看，帽/眼面/躯干/手/足可辨，无大框和文字。背景仍有轻微明暗，不能称完全均匀或透明；人物是生成重制，不作用品型号/等级凭证，不替代用户FBX |

参考图保留在`artifacts/full-script-reference-images/image4.png`（SHA-256 `f5c8b7d112cd9a9b240fb3e4e162180b64ed7ae8d7c8ac9238b5f7185abe72a1`）和`image5.png`（`bb68098551011a765514044a6168c94552f167b7b23e7886ad1023058547a57e`）。桌面0920文件本轮读取不是标准DOCX ZIP头，未重新解析成功；依据项目已有提取原图及现行规格，未修改原文档。已清理旧概念图与拒绝稿见[图像清理记录](IMAGE_CLEANUP_20260923.md)。

## MV01 指定水槽视频

- 用户将完整本地视频对应到原剧本[指定视频链接](https://www.douyin.com/video/7650412191259065723)；本轮没有从远端下载或独立核实在线文件身份。
- 用户给出的分层路径不存在；实际找到桌面同名扁平文件`video_#护士日常 感谢我们的企划部同事..._top_0_1.mp4`，66,817,472字节，时长约379.27秒。未修改原文件。
- [完整源副本](media/sink-video-source.mp4)，SHA-256 `74e8aabc730b44cacb211bf02d66d6c0ed8cfdd2757ee176af3c8f62bfb15d24`，仅归档，不打包应用。
- [20秒有声片段](media/sink-trigger-04m46s-05m06s-with-audio.mp4)，区间286–306秒，5,259,133字节，SHA-256 `bcfba19d07b78ffa5184ceb66572d13dedd62125a6c22de47fb9c7eb22278bbe`。
- 输出H.264、1280×720、30fps、yuv420p，AAC 44.1kHz双声道；音视频重新编码以精确切片，保留原声内容但非位流复制。不降音量、不移除原字幕、不另配音。
- 用户随后明确“不要静音”，本片为局部有声例外；向导/环境/按钮/其他媒体不自动解禁。运行时声音是否正确输出仍待接入与Quest验证。
- FFmpeg完整解码检查成功；元数据20.00秒且含音轨；volumedetect测得mean -15.6dB、max 0.0dB，证明不是空音轨，不是主观听感验收。见[元数据](media/clip-metadata.txt)、[声音检测](media/audio-validation.txt)和[可复现命令/来源清单](media/manifest.json)。
- 首/中/尾抽帧已查看，内容为水槽内取放、冲洗内镜/附件。原字幕包含具体水质和时长表述，本轮仅保存原视频，不核准为通用判分依据。视频中的病例/标签等不做额外识别。
- 拟消费者为RE-03水槽近触播放器（C07.E01）；当前没有Unity消费者，不把源片可解码等同于Android/Quest可播放。

![片段开始，原片4分46秒](media/sink-clip-start.png)

![片段中部，原片4分56秒](media/sink-clip-middle.png)

![片段结束前，原片5分05.5秒](media/sink-clip-end.png)

## M-PPE01 用户人员穿戴模型

用户追加授权复用`C:/Users/i1204/Desktop/医务人员穿戴.FBX`。原文件不变，留存[项目源副本](models/medical-worker-ppe-source.FBX)，2,411,472字节，SHA-256 `c08849f9fe06120b3e3c6f5e43a7284f37a2f68c8bf9cc0081fe855b519cd77b`。

用项目已有只读FBX解析器读取，未执行模型内逻辑、未导入Unity。FBX7400，1个Mesh、1个材质、1个UV层，17,514顶点/17,512多边形；检测到1份内嵌贴图数据（1,248,846字节），不能因外部路径不存在就视为无贴图白模。未发现Deformer骨骼蒙皮信息；有动画节点不等于已具备人物动作。完整结果见[source-audit.json](models/source-audit.json)。

计划C08.E02优先复用此模型及内嵌材质，后续核实实际姿态、用品形态、比例/UV与眼面等六部位；单网格可设置独立部位锚点/碰撞区域，不要求为了六部位判定强行拆碎网格。不因为文件名合适就自动认定医学表现或六部位交互已达标。许可证/第三方来源文件本轮未附，不杜撰许可证；保留用户提供来源。

## 本轮验证边界

完成资源存在/尺寸/哈希、生成图人工查看、原声检测与视频解码、FBX只读结构核查、文档链接/计划依赖检查。没有改业务代码，没有Unity导入/编译、EditMode、真实Play、渲染或Quest验收，没有构建APK或操作设备。源资源准备与实际接入/完整验收分开记录。

[文档验证结果](document-validation.json)：本地链接与媒体哈希核对通过，27个切片/准备项/检查点编号唯一且引用有效，核心依赖图无环，spec保留13项EX验收。已跟踪文档的`git diff --check`通过；未跟踪新增文档另查行尾空白与冲突标记。
