using System.Linq;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalTrainingRecordsTests
    {
        [SetUp]
        public void LoadPublishedRecords()
        {
            ClinicalTrainingRecordsConfiguration.Load();
        }

        [Test]public void StorageWeekReviewRequiresSubmissionAndDoesNotInferCleaningFailure()
        {
            var definition=ClinicalJourneyConfiguration.Load();var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.IndependentCheck);
            session.TrySwitchRoom("R01_OFFICE",true,true);session.TrySwitchRoom("R02_STORAGE",true,true);
            Assert.That(session.TrySetIndependentJudgement("ST-02",ClinicalJourneyJudgement.NoIssue),Is.False);
            Assert.That(StorageCleaningRecords.AfterSubmission(session),Is.Empty);
            Assert.That(StorageCleaningRecords.Cell(2,1,false),Is.Not.Empty);
            Assert.That(StorageCleaningRecords.Cell(2,1,true),Is.Empty);
            Assert.That(session.TryRecordFinding("ST-02",StorageCleaningRecords.Criterion(2),ClinicalJourneyJudgement.IssueFound,new[]{StorageCleaningRecords.Evidence(1)}),Is.True);
            foreach(var id in definition.mainlineRoomIds.Skip(3))Assert.That(session.TrySwitchRoom(id,true,true).Succeeded,Is.True,id);
            Assert.That(session.TrySubmitIndependentAtSummary(),Is.True);
            var review=StorageCleaningRecords.AfterSubmission(session);
            Assert.That(review,Has.Length.EqualTo(4));Assert.That(review[0],Does.Contain("未答"));
            Assert.That(review[2],Does.Contain("引用周期不符"));Assert.That(review[2],Does.Contain("不能据此断言未清洁"));
            var fresh=new ClinicalJourneySession(definition,ClinicalJourneyMode.IndependentCheck);
            Assert.That(fresh.GetFindings("ST-02"),Is.Empty);
        }
        [Test]public void DocumentRoutingUsesIdentityRatherThanPublishedOrderAndFailsClosed()
        {
            var ids=new[]{"training","product","biological","leak","disinfection","disinfectant"};
            try
            {
                ClinicalTrainingRecords.Configure("routing-test",new[]{new ClinicalTrainingRecords.FieldDefinition("date","日期")},
                    new ClinicalTrainingRecords.Row[0],ids.Select(id=>new ClinicalTrainingRecords.DocumentDefinition(id,id,id+" body")).ToArray());
                foreach(var pair in new[]{new[]{"OF-00","disinfection"},new[]{"OF-02","leak"},new[]{"OF-03","biological"},new[]{"OF-04","disinfectant"},new[]{"OF-05","training"}})
                    Assert.That(ClinicalTrainingRecords.DocumentTitle(ClinicalTrainingRecords.DocumentIndexForTask(pair[0])),Is.EqualTo(pair[1]));
                Assert.That(ClinicalTrainingRecords.DocumentIndexForTask("OF-01"),Is.EqualTo(-1));
                Assert.That(ClinicalTrainingRecords.DocumentIndexForTask("unknown"),Is.EqualTo(-1));
                ClinicalTrainingRecords.Configure("missing",new[]{new ClinicalTrainingRecords.FieldDefinition("date","日期")},
                    new ClinicalTrainingRecords.Row[0],new[]{new ClinicalTrainingRecords.DocumentDefinition("training","培训","正文")});
                Assert.That(ClinicalTrainingRecords.DocumentIndexForTask("OF-02"),Is.EqualTo(-1));
            }
            finally {ClinicalTrainingRecordsConfiguration.Load();}
        }
        [Test]public void LeakEntriesPreservePublishedValuesAndValidateUseLinks()
        {
            int index=ClinicalTrainingRecords.DocumentIndexForTask("OF-02");
            var entries=ClinicalTrainingRecords.LeakEntries(index);
            Assert.That(entries.Select(e=>e.Id),Is.EqualTo(new[]{"SIM-L001","SIM-L002","SIM-L003"}));
            Assert.That(entries.Select(e=>e.Time),Is.EqualTo(new[]{"08:05","09:00","09:55"}));
            foreach(var entry in entries)Assert.That(ClinicalTrainingRecords.Query().Single(r=>r.Id==entry.UseId).LeakId,Is.EqualTo(entry.Id));
            entries[0]=null;Assert.That(ClinicalTrainingRecords.LeakEntries(index)[0],Is.Not.Null);
            var fields=Enumerable.Range(0,ClinicalTrainingRecords.FieldCount).Select(ClinicalTrainingRecords.Field).ToArray();
            var rows=ClinicalTrainingRecords.Query();
            var invalid=new ClinicalTrainingRecords.LeakEntry("SIM-L001","SIM-R002","08:05","训练人员甲","未见泄漏");
            Assert.Throws<System.InvalidOperationException>(()=>ClinicalTrainingRecords.Configure("invalid",fields,rows,
                new[]{new ClinicalTrainingRecords.DocumentDefinition("leak","测漏","正文",new[]{invalid})}));
            Assert.That(ClinicalTrainingRecords.Version,Is.EqualTo("SIM-20260921-2"),"Rejected replacement must not mutate active data");
        }
        [Test]public void FiltersPreserveOneSourceAndEmptyResultsAreNotFabricated()
        {
            var rows=ClinicalTrainingRecords.Query();Assert.That(rows,Has.Length.EqualTo(3));
            Assert.That(ClinicalTrainingRecords.Query("2026-09-20"),Has.Length.EqualTo(2));
            Assert.That(ClinicalTrainingRecords.Query(scope:"DEMO-GI-001").Select(r=>r.Id),Is.EqualTo(new[]{"SIM-R001","SIM-R003"}));
            Assert.That(ClinicalTrainingRecords.Query("2026-09-21","DEMO-RESP-001"),Is.Empty);
            Assert.That(ClinicalTrainingRecords.Query(room:"RESP").Single(),Is.SameAs(rows[1]));
            rows[0]=null;Assert.That(ClinicalTrainingRecords.Query()[0],Is.Not.Null);
            Assert.That(ClinicalTrainingRecords.Query()[1].Operator,Is.Empty);
        }
        [Test]public void CriteriaCannotBeBypassedAndReviewWaitsForOfficeSubmission()
        {
            var definition=ClinicalJourneyConfiguration.Load();var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.IndependentCheck);
            session.TrySwitchRoom("R01_OFFICE",true,true);
            Assert.That(session.TrySetIndependentJudgement("OF-01",ClinicalJourneyJudgement.NoIssue),Is.False);
            Assert.That(session.TryRecordFinding("OF-01","operator",ClinicalJourneyJudgement.IssueFound,new[]{"SIM-R001"}),Is.True);
            Assert.That(ClinicalRecordReview.AfterSubmission(session),Is.Empty);
            Assert.That(session.TryRecordFinding("OF-01","date",ClinicalJourneyJudgement.NoIssue,new[]{"SIM-R001"}),Is.True);
            session.TryGetTask("OF-01",out var task);Assert.That(task.Status,Is.EqualTo(ClinicalJourneyTaskStatus.InProgress));
            Assert.That(task.Judgement,Is.EqualTo(ClinicalJourneyJudgement.IssueFound));
            foreach(var id in definition.mainlineRoomIds.Skip(2))Assert.That(session.TrySwitchRoom(id,true,true).Succeeded,Is.True,id);
            Assert.That(session.TrySubmitIndependentAtSummary(),Is.True);
            var review=ClinicalRecordReview.AfterSubmission(session);
            Assert.That(review,Has.Length.EqualTo(13));Assert.That(review[5],Does.Contain("引用行不符"));Assert.That(review[1],Does.Contain("未答"));
            Assert.That(session.TryRecordFinding("OF-01","operator",ClinicalJourneyJudgement.IssueFound,new[]{"SIM-R002"}),Is.False);
        }
        [Test]public void LeakReviewRequiresSubmissionAndChecksBothEvidenceIds()
        {
            var definition=ClinicalJourneyConfiguration.Load();var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.IndependentCheck);
            session.TrySwitchRoom("R01_OFFICE",true,true);
            Assert.That(session.TrySetIndependentJudgement("OF-02",ClinicalJourneyJudgement.NoIssue),Is.False);
            Assert.That(ClinicalRecordReview.LeakAfterSubmission(session),Is.Empty);
            Assert.That(session.TryRecordFinding("OF-02",ClinicalRecordReview.LeakCriterion("SIM-R001"),ClinicalJourneyJudgement.NoIssue,new[]{"SIM-R001","SIM-L001"}),Is.True);
            Assert.That(session.TryRecordFinding("OF-02",ClinicalRecordReview.LeakCriterion("SIM-R002"),ClinicalJourneyJudgement.NoIssue,new[]{"SIM-R001","SIM-L001"}),Is.True);
            foreach(var room in definition.mainlineRoomIds.Skip(2))Assert.That(session.TrySwitchRoom(room,true,true).Succeeded,Is.True);
            Assert.That(session.TrySubmitIndependentAtSummary(),Is.True);
            var review=ClinicalRecordReview.LeakAfterSubmission(session);
            Assert.That(review[0],Does.Contain("判断及引用相符"));Assert.That(review[1],Does.Contain("引用不符"));Assert.That(review[2],Does.Contain("未答"));
            Assert.That(review.All(text=>text.Contains("不证明实际执行")),Is.True);
            Assert.That(session.TryRecordFinding("OF-02",ClinicalRecordReview.LeakCriterion("SIM-R003"),ClinicalJourneyJudgement.NoIssue,new[]{"SIM-R003","SIM-L003"}),Is.False);
            Assert.That(new ClinicalJourneySession(definition,ClinicalJourneyMode.IndependentCheck).GetFindings("OF-02"),Is.Empty);
        }
    }
}
