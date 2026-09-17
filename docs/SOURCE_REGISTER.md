# 来源登记

## 需求范围

唯一范围基线：用户提供《内镜中心VR监督检查拍摄场景.doc》，正文提取见`02_资料提取/拍摄场景原文.txt`。五场景18组镜头及转场、无人、教学/找错、文书汇总均保留。

指南仅支持对应检查点的业务解释。当前实现中引用附件2026年4月版本；没有把指南独立章节或全部条目自动加入范围。

## 程序依赖

- Unity 6000.3.23f1（09d2ecc7fb28），本机已安装。
- URP 17.3.0、OpenXR 1.16.1、XR Management 4.6.1、XR Interaction Toolkit 3.3.2、Input System 1.20.0、Unity Test Framework 1.6.0。
- 实际解析版本由`app/Packages/packages-lock.json`保存。
- API签名依据本项目解析后的Unity官方包源代码核对，包含XRInputButtonReader、XRRayInteractor、XRGeneralSettingsPerBuildTarget和OpenXR配置。

## 字体

Noto Sans CJK SC Regular，来源：[Noto CJK](https://github.com/notofonts/noto-cjk)。字体放在`app/Assets/Endoscopy/Resources/Fonts`，同目录保留SIL OFL许可证。使用公开字体以避免依赖本机中文字体或擅自打包系统字体。

## 三维模型

清洗室模型为用户提供。原始副本不修改；Blender预览脚本仅在本次进程内修正Blender 5.2自带FBX导入器对已移除灯光属性的访问，不修改Blender安装目录。

已得到可视模型与嵌入纹理报告；该结果不等于Quest性能通过。当前程序化场景仍是交互开发场景，真实模型尚待接入与最终物件核查。

## 教学内容

所有应用记录是自制教学模拟。明确标记未完成的产品消毒时间/浓度依据、脚本设计观察项；未声称真实医院存在预埋风险。
