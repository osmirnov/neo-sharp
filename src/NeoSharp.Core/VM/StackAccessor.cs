using System;
using System.Linq;
using System.Numerics;
using NeoSharp.Types;
using NeoSharp.VM;
using Stack = NeoSharp.VM.Stack;

namespace NeoSharp.Core.VM
{
    public class StackAccessor : IStackAccessor
    {
        private readonly ExecutionEngineBase _engine;
        private readonly ExecutionContextBase _context;
        private readonly Stack _stack;

        public UInt160 ScriptHash => new UInt160(_context.ScriptHash);

        public StackAccessor(ExecutionEngineBase engine)
        {
            _engine = engine;
            _context = engine.CurrentContext;
            _stack = engine.CurrentContext.EvaluationStack;
        }

        public void Push(bool value) => _stack.Push(value);

        public void Push(int value) => _stack.Push(value);

        public void Push(uint value) => _stack.Push(value);

        public void Push(long value) => _stack.Push(value);

        public void Push(ulong value) => _stack.Push(value);

        public void Push(byte[] value) => _stack.Push(value);

        public void Push<T>(T item) where T : class => _stack.PushObject(item);

        public void Push<T>(T[] items) where T : class
        {
            //var stackItems = items
            //    .Select(_engine.CreateInterop)
            //    .ToArray();

            //_stack.Push(_engine.CreateArray(stackItems));
        }

        public byte[] PeekByteArray(int index = 0) => _stack.PeekByteArray();

        public T PeekObject<T>(int index = 0) where T : class => _stack.PeekObject<T>();

        public BigInteger? PopBigInteger() => _stack.PopBigInteger();

        public byte[] PopByteArray() => _stack.PopByteArray();

        public T PopObject<T>() where T : class => _stack.PopObject<T>();

        public T[] PopArray<T>() where T : class => _stack.PopArray<T>();
    }
}