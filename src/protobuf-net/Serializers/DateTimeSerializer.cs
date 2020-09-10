#if !NO_RUNTIME
using CustomDataStruct;
using System;
using System.Reflection;

namespace ProtoBuf.Serializers
{
    internal sealed class DateTimeSerializer : IProtoSerializer
    {
        private static readonly Type expectedType = typeof(DateTime);

        public Type ExpectedType => expectedType;

        bool IProtoSerializer.RequiresOldValue => false;
        bool IProtoSerializer.ReturnsValue => true;

        private readonly bool includeKind, wellKnown;

        public DateTimeSerializer(DataFormat dataFormat, ProtoBuf.Meta.TypeModel model)
        {
            wellKnown = dataFormat == DataFormat.WellKnown;
            includeKind = model?.SerializeDateTimeKind() == true;
        }

        public object Read(object value, ProtoReader source)
        {
            if (wellKnown)
            {
                return ValueObject.Get(BclHelpers.ReadTimestamp(source));
            }
            else
            {
                Helpers.DebugAssert(value == null); // since replaces
                return ValueObject.Get(BclHelpers.ReadDateTime(source));
            }
        }

        public void Write(object value, ProtoWriter dest)
        {
            if (wellKnown)
                BclHelpers.WriteTimestamp(ValueObject.Value<DateTime>(value), dest);
            else if (includeKind)
                BclHelpers.WriteDateTimeWithKind(ValueObject.Value<DateTime>(value), dest);
            else
                BclHelpers.WriteDateTime(ValueObject.Value<DateTime>(value), dest);
        }
#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitWrite(ctx.MapType(typeof(BclHelpers)),
                wellKnown ? nameof(BclHelpers.WriteTimestamp)
                : includeKind ? nameof(BclHelpers.WriteDateTimeWithKind) : nameof(BclHelpers.WriteDateTime), valueFrom);
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local entity)
        {
            if (wellKnown) ctx.LoadValue(entity);
            ctx.EmitBasicRead(ctx.MapType(typeof(BclHelpers)),
                wellKnown ? nameof(BclHelpers.ReadTimestamp) : nameof(BclHelpers.ReadDateTime),
                ExpectedType);
        }
#endif

    }
}
#endif