namespace BotanicalGardenQR.FrontendShell.Contracts
{
    // Run-owned learning facts only: no textures, poses, tools or media handles.
    public sealed class ClinicalObservationProgress
    {
        readonly int[] _topics = new int[3];
        public bool Started { get; private set; }
        public int CompletedCount => Count(1);
        public int SkippedCount => Count(2);
        public int NextTopic
        {
            get { for(int i=0;i<_topics.Length;i++) if(_topics[i]==0)return i;return _topics.Length; }
        }
        public void Begin() { Started=true; }
        public bool Record(int topic,bool skipped)
        {
            if(!Started || topic!=NextTopic || topic<0 || topic>=_topics.Length)return false;
            _topics[topic]=skipped?2:1;return true;
        }
        int Count(int state){int count=0;foreach(var topic in _topics)if(topic==state)count++;return count;}
    }
}
