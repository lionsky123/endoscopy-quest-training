"""Generate reviewable content matrices from the same catalog consumed by the application."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
c=json.loads((root/'app/Assets/Endoscopy/Resources/catalog.json').read_text(encoding='utf-8'))
lines=['# 风险状态及情境组合','',f'内容版本：{c["version"]}。以下是制作方从甲方合规画面派生的风险，不声称原文已经列出这些题目。','',
       '现场与证据由同一会话配置决定，不能依据用户答案切换风险。全部条目当前为实现中/待视觉验收；逻辑测试通过不等于物件验收通过。','',
       '| 检查点 | 甲方镜头 | 可见风险或记录变化 | 情境 |','|---|---|---|---|']
for p in c['points']:
    cases=', '.join(s['id'] for s in c['scenarios'] if p['id'] in s['risks'])
    lines.append(f'| {p["id"]} {p["title"]} | {", ".join(p["sources"])} | {p["riskVisual"]} | {cases} |')
lines += ['','## 情境规则','',
          '- CASE-01集中启用当前目录全部候选风险；尚须完整视觉及因果一致性验收，不因已配置即视作完整风险版交付。',
          '- CASE-02/03为混合状态，保留合规对照；名称不透露问题类型或数量。',
          '- 当前产品档案和瓶签始终一起变更。历史产品记录是09-14，当前错用其他产品是09-15，不能互相替代。',
          '- 测漏设备当前状态与前一工作日登记分日期；设备在位不能证明记录完整。',
          '- 同时缺终末漂洗工位和改接水路时，需核对模型是否仍有可检查的接出口，不能留下悬空且无法解释的证据。',
          '- CL-TOOLS与CL-LEAK-DEVICE分别检查灌流器和测漏设备。整套工具配置仍须从现场和关联证据一起复核。',
          '- 储存柜开门是观察行为，不自动成为风险；初始柜体干燥与镜体表面状态独立。',
          '- 当前每检查点一个主要问题类型，多个并列子条件仍需按CLIENT_REQUIREMENTS矩阵验收，不能把26个条目数量当作需求已全部覆盖。','',
          '## 数据审查入口','',
          '原始创作输入：tools/generate_content.py；运行文件：app/Assets/Endoscopy/Resources/catalog.json。原始甲方文件不被改写。']
(root/'docs/RISK_STATES.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Updated docs/RISK_STATES.md')
