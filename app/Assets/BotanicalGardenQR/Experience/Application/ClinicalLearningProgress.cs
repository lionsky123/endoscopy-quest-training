using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    public enum ClinicalLearningAction { Opened, Observed, Operated, MethodShown, Hint, Explanation }

    public readonly struct ClinicalLearningAttempt
    {
        public readonly int Attempts, Revisions, IncorrectAttempts;
        public readonly bool Correct, HintUsed, ExplanationUsed;
        internal ClinicalLearningAttempt(int attempts,int revisions,int incorrect,bool correct,bool hint,bool explanation)
        {Attempts=attempts;Revisions=revisions;IncorrectAttempts=incorrect;Correct=correct;HintUsed=hint;ExplanationUsed=explanation;}
    }

    public sealed partial class ClinicalJourneySession
    {
        sealed class LearningEntry
        {
            internal int Attempts,Revisions,Incorrect;
            internal bool Correct,Hint,Explanation;
            internal ClinicalJourneyJudgement Judgement;
            internal string[] Evidence=Array.Empty<string>();
        }
        readonly Dictionary<string,LearningEntry> _learning=new Dictionary<string,LearningEntry>(StringComparer.Ordinal);
        readonly HashSet<string> _learningActions=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _learningMethods=new HashSet<string>(StringComparer.Ordinal);
        static string LearningKey(string task,string criterion)=>task+"/"+criterion;
        bool CanLearnHere(string task)=>Mode==ClinicalJourneyMode.GuidedLearning && CanEdit &&
            (TaskIsInCurrentRoom(task) || task=="OF-02" && CurrentRoomId=="R05_REPROCESSING");
        public bool TryRecordLearningAction(string task,ClinicalLearningAction kind,string detail)
        {
            if(!CanLearnHere(task) || string.IsNullOrWhiteSpace(detail) || _definition.FindTask(task)==null)return false;
            return _learningActions.Add(task+"/"+(int)kind+"/"+detail);
        }
        public int LearningActionCount(string task,ClinicalLearningAction kind)
            =>_learningActions.Count(item=>item.StartsWith(task+"/"+(int)kind+"/",StringComparison.Ordinal));
        public int LearningActionCount(ClinicalLearningAction kind)
            =>_definition.tasks.Sum(task=>LearningActionCount(task.id,kind));
        public bool TryIntroduceLearningMethod(string task,string method)
        {
            if(!CanLearnHere(task) || !IsContentAvailable(task) || string.IsNullOrWhiteSpace(method) || !_learningMethods.Add(method))return false;
            TryRecordLearningAction(task,ClinicalLearningAction.MethodShown,method);return true;
        }
        public ClinicalLearningAttempt GetLearningAttempt(string task,string criterion)
            =>_learning.TryGetValue(LearningKey(task,criterion),out var entry)
                ?new ClinicalLearningAttempt(entry.Attempts,entry.Revisions,entry.Incorrect,entry.Correct,entry.Hint,entry.Explanation):default;
        public ClinicalLearningAttempt[] LearningAttempts(string task=null)
            =>_learning.Where(pair=>task==null || pair.Key.StartsWith(task+"/",StringComparison.Ordinal))
                .Select(pair=>new ClinicalLearningAttempt(pair.Value.Attempts,pair.Value.Revisions,pair.Value.Incorrect,
                    pair.Value.Correct,pair.Value.Hint,pair.Value.Explanation)).ToArray();
        bool LearningCriteriaComplete(string task)
            =>(_definition.FindTask(task)?.criterionIds??Array.Empty<string>()).All(id=>GetLearningAttempt(task,id).Correct);

        public bool TryRecordGuidedFinding(string task,string criterion,ClinicalJourneyJudgement judgement,string[] evidence)
        {
            if(!CanLearnHere(task) || !IsContentAvailable(task) ||
                judgement!=ClinicalJourneyJudgement.NoIssue && judgement!=ClinicalJourneyJudgement.IssueFound ||
                !ClinicalLearningQuestion.TryGet(task,criterion,out var question))return false;
            var definition=_definition.FindTask(task);
            if(Array.IndexOf(definition.criterionIds??Array.Empty<string>(),criterion)<0 || evidence==null || evidence.Length==0 ||
                evidence.Any(id=>Array.IndexOf(definition.evidenceIds??Array.Empty<string>(),id)<0))return false;
            var key=LearningKey(task,criterion);
            if(!_learning.TryGetValue(key,out var entry))_learning.Add(key,entry=new LearningEntry());
            var selected=evidence.Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            if(entry.Attempts>0 && entry.Judgement==judgement && entry.Evidence.SequenceEqual(selected))return false;
            if(entry.Attempts>0)entry.Revisions++;
            entry.Attempts++;entry.Judgement=judgement;entry.Evidence=selected;
            entry.Correct=question.Matches(judgement,selected);
            if(!entry.Correct){entry.Incorrect++;entry.Hint=true;TryRecordLearningAction(task,ClinicalLearningAction.Hint,criterion);}
            var record=_tasks[task];record.Findings[criterion]=new ClinicalFinding(criterion,judgement,selected);
            record.Status=LearningCriteriaComplete(task)?ClinicalJourneyTaskStatus.Completed:ClinicalJourneyTaskStatus.InProgress;
            record.Judgement=record.Findings.Values.Any(f=>f.Judgement==ClinicalJourneyJudgement.IssueFound)
                ?ClinicalJourneyJudgement.IssueFound:ClinicalJourneyJudgement.NoIssue;
            return true;
        }
        public bool TryRequestLearningHelp(string task,string criterion,bool explanation)
        {
            var definition=_definition.FindTask(task);
            if(!CanLearnHere(task) || !IsContentAvailable(task) ||
                Array.IndexOf(definition?.criterionIds??Array.Empty<string>(),criterion)<0 ||
                !ClinicalLearningQuestion.TryGet(task,criterion,out _))return false;
            var key=LearningKey(task,criterion);
            if(!_learning.TryGetValue(key,out var entry))_learning.Add(key,entry=new LearningEntry());
            if(explanation)entry.Explanation=true;else entry.Hint=true;
            TryRecordLearningAction(task,explanation?ClinicalLearningAction.Explanation:ClinicalLearningAction.Hint,criterion);
            return true;
        }
    }

    // Only evaluates published fictional record completeness/association. No
    // equipment settings, clinical efficacy or missing real-world evidence is graded.
    public sealed class ClinicalLearningQuestion
    {
        public string Title {get;}
        public string Hint {get;}
        public string Explanation {get;}
        readonly ClinicalJourneyJudgement _expected;
        readonly Func<string[],bool> _evidenceMatches;
        ClinicalLearningQuestion(string title,string hint,string explanation,ClinicalJourneyJudgement expected,Func<string[],bool> evidence)
        {Title=title;Hint=hint;Explanation=explanation;_expected=expected;_evidenceMatches=evidence;}
        public bool Matches(ClinicalJourneyJudgement answer,string[] evidence)=>answer==_expected && _evidenceMatches(evidence);
        public static bool TryGet(string task,string criterion,out ClinicalLearningQuestion question)
        {
            question=null;
            if(task=="OF-01" && ClinicalTrainingRecords.IsConfigured)
            {
                int field=Enumerable.Range(0,ClinicalTrainingRecords.FieldCount).Where(i=>ClinicalTrainingRecords.Criterion(i)==criterion).DefaultIfEmpty(-1).First();
                if(field<0)return false;
                var rows=ClinicalTrainingRecords.Query();
                if(rows.Length==0)return false;
                var missing=rows.Where(row=>string.IsNullOrEmpty(row.Field(field))).Select(row=>row.Id).ToArray();
                question=new ClinicalLearningQuestion("完整记录范围内，"+ClinicalTrainingRecords.Heading(field)+"是否有缺项？",
                    "沿这一列逐行对照；若发现空白，请返回资料并选中空白所在记录作为引用。",
                    missing.Length==0?"完整范围内这一列均有值。这里只检查字段完整性，不判断实际清洗消毒效果。":
                        string.Join("、",missing)+"的这一字段为空。应引用空白所在记录；缺项不等于已证明未执行操作。",
                    missing.Length==0?ClinicalJourneyJudgement.NoIssue:ClinicalJourneyJudgement.IssueFound,
                    ids=>missing.Length==0?ids.Any(id=>rows.Any(row=>row.Id==id)):ids.Any(missing.Contains));
                return true;
            }
            if(task=="ST-02")
            {
                int week=Enumerable.Range(0,StorageCleaningRecords.Count).Where(i=>StorageCleaningRecords.Criterion(i)==criterion).DefaultIfEmpty(-1).First();
                if(week<0)return false;
                bool missing=Enumerable.Range(1,4).Any(column=>string.IsNullOrEmpty(StorageCleaningRecords.Cell(week,column,true)));
                question=new ClinicalLearningQuestion(StorageCleaningRecords.Period(week)+"这一周的登记是否有缺项？",
                    "对照本周的日期、对象、登记内容和操作人；不要把对象名称当成完整登记。",
                    missing?"本周日期、登记内容和操作人为空，只能判断登记缺项，不能断言没有清洁。":"本周登记字段有值；这不证明清洁效果，也不把模拟每周安排当作临床标准。",
                    missing?ClinicalJourneyJudgement.IssueFound:ClinicalJourneyJudgement.NoIssue,ids=>ids.Contains(StorageCleaningRecords.Evidence(week)));
                return true;
            }
            if(task=="OF-02" && ClinicalTrainingRecords.IsConfigured)
            {
                var use=ClinicalTrainingRecords.Query().FirstOrDefault(row=>ClinicalRecordReview.LeakCriterion(row.Id)==criterion);
                int document=ClinicalTrainingRecords.DocumentIndexForTask("OF-02");
                if(use==null || document<0)return false;
                var entry=ClinicalTrainingRecords.LeakEntries(document).FirstOrDefault(item=>item.UseId==use.Id);
                question=new ClinicalLearningQuestion("使用记录 "+use.Id+" 是否有对应测漏登记？",
                    "按使用号与镜号逐项对照完整登记表；不要仅凭时间相近认定为同一次使用。",
                    entry==null?"给定完整登记范围内未找到这次使用的对应登记；这不等于已证明没有测漏。":
                        "这次使用对应登记 "+entry.Id+"。这里只核对模拟记录关联，不证明实际检测效果。",
                    entry==null?ClinicalJourneyJudgement.IssueFound:ClinicalJourneyJudgement.NoIssue,
                    ids=>ids.Contains(use.Id) && (entry==null || ids.Contains(entry.Id)));
                return true;
            }
            return false;
        }
    }
}
