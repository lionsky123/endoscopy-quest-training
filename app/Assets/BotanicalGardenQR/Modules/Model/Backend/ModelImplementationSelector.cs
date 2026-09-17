using System.Runtime.CompilerServices;
using BotanicalGardenQR.Model.Contracts;

[assembly: InternalsVisibleTo("BotanicalGardenQR.Tests.EditMode")]

namespace BotanicalGardenQR.Model.Backend
{
    internal class ModelImplementationSelector
    {
        readonly PrefabModelLoader _prefabLoader = new PrefabModelLoader();
        readonly GlbModelLoader _glbLoader = new GlbModelLoader();

        public virtual IModelLoader SelectLoader(ModelSource source)
        {
            switch (source.Kind)
            {
                case ModelSourceKind.Prefab:
                    return _prefabLoader;
                case ModelSourceKind.Glb:
                    return _glbLoader;
                default:
                    return null;
            }
        }

        public virtual IModelDriver SelectDriver(ModelAnimationSpec animation)
            => animation == null ? (IModelDriver)new StaticModelDriver() : new AnimatorModelDriver(animation);
    }
}
