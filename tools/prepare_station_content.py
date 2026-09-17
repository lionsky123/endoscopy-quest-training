"""Prepare traceable local teaching media and authored lesson text; no invented clinical imagery."""
from pathlib import Path
import json,shutil
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'app/Assets/Endoscopy/Resources/StationMedia';OUT.mkdir(exist_ok=True)
media={
 'leak_photo':('01_原始资料/36589735c06677d3805e96f5e86a9213.jpg','用户原图：测漏工位局部，仅作外观参考'),
 'wash_photo':('01_原始资料/3cb3e78ea5311059f36b727a3b38cfbd.jpg','用户原图：胃镜清洗组、水枪及管路局部'),
 'dry_photo':('01_原始资料/47fa09695d2488f317120bd8c629a0ff.jpg','用户原图：干燥台和软式内镜，不代表灭菌附件包装'),
 'product_label':('04_补充依据/产品页4.png','P01公开产品资料第4页：标签扫描'),
 'product_ifu':('04_补充依据/产品页5.png','P01公开产品资料第5页：说明书扫描')}
for key,(path,caption) in media.items():shutil.copy2(ROOT/path,OUT/(key+Path(path).suffix))
rows=[
('CL-SEPARATE',1,'隔断和关闭的门','第一站，布局与流程。先观察诊疗区与清洗消毒室之间的实体隔断和门。触碰查看本题资料，再完成判断。','本小步需要同时核查什么？',['实体隔断完整，并查看门是否关闭','只要门上有清洗室标牌即可'],0,'核查实体隔断和门的实际状态。标牌不能代替隔断。',[]),
('CL-SYSTEM',1,'两类设备分别配置','继续第一站。观察消化与呼吸内镜的设备组，清洗槽和清洗消毒机都要核对用途与分设关系。','哪一种配置符合本组脚本要求？',['同一台设备贴两种用途标识','两类内镜的清洗槽和清洗消毒机分别配置'],1,'槽和机器都要分别配置；仅改变标签不能证明设备分设。',[]),
('CL-STAGES',1,'五工位及单向流程','第一站最后一步。核对清洗、漂洗、消毒、终末漂洗、干燥五个环节，检查是否单向衔接。','请选择完整的流程。',['清洗、漂洗、消毒、终末漂洗、干燥，单向衔接','清洗、漂洗、消毒、干燥，可以返回前一工位'],0,'五个环节齐全，并核对单向无回流；不能漏掉终末漂洗。',['dry_photo']),
('CL-TOOLS',2,'认识五类工具','第二站，清洗工具。逐一认识毛刷、压力水枪、压力气枪、测漏仪、灌流器。照片展示部分真实外观，现场工具需逐项核对。','本组要求核查的完整工具组合是？',['毛刷、水枪、气枪三类即可','毛刷、压力水枪、压力气枪、测漏仪、灌流器'],1,'逐一核查五类工具。部分工具照片不能证明整套配置齐全。',['wash_photo','leak_photo']),
('CL-BRUSH',2,'观察毛刷完整性','继续第二站。观察刷头和刷毛有无可见断裂、缺损。本关根据案例资料判断，真实刷头特写待补。','检查毛刷完整性，重点看哪里？',['刷头和刷毛有无可见破损、缺失','是否连接灌流器'],0,'观察刷头和刷毛本身的完整性。本关使用文字案例，真实刷头近景素材待补。',[]),
('CL-WATER',3,'供水连接与滤膜','第三站，供水与滤膜。先追查终末漂洗水的来源和连接，再看滤膜装置。外壳外观不能单独证明内部滤膜状态。','怎样核查终末漂洗供水？',['只看纯化水三个字','对照水源到终点的连接，并核查滤膜状态和相关证据'],1,'核查实际连接与滤膜相关证据，不能只读水源标牌。本关以案例描述为准，不代表模型内实际水路。',[]),
('CL-MAINT',3,'查阅滤膜更换记录','继续第三站。打开设备维护计划和更换记录，核对是否按给定计划实施。这里的周期属于模拟设备计划，不是通用周期。','更换记录应与什么对照？',['对应设备的说明书或维护计划','所有设备统一每天更换'],0,'以对应设备要求或给定维护计划核对记录，不自创通用更换周期。',[]),
('CL-PRODUCT',4,'标签、说明书和存放','第四站，消毒产品。放大同一产品的标签和说明书，查找适用对象、消毒用途和材料腐蚀性说明，并观察存放工位。','怎样确认资料支持当前产品用途？',['任意内镜消毒产品的说明书都可以','标签与说明书对应同一产品，并核对用途及材料条件'],1,'核对同一产品的适用范围、高水平消毒用途与材料限制；不能声称对所有内镜材料都无腐蚀。',['product_label','product_ifu']),
('CL-ACCESSORY',5,'附件包装与处理记录','第五站，灭菌附件包装。触碰查看包装标识与同批次处理记录。完整标识不能单独证明实际处理合格。','完成包装标识核查后，还应核对什么？',['仅看已灭菌三个字即可','同批次处理和放行证据是否一致'],1,'附件用途、包装标识与同批次处理记录应对应。当前包装与处理资料为教学模拟，真实包装照片待补。',[]),
('CL-LEAK-DEVICE',6,'观察测漏设备','第六站，测漏设备与逐次登记。先核对设备是否在位，观察摆放与接口。历史记录不能证明今天设备仍然在位或正常。','设备现场状态能否由历史记录代替？',['可以，有历史记录就证明设备正常','不能，需要核查当前设备状态'],1,'分别核查当前设备与历史记录。照片中的测漏面板也不能证明设备功能正常。',['leak_photo']),
('OF-LEAK',6,'逐次核对使用登记','最后一步。按甲方脚本，将给定使用清单与同一时间范围的测漏登记逐条对应。找到每次使用对应的记录，再完成本站。','核查三次使用是否均有登记，应该怎么做？',['逐一对应三次使用和登记，确认没有遗漏','当天有一次测漏记录就够了'],0,'按本项目脚本逐次对应，不能用当天一条记录代替全部使用。教学台账不是真实患者记录。',[])]
steps=[]
for ident,station,title,narration,question,options,correct,explanation,images in rows:
 steps.append(dict(id=ident,station=station,title=title,narration=narration,question=question,options=options,correct=correct,explanation=explanation,media=images,mediaCaptions=[media[x][1] for x in images],mediaNote='\n'.join(media[x][1] for x in images) if images else '本页为教学模拟资料；真实专业特写未齐备。'))
(OUT.parent/'station-lessons.json').write_text(json.dumps({'steps':steps},ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'artifacts/station-media-sources.json').write_text(json.dumps(media,ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared',len(steps),'steps and',len(media),'traceable media files.')
