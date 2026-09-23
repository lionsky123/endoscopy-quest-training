using System;
using System.Linq;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    // Fictional, complete four-week record set. Weekly cadence belongs to O-S06's
    // training scenario, not a claim about current clinical regulations.
    public static class StorageCleaningRecords
    {
        public const int Count=4;
        public const string Range="2026-08-24—2026-09-20";
        public const string Scope="模拟储存柜 A · 完整4周 / 共1页 / 无另附登记";
        public static string Criterion(int week)=>"storage-week-"+(week+1);
        public static string Evidence(int week)=>"SIM-ST-W"+(week+1);
        public static string Period(int week)=>new[]{"08/24—08/30","08/31—09/06","09/07—09/13","09/14—09/20"}[week];
        public static string Heading(int column)=>new[]{"周范围","登记日期","对象","登记内容","操作人"}[column];
        public static string Cell(int week,int column,bool independent)
        {
            if(week<0 || week>=Count || column<0 || column>=5)throw new ArgumentOutOfRangeException();
            if(column==0)return Period(week);
            if(column==2)return "模拟柜 A";
            if(independent && week==2)return "";
            return column==1?new[]{"2026/08/28","2026/09/04","2026/09/11","2026/09/18"}[week]
                :column==3?"柜体清洁消毒":"模拟操作员甲";
        }
        public static string[] AfterSubmission(ClinicalJourneySession session)
        {
            if(session==null || !session.IsFinished)return Array.Empty<string>();
            var findings=session.GetFindings("ST-02");
            return Enumerable.Range(0,Count).Select(week=>
            {
                var finding=findings.FirstOrDefault(f=>f.CriterionId==Criterion(week));
                var expected=week==2?ClinicalJourneyJudgement.IssueFound:ClinicalJourneyJudgement.NoIssue;
                var outcome=finding.Judgement==ClinicalJourneyJudgement.None?"未答":finding.Judgement!=expected?"判断不符":
                    finding.EvidenceIds.Contains(Evidence(week))?"判断及引用相符":"判断相符，但引用周期不符";
                return "储存柜 "+Period(week)+"："+outcome+"。"+Evidence(week)+
                    (week==2?"日期/内容/操作人空白，不能据此断言未清洁。":"本周登记字段完整，不代表清洁效果。")+
                    "完整4周/1页，无另附登记。";
            }).ToArray();
        }
    }
}
