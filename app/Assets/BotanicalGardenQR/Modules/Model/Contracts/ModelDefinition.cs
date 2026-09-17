namespace BotanicalGardenQR.Model.Contracts
{
    public sealed class ModelDefinition
    {
        public ModelDefinition(
            ModelSource source,
            ModelPresentationSpec presentation = null,
            ModelAnimationSpec animation = null)
        {
            Source = source ?? throw new System.ArgumentNullException(nameof(source));
            Presentation = presentation ?? ModelPresentationSpec.Default;
            Animation = animation;
        }

        public ModelSource Source { get; }
        public ModelPresentationSpec Presentation { get; }
        public ModelAnimationSpec Animation { get; }
    }
}
