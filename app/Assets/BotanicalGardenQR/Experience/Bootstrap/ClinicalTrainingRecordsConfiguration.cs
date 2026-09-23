using System;
using System.Linq;
using BotanicalGardenQR.Experience.Application;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    // Unity-facing adapter for the versioned office record data.
    public static class ClinicalTrainingRecordsConfiguration
    {
        const string ResourcePath="ClinicalCourse/office-training-records";

        [Serializable]
        sealed class Payload
        {
            public string version;
            public FieldData[] fields;
            public RowData[] rows;
            public DocumentData[] documents;
        }

        [Serializable]
        sealed class FieldData
        {
            public string id;
            public string title;
        }

        [Serializable]
        sealed class RowData
        {
            public string id;
            public string[] values;
            public string room;
            public string leakId;
        }

        [Serializable]
        sealed class DocumentData
        {
            public string id;
            public string title;
            public string body;
            public LeakData[] leakEntries;
        }
        [Serializable] sealed class LeakData
        {
            public string id,useId,time,person,result;
        }

        public static void Load()
        {
            var asset=Resources.Load<TextAsset>(ResourcePath);
            if(!asset)throw new InvalidOperationException("Office training record data is missing: "+ResourcePath);
            var payload=JsonUtility.FromJson<Payload>(asset.text);
            if(payload==null)throw new InvalidOperationException("Office training record data is empty: "+ResourcePath);
            ClinicalTrainingRecords.Configure(payload.version,
                (payload.fields??new FieldData[0]).Select(field=>new ClinicalTrainingRecords.FieldDefinition(field.id,field.title)).ToArray(),
                (payload.rows??new RowData[0]).Select(row=>new ClinicalTrainingRecords.Row(row.id,row.values,row.room,row.leakId)).ToArray(),
                (payload.documents??new DocumentData[0]).Select(document=>new ClinicalTrainingRecords.DocumentDefinition(document.id,document.title,document.body,
                    (document.leakEntries??new LeakData[0]).Select(entry=>new ClinicalTrainingRecords.LeakEntry(entry.id,entry.useId,entry.time,entry.person,entry.result)).ToArray())).ToArray());
        }
    }
}
