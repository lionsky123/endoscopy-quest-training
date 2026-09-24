using System;
using System.Linq;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Experience.Application;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalLeakSourceTests
    {
        [SetUp] public void LoadSource()=>ClinicalTrainingRecordsConfiguration.Load();

        [Test] public void PublishedUsesResolveOnlyTheirOwnRegisterEntries()
        {
            var uses=ClinicalTrainingRecords.Query();
            var entries=ClinicalTrainingRecords.LeakEntries(ClinicalTrainingRecords.DocumentIndexForTask("OF-02"));
            Assert.That(uses.Select(use=>use.Id),Is.Unique);
            foreach(var use in uses)
            {
                var entry=ClinicalTrainingRecords.LeakEntryForUse(use.Id);
                Assert.That(entry,Is.Not.Null,use.Id);
                Assert.That(entry.Id,Is.EqualTo(use.LeakId));
                Assert.That(entries.Count(item=>item.UseId==use.Id),Is.EqualTo(1));
            }
            Assert.That(ClinicalTrainingRecords.LeakEntryForUse("unused-table-row"),Is.Null);
        }

        [Test] public void MissingEntryStaysMissingAndDanglingPublishedLinkIsRejected()
        {
            var fields=new[]{new ClinicalTrainingRecords.FieldDefinition("date","日期")};
            var available=new ClinicalTrainingRecords.Row("USE-1",new[]{"2026-09-20"},"GI","LEAK-1");
            var missing=new ClinicalTrainingRecords.Row("USE-2",new[]{"2026-09-20"},"GI","");
            var document=new ClinicalTrainingRecords.DocumentDefinition("leak","逐次测漏","模拟训练记录",
                new[]{new ClinicalTrainingRecords.LeakEntry("LEAK-1","USE-1","08:00","训练人员","未见泄漏")});
            try
            {
                ClinicalTrainingRecords.Configure("test-missing",fields,new[]{available,missing},new[]{document});
                Assert.That(ClinicalTrainingRecords.LeakEntryForUse(missing.Id),Is.Null);
                Assert.Throws<InvalidOperationException>(()=>ClinicalTrainingRecords.Configure("test-dangling",fields,
                    new[]{available,new ClinicalTrainingRecords.Row("USE-2",new[]{"2026-09-20"},"GI","LEAK-2")},new[]{document}));
                Assert.That(ClinicalTrainingRecords.Version,Is.EqualTo("test-missing"));
            }
            finally {ClinicalTrainingRecordsConfiguration.Load();}
        }
    }
}
