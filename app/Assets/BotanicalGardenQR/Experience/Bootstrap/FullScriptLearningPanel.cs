using System;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed partial class FullScriptRoomVisit
    {
        internal event Action<FullScriptGuideCopy,Action> TeachingRequested;
        string _practiceTask,_practiceCriterion;
        string[] _practiceEvidence;
        Action _practiceReturn;
        ClinicalLearningQuestion _practiceQuestion;
        internal void CloseLearningPractice()=>ClosePanel();

        internal void ShowTeaching(FullScriptGuideCopy copy,Action resume)
        {
            if(!InputAllowed)return;
            ClosePanel();_office?.EndView();
            if(TeachingRequested!=null)TeachingRequested(copy,resume);
            else resume();
        }
        internal void BeginRecordPractice(string task,string criterion,string[] evidence,Action returnToRecords)
        {
            if(!InputAllowed || !ClinicalLearningQuestion.TryGet(task,criterion,out var question))return;
            _practiceTask=task;_practiceCriterion=criterion;_practiceEvidence=(string[])evidence.Clone();
            _practiceReturn=returnToRecords;_practiceQuestion=question;
            _office?.EndView();ClosePanel();
            string method=task=="OF-01"?"record-fields":task=="ST-02"?"register-range":"record-link";
            if(_owner.Session.TryIntroduceLearningMethod(task,method))
                ShowTeaching(new FullScriptGuideCopy("核查方法",question.Hint,"开始判断"),ShowRecordPractice);
            else ShowRecordPractice();
        }
        void ShowRecordPractice()
        {
            if(!InputAllowed)return;
            ClosePanel();EnsureScriptTextMaterial();EnsureScriptPose();
            _panel=new GameObject("RecordLearningPractice",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;board.sizeDelta=new Vector2(860,570);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;
            Fill(board,new Color(.96f,.98f,1),3);
            void Copy(string name,string body,float y,float height,int size)
            {
                var text=Label(board,_font,name,0,y,800,height,size);text.fontSharedMaterial=_scriptTextMaterial;
                text.text=body;text.color=new Color(.04f,.12f,.18f);
            }
            var attempt=_owner.Session.GetLearningAttempt(_practiceTask,_practiceCriterion);
            Copy("LearningQuestion",_practiceQuestion.Title,211,108,29);
            Copy("LearningEvidence","引用模拟记录："+string.Join("、",_practiceEvidence),129,48,20);
            Copy("LearningFeedback",attempt.Attempts==0?"先查看资料，再作出判断。需要时可以请求提示。":attempt.Correct?
                "判断与引用相符。可返回资料继续核对其他项目。":_practiceQuestion.Hint,58,84,23);
            Button Control(string name,string label,float x,float y,Action action)
            {
                var button=Button(board,_font,name,label,x,y,375,58,()=>QueueStationaryAction(action));
                button.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;return button;
            }
            var noIssue=Control("LearningNoIssue",_practiceTask=="OF-02"?"有对应登记":"未发现缺项",-202,-37,()=>AnswerPractice(ClinicalJourneyJudgement.NoIssue));
            var issue=Control("LearningIssue",_practiceTask=="OF-02"?"未找到对应登记":"发现缺项",202,-37,()=>AnswerPractice(ClinicalJourneyJudgement.IssueFound));
            noIssue.interactable=issue.interactable=_owner.Session.CanEdit;
            Control("LearningHint","给我一个提示",-202,-112,()=>PracticeHelp(false));
            Control("LearningExplanation","查看完整讲解",202,-112,()=>PracticeHelp(true));
            Control("LearningReturn","返回资料再看",-202,-211,()=>_practiceReturn());
            var skip=Control("LearningSkip","跳过这项检查",202,-211,()=>
            {_owner.Session.TrySkipGuidedTask(_practiceTask);_practiceReturn();});
            skip.interactable=_owner.Session.CanEdit;
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }
        void AnswerPractice(ClinicalJourneyJudgement answer)
        {
            if(!_owner.Session.TryRecordGuidedFinding(_practiceTask,_practiceCriterion,answer,_practiceEvidence))return;
            var result=_owner.Session.GetLearningAttempt(_practiceTask,_practiceCriterion);
            ShowTeaching(new FullScriptGuideCopy("核查反馈",result.Correct?
                "判断与引用相符。这只说明模拟记录的核对结果，不代表实际操作效果。":_practiceQuestion.Hint,
                result.Correct?"返回本项":"重新判断"),ShowRecordPractice);
        }
        void PracticeHelp(bool explanation)
        {
            if(!_owner.Session.TryRequestLearningHelp(_practiceTask,_practiceCriterion,explanation))return;
            ShowTeaching(new FullScriptGuideCopy(explanation?"核查讲解":"观察提示",
                explanation?_practiceQuestion.Explanation:_practiceQuestion.Hint,"返回判断"),ShowRecordPractice);
        }
    }
}
