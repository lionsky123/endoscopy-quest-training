using System;
using System.Collections.Generic;
using System.Linq;

namespace BotanicalGardenQR.Experience.Application
{
    // Fictional training records are published data, never hospital data or processing standards.
    // Resource loading is kept in the Unity-facing Bootstrap layer; this query model remains engine-free.
    public static class ClinicalTrainingRecords
    {
        sealed class Content
        {
            public readonly string Version;
            public readonly FieldDefinition[] Fields;
            public readonly Row[] Rows;
            public readonly DocumentDefinition[] Documents;

            public Content(string version,FieldDefinition[] fields,Row[] rows,DocumentDefinition[] documents)
            {
                Version=version;Fields=fields;Rows=rows;Documents=documents;
            }
        }

        static Content _content;
        static Content Data=>_content??throw new InvalidOperationException("Office training records have not been configured.");

        public static bool IsConfigured=>_content!=null;
        public static string Version=>Data.Version;
        public static int FieldCount=>Data.Fields.Length;
        public static int DocumentCount=>Data.Documents.Length;

        public sealed class FieldDefinition
        {
            public string Id {get;}
            public string Heading {get;}
            public FieldDefinition(string id,string heading){Id=id;Heading=heading;}
        }

        public sealed class DocumentDefinition
        {
            readonly LeakEntry[] _leakEntries;
            public string Id {get;}
            public string Title {get;}
            public string Body {get;}
            public DocumentDefinition(string id,string title,string body,LeakEntry[] leakEntries=null)
            {Id=id;Title=title;Body=body;_leakEntries=(leakEntries??new LeakEntry[0]).ToArray();}
            public LeakEntry[] LeakEntries=>_leakEntries.ToArray();
        }

        public sealed class LeakEntry
        {
            public string Id {get;}
            public string UseId {get;}
            public string Time {get;}
            public string Operator {get;}
            public string Result {get;}
            public LeakEntry(string id,string useId,string time,string person,string result)
            {Id=id;UseId=useId;Time=time;Operator=person;Result=result;}
        }

        public sealed class Row
        {
            readonly string[] _values;
            public string Id {get;}
            public string Room {get;}
            public string LeakId {get;}
            public string Date=>Value("date");
            public string Patient=>Value("patient");
            public string Scope=>Value("scope");
            public string Start=>Value("start");
            public string End=>Value("end");
            public string Operator=>Value("operator");

            public Row(string id,string[] values,string room,string leakId)
            {
                Id=id;_values=(values??new string[0]).ToArray();Room=room;LeakId=leakId;
            }

            public string Field(int index)
            {
                if(index<0 || index>=FieldCount)throw new ArgumentOutOfRangeException(nameof(index));
                return index<_values.Length?_values[index]??string.Empty:string.Empty;
            }

            public string Value(string fieldId)
            {
                var index=IndexOfField(fieldId);
                return index<0?string.Empty:Field(index);
            }
        }

        public static void Configure(string version,FieldDefinition[] fields,Row[] rows,DocumentDefinition[] documents)
        {
            Validate(version,fields,rows,documents);
            _content=new Content(version,fields.ToArray(),rows.ToArray(),documents.ToArray());
        }

        public static FieldDefinition Field(int index)
        {
            if(index<0 || index>=FieldCount)throw new ArgumentOutOfRangeException(nameof(index));
            return Data.Fields[index];
        }

        public static string Criterion(int index)=>Field(index).Id;
        public static string Heading(int index)=>Field(index).Heading;
        public static string DocumentTitle(int index)=>Document(index).Title;
        public static string DocumentBody(int index)=>Document(index).Body;
        public static bool IsLeakDocument(int index)=>Document(index).Id=="leak";
        public static LeakEntry[] LeakEntries(int index)=>Document(index).LeakEntries;

        public static int DocumentIndexForTask(string taskId)
        {
            string id;
            switch(taskId)
            {
                case "OF-00":id="disinfection";break;
                case "OF-02":id="leak";break;
                case "OF-03":id="biological";break;
                case "OF-04":id="disinfectant";break;
                case "OF-05":id="training";break;
                default:return -1;
            }
            for(int i=0;i<DocumentCount;i++)if(Data.Documents[i].Id==id)return i;
            return -1; // Missing evidence must not silently open a different document.
        }

        public static Row[] Query(string date=null,string scope=null,string room=null)
            =>Data.Rows.Where(r=>(string.IsNullOrEmpty(date)||r.Date==date)&&
                (string.IsNullOrEmpty(scope)||r.Scope==scope)&&
                (string.IsNullOrEmpty(room)||r.Room==room)).ToArray();

        static DocumentDefinition Document(int index)
        {
            if(index<0 || index>=DocumentCount)throw new ArgumentOutOfRangeException(nameof(index));
            return Data.Documents[index];
        }

        static int IndexOfField(string id)
        {
            for(var i=0;i<FieldCount;i++)if(Data.Fields[i].Id==id)return i;
            return -1;
        }

        static void Validate(string version,FieldDefinition[] fields,Row[] rows,DocumentDefinition[] documents)
        {
            if(string.IsNullOrWhiteSpace(version))throw new InvalidOperationException("Office training record data has no version.");
            if(fields==null || fields.Length==0)throw new InvalidOperationException("Office training record data has no fields.");
            if(rows==null || documents==null)throw new InvalidOperationException("Office training record data is incomplete.");
            var fieldIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var field in fields)
                if(field==null || string.IsNullOrWhiteSpace(field.Id) || string.IsNullOrWhiteSpace(field.Heading) || !fieldIds.Add(field.Id))
                    throw new InvalidOperationException("Office training record field ids must be present and unique.");
            var rowIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in rows)
                if(row==null || string.IsNullOrWhiteSpace(row.Id) || !rowIds.Add(row.Id))
                    throw new InvalidOperationException("Office training record row ids must be present and unique.");
            var documentIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var document in documents)
            {
                if(document==null || string.IsNullOrWhiteSpace(document.Id) || string.IsNullOrWhiteSpace(document.Title) ||
                   string.IsNullOrWhiteSpace(document.Body) || !documentIds.Add(document.Id))
                    throw new InvalidOperationException("Office training record document ids and bodies must be present and unique.");
                var entryIds=new HashSet<string>(StringComparer.Ordinal);
                var uses=new HashSet<string>(StringComparer.Ordinal);
                foreach(var entry in document.LeakEntries)
                {
                    if(document.Id!="leak" || entry==null || string.IsNullOrWhiteSpace(entry.Id) || !entryIds.Add(entry.Id) ||
                        string.IsNullOrWhiteSpace(entry.UseId) || !uses.Add(entry.UseId) ||
                        string.IsNullOrWhiteSpace(entry.Time) || string.IsNullOrWhiteSpace(entry.Operator) || string.IsNullOrWhiteSpace(entry.Result) ||
                        !rows.Any(row=>row.Id==entry.UseId && row.LeakId==entry.Id))
                        throw new InvalidOperationException("Leak entries require unique ids and matching published use records.");
                }
            }
        }
    }
}
