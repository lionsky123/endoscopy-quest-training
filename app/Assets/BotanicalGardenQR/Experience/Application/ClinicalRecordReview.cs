using System;
using System.Linq;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    // Deliberately evaluates only fictional field completeness, not medical/legal compliance.
    public static class ClinicalRecordReview
    {
        public static string[] AfterSubmission(ClinicalJourneySession session)
        {
            if(session==null || !session.IsFinished)return Array.Empty<string>();
            var rows=ClinicalTrainingRecords.Query();var findings=session.GetFindings("OF-01");
            return Enumerable.Range(0,ClinicalTrainingRecords.FieldCount).Select(index=>
            {
                var finding=findings.FirstOrDefault(f=>f.CriterionId==ClinicalTrainingRecords.Criterion(index));
                var missing=rows.Where(r=>string.IsNullOrEmpty(r.Field(index))).Select(r=>r.Id).ToArray();
                string outcome;
                if(finding.Judgement==ClinicalJourneyJudgement.None)outcome="未答";
                else if(missing.Length==0)outcome=finding.Judgement==ClinicalJourneyJudgement.NoIssue?"判断相符":"误报";
                else if(finding.Judgement==ClinicalJourneyJudgement.NoIssue)outcome="漏检";
                else outcome=finding.EvidenceIds.Any(id=>missing.Contains(id))?"判断及引用相符":"发现缺项，但引用行不符";
                return ClinicalTrainingRecords.Heading(index)+"："+outcome+"；"+(missing.Length==0?$"完整范围{rows.Length}行均有值":string.Join(",",missing)+"原值为空");
            }).Concat(StorageCleaningRecords.AfterSubmission(session)).Concat(LeakAfterSubmission(session)).ToArray();
        }
        public static string LeakCriterion(string useId)=>"leak-"+useId;
        public static string[] LeakAfterSubmission(ClinicalJourneySession session)
        {
            if(session==null || !session.IsFinished)return Array.Empty<string>();
            int document=ClinicalTrainingRecords.DocumentIndexForTask("OF-02");
            if(document<0)return Array.Empty<string>();
            var entries=ClinicalTrainingRecords.LeakEntries(document);
            var findings=session.GetFindings("OF-02");
            return ClinicalTrainingRecords.Query().Select(use=>
            {
                var entry=entries.FirstOrDefault(e=>e.UseId==use.Id);
                var finding=findings.FirstOrDefault(f=>f.CriterionId==LeakCriterion(use.Id));
                bool present=entry!=null;
                var expected=present?ClinicalJourneyJudgement.NoIssue:ClinicalJourneyJudgement.IssueFound;
                string outcome=finding.Judgement==ClinicalJourneyJudgement.None?"未答":finding.Judgement!=expected?"判断不符":
                    finding.EvidenceIds.Contains(use.Id) && (!present || finding.EvidenceIds.Contains(entry.Id))?"判断及引用相符":"判断相符，但引用不符";
                return "测漏对账 "+use.Id+"："+outcome+"。"+(present?"对应 "+entry.Id+"，登记字段存在。":"给定完整范围无对应登记。")+"只评价模拟记录关联，不证明实际执行或检测效果。";
            }).ToArray();
        }
    }
}
