#if !NO_RUNTIME
using System;
using CustomDataStruct;

namespace ProtoBuf.Serializers
{
    sealed class DoubleSerializer : IProtoSerializer
    {
        static readonly Type expectedType = typeof(double);

        public DoubleSerializer(ProtoBuf.Meta.TypeModel model) { }

        public Type ExpectedType => expectedType;

        bool IProtoSerializer.RequiresOldValue => false;

        bool IProtoSerializer.ReturnsValue => true;

        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return ValueObject.Get(source.ReadDouble());
        }

        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteDouble(ValueObject.Value<double>(value), dest);
        }

#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitBasicWrite("WriteDouble", valueFrom);
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitBasicRead("ReadDouble", ExpectedType);
        }
#endif
    }
}
#endif
