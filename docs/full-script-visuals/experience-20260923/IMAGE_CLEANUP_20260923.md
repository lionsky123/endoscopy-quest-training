# 旧图清理记录（2026-09-23）

按用户要求，审计了已过时、无运行消费者的场景概念/渲染图和不再使用的旧记录屏幕，并从活动图册/计划中移除其引用。物理删除工作区文件时执行策略拒绝了PowerShell删除请求；二进制文件不能由文本补丁删除。因此下列文件均标记退役，但仍留在本机工作区，不能声称已经删除。保留来源、用途和清理前SHA-256，便于审计。

| 文件 | 清理前SHA-256 | 清理原因 | 当前替代/状态 |
| --- | --- | --- | --- |
| `docs/full-script-visuals/hospital-lobby-reference-v1.png` | `1CEC1801F0A876F5635D6F1106CDD9A352352B59B85D75E9A688803477A12D85` | 旧单视角大厅环境图，仅作场景参考，无运行消费者；容易被误认作实际大厅画面 | 标记退役；大厅使用用户提供的`Lobby360.png`，原剧本参考图保留；PNG仍在工作区 |
| `docs/full-script-visuals/office-environment-reference-v1.png` | `BE9499EB8079561DB7CCF071D44C5C7106C08C807018DDBE196BC5D127C31F9D` | 旧办公室环境渲染，只在文档中引用，无运行消费者 | 标记退役；办公室依据指定模型目录中的实物模型和原始材质核对；PNG仍在工作区 |
| `app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/office-records-v1.png` | `62B44F4CF1505E2D5E454F7ADBFAD7A5929700C27C06658A8D39A630449832C7` | 带有旧的五行烘焙示例数据；全工作区无代码/场景消费者，Unity GUID `e36a574cfa704da7a8ce4f1195cf5e29`无序列化引用 | 标记退役；由实际运行中的`office-records-reference-v2.png`与同源程序数据取代；PNG和对应`.meta`仍在工作区 |
| `docs/full-script-visuals/experience-20260923/lobby-chapter-layout-v1.png` | `ADBB915732C3B9BBF697DE1BFC8D88D2CA7953662FFD284773B86CBBB83AE199` | AI生成的章节选择概念图，无运行消费者，且与用户实际看到的入口画面不同 | 标记退役；正式流程为“开始学习”后前往办公室；Play/Quest画面只以当前运行捕获为证；PNG仍在工作区 |
| `docs/full-script-visuals/experience-20260923/ppe-teaching-plate-v1-rejected.png` | `95723E489E2AEE08876679AFCD2C52E9E441AEC99284B0B55F67BE8B1C2FAA0C` | 已拒绝的黑底/蓝色光晕旧稿，无运行消费者 | 标记退役；保留剧本image5原图与最终不透明v2；PNG仍在工作区 |

本次没有触碰用户剧本原图、用户提供的大厅全景、V04/V05材质纹理、V06实际运行电脑屏幕、V07/V08等已有消费者资源，也没有删除Play/Quest运行截图。物理删除仅尝试以上5个PNG及`office-records-v1.png.meta`，但被执行策略拦截；它们仍存在本机工作区。图册和提示档案保留清理前哈希及来源记录。
