using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Video.Backend;
using BotanicalGardenQR.Video.Contracts;
using BotanicalGardenQR.Video.Frontend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed partial class FullScriptRoomVisit : IVideoStateSink
    {
        internal const string SinkVideoPath = "ClinicalCourse/sink-trigger-04m46s-05m06s-with-audio.mp4";
        internal Func<Transform, IVideoController> SinkVideoFactory = root => VideoModuleFactory.Create(root, new VideoRuntimeOptions(20f, ignoreListenerSilence:true));
        IVideoController _sinkVideo;
        IDisposable _sinkVideoObservation;
        SessionToken _sinkVideoSession;
        VideoPhase _sinkVideoPhase;
        TMP_Text _sinkVideoStatus;
        Button _sinkPause, _sinkReplay;

        void ShowSinkVideo()
        {
            if(!InputAllowed)return;
            ClosePanel();EnsureScriptTextMaterial();EnsureScriptPose();
            _panel=new GameObject("SinkVideoPanel",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.sizeDelta=new Vector2(860,650);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;
            Fill(board,new Color(.035f,.07f,.10f),3);
            var title=Label(board,_font,"VideoTitle",0,272,790,46,28);
            title.text="水槽操作 · 视频示范";title.color=Color.white;
            var stage=Rect(board,"VideoStage",0,24,800,450);
            _sinkVideoStatus=Label(board,_font,"VideoStatus",0,-222,790,40,20);
            _sinkVideoStatus.color=Color.white;_sinkVideoStatus.text="正在加载视频…";
            _sinkPause=Button(board,_font,"PauseSinkVideo","暂停",-280,-282,240,58,
                ()=>QueueStationaryAction(()=>_sinkVideo?.Dispatch(_sinkVideoSession,new VideoIntent(VideoIntentKind.TogglePlayback))));
            _sinkReplay=Button(board,_font,"ReplaySinkVideo","重新播放",0,-282,240,58,
                ()=>QueueStationaryAction(()=>
                {
                    if(_sinkVideoPhase==VideoPhase.Failed)ShowSinkVideo();
                    else _sinkVideo?.Dispatch(_sinkVideoSession,new VideoIntent(VideoIntentKind.Replay));
                }));
            Button(board,_font,"ReturnFromSinkVideo","返回检查",280,-282,240,58,()=>QueueStationaryAction(ShowScriptTask));
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
            foreach(var label in board.GetComponentsInChildren<TMP_Text>())label.fontSharedMaterial=_scriptTextMaterial;
            _sinkPause.interactable=false;_sinkReplay.interactable=false;
            var surface=VideoSurfaceTarget.Create(stage,new Vector2(800,450));
            var lease=new VideoSurfaceLease(surface,true,()=>
            {
                if(!surface)return;
                surface.Clear();
                if(Application.isPlaying)UnityEngine.Object.Destroy(surface.gameObject);
                else UnityEngine.Object.DestroyImmediate(surface.gameObject);
            });
            _sinkVideoSession=SessionToken.CreateNew();
            _sinkVideo=SinkVideoFactory(board);
            _sinkVideoObservation=_sinkVideo.Observe(this);
            _sinkVideo.Open(_sinkVideoSession,new VideoDefinition(VideoSource.FromStreamingAssetsPath(SinkVideoPath)),lease);
        }

        void IVideoStateSink.Publish(VideoState state)
        {
            if(state.Session!=_sinkVideoSession || !_sinkVideoStatus)return;
            _sinkVideoPhase=state.Phase;
            SetSinkVideoAudioActive(state.Phase==VideoPhase.Playing);
            _sinkPause.interactable=state.CanPauseResume;
            _sinkReplay.interactable=state.CanReplay || state.Phase==VideoPhase.Failed;
            _sinkPause.GetComponentInChildren<TMP_Text>().text=state.Phase==VideoPhase.Paused?"继续播放":"暂停";
            _sinkReplay.GetComponentInChildren<TMP_Text>().text=state.Phase==VideoPhase.Failed?"重新加载":"重新播放";
            _sinkVideoStatus.text=state.Phase==VideoPhase.Loading?"正在加载视频…":
                state.Phase==VideoPhase.Failed?"视频加载失败，请重新加载或返回检查。":
                state.Phase==VideoPhase.Completed?"示范已看完，可以重看或返回检查。":
                state.Phase==VideoPhase.Paused?"已暂停":"原声示范 · 20 秒";
            if(state.Phase==VideoPhase.Completed && _owner.Session.CanEdit)
            {
                _owner.ScriptActions.Add("RE-03:video-watched");
                _owner.Session.TryRecordLearningAction("RE-03",BotanicalGardenQR.Experience.Application.ClinicalLearningAction.Observed,"video-completed");
            }
        }

        void CloseSinkVideo()
        {
            SetSinkVideoAudioActive(false);
            _sinkVideoObservation?.Dispose();_sinkVideoObservation=null;
            _sinkVideo?.Close(_sinkVideoSession);_sinkVideo=null;
            _sinkVideoSession=default;_sinkVideoStatus=null;_sinkPause=null;_sinkReplay=null;
        }
    }
}
