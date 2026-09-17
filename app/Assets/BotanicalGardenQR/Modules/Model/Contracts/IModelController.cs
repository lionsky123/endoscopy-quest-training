using System;
using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Model.Contracts
{
    public interface IModelController
    {
        ModelResult Open(SessionToken session, ModelDefinition definition, ModelSurfaceLease surface);
        ModelResult Dispatch(SessionToken session, ModelIntent intent);
        ModelResult Close(SessionToken session);
        IDisposable Observe(IModelStateSink sink);
    }
    public interface IModelStateSink { void Publish(ModelState state); }
}
