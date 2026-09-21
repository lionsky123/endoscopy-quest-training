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
            if(session==null || !session.IsSubmitted)return Array.Empty<string>();
            var rows=ClinicalTrainingRecords.Query();var findings=session.GetFindings("OF-01");
            return Enumerable.Range(0,6).Select(index=>
            {
                var finding=findings.FirstOrDefault(f=>f.CriterionId==ClinicalTrainingRecords.Criterion(index));
                var missing=rows.Where(r=>string.IsNullOrEmpty(r.Field(index))).Select(r=>r.Id).ToArray();
                string outcome;
                if(finding.Judgement==ClinicalJourneyJudgement.None)outcome="未答";
                else if(missing.Length==0)outcome=finding.Judgement==ClinicalJourneyJudgement.NoIssue?"判断相符":"误报";
                else if(finding.Judgement==ClinicalJourneyJudgement.NoIssue)outcome="漏检";
                else outcome=finding.EvidenceIds.Any(id=>missing.Contains(id))?"判断及引用相符":"发现缺项，但引用行不符";
                return ClinicalTrainingRecords.Heading(index)+"："+outcome+"；"+(missing.Length==0?"完整范围3行均有值":string.Join(",",missing)+"原值为空");
            }).ToArray();
        }
    }
}
