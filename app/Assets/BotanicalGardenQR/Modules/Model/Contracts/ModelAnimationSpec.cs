using System;
using UnityEngine;
namespace BotanicalGardenQR.Model.Contracts
{
    public enum ModelAnimationDriver { AnimatorController, AnimationClip }
    public enum ModelAnimationStartPolicy { OnVisitorCommand, OnOpen }
    public sealed class ModelAnimationSpec
    {
        ModelAnimationSpec(ModelAnimationDriver driver,RuntimeAnimatorController controller,AnimationClip clip,string initialState,ModelAnimationStartPolicy startPolicy)
        { Driver=driver; Controller=controller; Clip=clip; InitialState=initialState; StartPolicy=startPolicy; }
        public ModelAnimationDriver Driver { get; }
        public RuntimeAnimatorController Controller { get; }
        public AnimationClip Clip { get; }
        public string InitialState { get; }
        public ModelAnimationStartPolicy StartPolicy { get; }
        public static ModelAnimationSpec FromController(RuntimeAnimatorController controller,string initialState,ModelAnimationStartPolicy startPolicy)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (string.IsNullOrWhiteSpace(initialState)) throw new ArgumentException("An initial animation state is required.",nameof(initialState));
            return new ModelAnimationSpec(ModelAnimationDriver.AnimatorController,controller,null,initialState.Trim(),startPolicy);
        }
        public static ModelAnimationSpec FromClip(AnimationClip clip,ModelAnimationStartPolicy startPolicy)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            return new ModelAnimationSpec(ModelAnimationDriver.AnimationClip,null,clip,null,startPolicy);
        }
    }
}
