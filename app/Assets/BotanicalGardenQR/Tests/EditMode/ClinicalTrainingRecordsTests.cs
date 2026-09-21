using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalTrainingRecordsTests
    {
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
            Assert.That(review,Has.Length.EqualTo(6));Assert.That(review[5],Does.Contain("引用行不符"));Assert.That(review[1],Does.Contain("未答"));
            Assert.That(session.TryRecordFinding("OF-01","operator",ClinicalJourneyJudgement.IssueFound,new[]{"SIM-R002"}),Is.False);
        }
    }
}
