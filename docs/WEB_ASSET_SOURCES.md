# 网络资源与许可

## 已下载并接入工程

| 资源 | 发布者/作者 | 许可 | 实际用途 |
|---|---|---|---|
| [Clipboard](https://polyhaven.com/a/clipboard) | Poly Haven / ProgrammerOnCoffee | [CC0](https://polyhaven.com/license) | 工位登记夹、随身检查夹；最长边0.36米，6176三角形 |
| [Plastic Bottle Gallon](https://polyhaven.com/a/plastic_bottle_gallon) | Poly Haven / Rahul Chaudhary | [CC0](https://polyhaven.com/license) | 通用教学容器形状；高0.32米，13728三角形，改用干净塑料材质 |
| [Metal032](https://ambientcg.com/view?id=Metal032) | ambientCG | [CC0](https://docs.ambientcg.com/license/) | 工作台金属表面，1K颜色与法线 |
| [Plastic010](https://ambientcg.com/view?id=Plastic010) | ambientCG | [CC0](https://docs.ambientcg.com/license/) | 通用容器表面，1K颜色与法线 |

原始文件在05_网络资源目录；[sources.json](../05_网络资源/sources.json)记录下载URL、SHA256及大小。下载脚本校验发布方MD5，再记录本地SHA256。引擎使用转换后的FBX和URP材质，资源位于app/Assets/Endoscopy/Resources/WebAssets。

容器原模型为开口通用塑料壶，不是教学消毒产品的准确包装，不能把其外形或开口状态作为临床判断依据。甲方提供的清洗室FBX和现场照片继续作为主要空间/设备参考。

## First Hand复用资源

本机来源：D:\unity\Unity-FirstHand-main；上游 [Meta First Hand](https://github.com/oculus-samples/Unity-FirstHand)。仅使用项目自身MIT许可资产，未复制其ThirdParty目录。

| 原始相对路径 | 本工程Resources下路径 | 用途 |
|---|---|---|
| Assets/Project/Models/HandReference/r_hand_skeletal_lowres.fbx | FirstHand/ReferenceHand.fbx | 短暂半透明接近/拿取位置示范 |
| Assets/Project/LocomotionTutorial/Audio/UI_Panel_Wrist_Action_CompleteCheckMark.wav | FirstHand/Complete.wav | 检查完成反馈 |
| LICENSE.txt | FirstHand/LICENSE.txt | 保留原MIT版权和许可文本 |

## 尚未替换的专用物件

清洗毛刷、水枪/气枪、测漏仪、灌流器、滤膜、附件包装仍有几何简化资产，当前不宣称这些已完成真实模型替换。已检索的内镜/医疗车候选常有登录下载、付费或单独许可要求，没有采购或将未下载候选写入已交付清单。

后续替换须核对形态与甲方照片一致性、可下载文件及再分发许可，并控制Quest模型三角形和纹理开销。通用消毒瓶外观不能代替内镜适用性核验。

## 重建资源

- 下载：tools/fetch_web_assets.py（需要联网；已归档原始资源可跳过）。
- 转换：使用Blender后台运行tools/prepare_web_assets.py；结果见artifacts/web-assets-preparation.json。
- Unity构建入口调用WebAssetSetup.Prepare()创建/更新URP材质。使用现有资源编译APK无需重新下载。

