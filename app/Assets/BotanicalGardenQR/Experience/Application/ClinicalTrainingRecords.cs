using System;
using System.Linq;

namespace BotanicalGardenQR.Experience.Application
{
    // Shared immutable fictional records; times are examples, never processing standards.
    public static class ClinicalTrainingRecords
    {
        public const string Version="SIM-20260921-1";
        public sealed class Row
        {
            public string Id {get;}
            public string Date {get;}
            public string Patient {get;}
            public string Scope {get;}
            public string Start {get;}
            public string End {get;}
            public string Operator {get;}
            public string Room {get;}
            public string LeakId {get;}
            internal Row(string id,string date,string patient,string scope,string start,string end,string person,string room,string leak)
            {Id=id;Date=date;Patient=patient;Scope=scope;Start=start;End=end;Operator=person;Room=room;LeakId=leak;}
            public string Field(int index)
            {switch(index){case 0:return Date;case 1:return Patient;case 2:return Scope;case 3:return Start;case 4:return End;case 5:return Operator;default:throw new ArgumentOutOfRangeException(nameof(index));}}
        }
        static readonly Row[] Rows={
            new Row("SIM-R001","2026-09-20","SIM-P001","DEMO-GI-001","08:10","08:45","训练人员甲","GI","SIM-L001"),
            new Row("SIM-R002","2026-09-20","SIM-P002","DEMO-RESP-001","09:05","09:40","","RESP","SIM-L002"),
            new Row("SIM-R003","2026-09-21","SIM-P003","DEMO-GI-001","10:00","10:35","训练人员乙","GI","SIM-L003")
        };
        public static Row[] Query(string date=null,string scope=null,string room=null)
            =>Rows.Where(r=>(string.IsNullOrEmpty(date)||r.Date==date)&&(string.IsNullOrEmpty(scope)||r.Scope==scope)&&(string.IsNullOrEmpty(room)||r.Room==room)).ToArray();
        public static string Criterion(int index)=>new[]{"date","patient","scope","start","end","operator"}[index];
        public static string Heading(int index)=>new[]{"诊疗日期","患者标识","内镜编号","清洗消毒开始时间","清洗消毒结束时间","操作人员姓名"}[index];
        public static string DocumentTitle(int index)=>new[]{"清洗消毒","逐次测漏","生物学监测","消毒剂监测","人员培训","产品索证"}[index];
        public static string DocumentBody(int index)
        {
            switch(index)
            {
                case 0:return "清洗消毒记录簿 · SIM-REC\n范围：2026-09-20 至 2026-09-21；共3行，无隐藏分页。\n正文由电子记录页同源显示。每行包含患者、镜号、起止时间和操作人，保留原始空白。\n该训练记录只支持字段与关联核查，不据示例时长判断处理效果。";
                case 1:return "测漏登记簿 · SIM-LEAK\n范围：上述3次使用对应的完整训练登记。\nSIM-L001｜SIM-R001｜DEMO-GI-001｜08:05｜训练人员甲｜未见泄漏\nSIM-L002｜SIM-R002｜DEMO-RESP-001｜09:00｜训练人员甲｜未见泄漏\nSIM-L003｜SIM-R003｜DEMO-GI-001｜09:55｜训练人员乙｜未见泄漏\n核对每次使用与登记关联；这些记录不是实际设备检测凭证。";
                case 2:return "生物学监测档案 · 资料待补\n需提供监测对象、采样日期、项目、结果、报告编号及适用依据的完整正文。\n当前没有经审核的对应报告，不制作虚假检测结果或有效期。\n本页是缺件说明，不是已查阅的正式监测证据；OF-03保持暂不可用。";
                case 3:return "消毒剂监测档案 · 资料待核验\n需将现场瓶体名称、规格、批次与监测记录、说明书关联。\n现有P05瓶体继续保留；未把旧课程中的通用资料直接认定为该产品凭证。\n浓度、作用时间、使用期限及检测方法均需具体产品依据，本页不提供自造结论。";
                case 4:return "人员培训登记 · SIM-TRAIN\n日期：2026-09-19；范围：训练人员甲、乙。\n内容：记录填写、镜号关联、逐次测漏登记与异常报告。\n方式：带教示范与独立练习；负责人：模拟带教员。\n本记录为界面与关联训练样例，不代替实际签名、资质或考核材料。其余五类资料的完整性仍需逐项核查。";
                case 5:return "产品索证目录 · 资料待核验\n待补：产品身份及批次、有效说明书、相关资质文件、供货凭证与适用范围。\n要求与现场标签和监测记录相互对应；不能用不同产品的资料替代。\n当前只有目录和缺件说明，没有将目录封面计为索证完成。";
                default:throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
