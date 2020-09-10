#if !NO_RUNTIME
using System;
using CustomDataStruct;

namespace ProtoBuf.Serializers
{
    sealed class Int64Serializer : IProtoSerializer
    {
        static readonly Type expectedType = typeof(long);

        public Int64Serializer(ProtoBuf.Meta.TypeModel model) { }

        public Type ExpectedType => expectedType;

        bool IProtoSerializer.RequiresOldValue => false;

        bool IProtoSerializer.ReturnsValue => true;

        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return ValueObject.Get(source.ReadInt64());
        }

        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteInt64(ValueObject.Value<long>(value), dest);
        }

#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitBasicWrite("WriteInt64", valueFrom);
        }
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitBasicRead("ReadInt64", ExpectedType);
        }
#endif
    }
}
#endif
