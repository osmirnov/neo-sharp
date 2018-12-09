using System;
using NeoSharp.VM;

namespace NeoSharp.Core.VM
{
    public static class InteropServiceExtensions
    {
        public static void RegisterStackTransition(
            this InteropService interopService,
            string name,
            Func<IStackAccessor, bool> handler)
        {
            bool InvokeHandler(ExecutionEngineBase engine)
            {
                return engine.CurrentContext != null && handler(new StackAccessor(engine));
            }

            interopService.Register(name, InvokeHandler);
        }
    }
}