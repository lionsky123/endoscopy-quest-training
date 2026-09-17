# Quest 3验证记录

日期：2026-09-15。

## 已确认

- adb识别为Quest_3，USB调试状态device。
- 0.3.0 APK安装返回Success。
- 设备包信息：com.endoscopy.inspection，versionName=0.3.0，versionCode=3，arm64-v8a，minSdk32/targetSdk35，包含VR启动类别。
- 启动请求发送后，Quest系统进入LaunchCheckControllerRequiredDialogActivity，要求唤醒控制器。此时目标应用尚未得到完整运行验证。

## 用户安排

用户暂时不方便佩戴，之后自行安装测试；当前0.6.0六站教学版本由用户自行覆盖安装，本次不继续向设备安装更新。

## 未通过/未实测

未确认双眼画面、头部跟踪、两手柄射线、近距离拿取、柜门开合、传送、全路线和帧率。没有用自动答题或电脑截图代替这些实测。

## 复现入口

参见项目根目录安装与测试.md，以及tools/install-quest.ps1。构建日志在artifacts/quest.log；领域测试在artifacts/editmode-results.xml；开发图像巡检在artifacts/smoke-release.log。
