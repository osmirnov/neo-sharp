using System;
using System.Collections.Generic;
using System.Numerics;
using Neo.VM;
using NeoSharp.VM.NeoVM.Extensions;
using NeoSharp.VM.NeoVM.StackItems;

namespace NeoSharp.VM.NeoVM
{
    public class StackWrapper : Stack
    {
        private readonly RandomAccessStack<StackItem> _stack;

        public StackWrapper(RandomAccessStack<StackItem> stack)
        {
            _stack = stack;
        }

        public override int Count => _stack.Count;

        public override int Drop(int count = 0)
        {
            count = Math.Min(count, _stack.Count);

            for (var x = 0; x < count; x++)
            {
                _stack.Pop();
            }

            return count;
        }

        #region Create items

        protected override ArrayStackItemBase CreateArray(IEnumerable<StackItemBase> items = null)
        {
            return new ArrayStackItem(items);
        }

        protected override ArrayStackItemBase CreateStruct(IEnumerable<StackItemBase> items = null)
        {
            return new StructStackItem(items);
        }

        protected override BooleanStackItemBase CreateBool(bool value)
        {
            return new BooleanStackItem(value);
        }

        protected override ByteArrayStackItemBase CreateByteArray(byte[] data)
        {
            return new ByteArrayStackItem(data);
        }

        protected override IntegerStackItemBase CreateInteger(BigInteger value)
        {
            return new IntegerStackItem(value);
        }

        protected override IntegerStackItemBase CreateInteger(byte[] value)
        {
            return new IntegerStackItem(value);
        }

        protected override IntegerStackItemBase CreateInteger(int value)
        {
            return new IntegerStackItem(value);
        }

        protected override IntegerStackItemBase CreateInteger(long value)
        {
            return new IntegerStackItem(value);
        }

        protected override InteropStackItemBase<T> CreateInterop<T>(T obj)
        {
            return new InteropStackItem<T>(obj);
        }

        protected override MapStackItemBase CreateMap()
        {
            return new MapStackItem();
        }

        #endregion

        public override StackItemBase Pop()
        {
            return _stack.Pop().ConvertFromNative();
        }

        public override void Push(StackItemBase item)
        {
            if (!(item is INativeStackItemContainer nitem)) throw new ArgumentException(nameof(item));

            _stack.Push(nitem.NativeStackItem);
        }

        public override bool TryPeek(int index, out StackItemBase obj)
        {
            if (_stack.Count <= index)
            {
                obj = null;
                return false;
            }

            obj = _stack.Peek(index)?.ConvertFromNative();

            return obj != null;
        }

        public override bool TryPop<TStackItem>(out TStackItem item)
        {
            if (_stack.Count < 1)
            {
                item = null;
                return false;
            }

            var ret = Pop();

            item = ret is TStackItem stackItem ? stackItem : null;

            return item != null;
        }
    }
}