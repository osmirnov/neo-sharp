using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NeoSharp.VM
{
    public abstract class ExecutionEngineBase : IDisposable
    {
        #region Public fields from arguments

        /// <summary>
        /// Gas ratio for computation
        /// </summary>
        public const ulong GasRatio = 100000;

        /// <summary>
        /// Trigger
        /// </summary>
        public ETriggerType Trigger { get; }

        /// <summary>
        /// Interop service
        /// </summary>
        public InteropService InteropService { get; }

        /// <summary>
        /// Script table
        /// </summary>
        public IScriptTable ScriptTable { get; }

        /// <summary>
        /// Logger
        /// </summary>
        public ExecutionEngineLogger Logger { get; }

        /// <summary>
        /// Message Provider
        /// </summary>
        public IMessageProvider MessageProvider { get; }

        #endregion

        /// <summary>
        /// Virtual Machine State
        /// </summary>
        public abstract EVMState State { get; }

        /// <summary>
        /// InvocationStack
        /// </summary>
        public abstract StackBase<ExecutionContext> InvocationStack { get; }

        /// <summary>
        /// ResultStack
        /// </summary>
        public abstract Stack ResultStack { get; }

        /// <summary>
        /// Is disposed
        /// </summary>
        public abstract bool IsDisposed { get; }

        /// <summary>
        /// Consumed Gas
        /// </summary>
        public abstract uint ConsumedGas { get; }

        #region Shortcuts

        public ExecutionContext CurrentContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return InvocationStack.TryPeek(0, out ExecutionContext i) ? i : null; }
        }

        public ExecutionContext CallingContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return InvocationStack.TryPeek(1, out ExecutionContext i) ? i : null; }
        }

        public ExecutionContext EntryContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return InvocationStack.TryPeek(-1, out ExecutionContext i) ? i : null; }
        }

        #endregion

        /// <summary>
        /// For unit testing only
        /// </summary>
        protected ExecutionEngineBase() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="e">Arguments</param>
        protected ExecutionEngineBase(ExecutionEngineArgs e)
        {
            if (e == null) return;

            InteropService = e.InteropService;
            ScriptTable = e.ScriptTable;
            MessageProvider = e.MessageProvider;
            Trigger = e.Trigger;
            Logger = e.Logger;
        }

        #region Load Script

        /// <summary>
        /// Load script
        /// </summary>
        /// <param name="script">Script</param>
        /// <returns>Script index in script cache</returns>
        public abstract int LoadScript(byte[] script);

        /// <summary>
        /// Load script
        /// </summary>
        /// <param name="scriptIndex">Script Index</param>
        /// <returns>True if is loaded</returns>
        public abstract bool LoadScript(int scriptIndex);

        #endregion

        #region Execution

        /// <summary>
        /// Increase gas
        /// </summary>
        /// <param name="gas">Gas</param>
        public abstract bool IncreaseGas(long gas);

        /// <summary>
        /// Clean Execution engine state
        /// </summary>
        /// <param name="iteration">Iteration</param>
        public abstract void Clean(uint iteration = 0);

        /// <summary>
        /// Execute (until x of Gas)
        /// </summary>
        /// <param name="gas">Gas</param>
        public abstract bool Execute(uint gas = uint.MaxValue);

        /// <summary>
        /// Step Into
        /// </summary>
        /// <param name="steps">Steps</param>
        public abstract void StepInto(int steps = 1);

        /// <summary>
        /// Step Out
        /// </summary>
        public abstract void StepOut();

        /// <summary>
        /// Step Over
        /// </summary>
        public abstract void StepOver();

        #endregion

        #region Creates

        /// <summary>
        /// Create Map StackItem
        /// </summary>
        public abstract MapStackItemBase CreateMap();

        /// <summary>
        /// Create Array StackItem
        /// </summary>
        /// <param name="items">Items</param>
        public abstract ArrayStackItemBase CreateArray(IEnumerable<StackItemBase> items = null);

        /// <summary>
        /// Create Struct StackItem
        /// </summary>
        /// <param name="items">Items</param>
        public abstract ArrayStackItemBase CreateStruct(IEnumerable<StackItemBase> items = null);

        /// <summary>
        /// Create ByteArrayStackItem
        /// </summary>
        /// <param name="data">Buffer</param>
        public abstract ByteArrayStackItemBase CreateByteArray(byte[] data);

        /// <summary>
        /// Create InteropStackItem
        /// </summary>
        /// <param name="obj">Object</param>
        public abstract InteropStackItemBase<T> CreateInterop<T>(T obj) where T : class;

        /// <summary>
        /// Create BooleanStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract BooleanStackItemBase CreateBool(bool value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IntegerStackItemBase CreateInteger(int value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IntegerStackItemBase CreateInteger(long value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IntegerStackItemBase CreateInteger(BigInteger value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IntegerStackItemBase CreateInteger(byte[] value);

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Dispose logic
        /// </summary>
        /// <param name="disposing">False for cleanup native objects</param>
        protected virtual void Dispose(bool disposing) { }

        /// <summary>
        /// Destructor
        /// </summary>
        ~ExecutionEngineBase()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}