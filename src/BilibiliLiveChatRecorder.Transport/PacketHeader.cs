using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Darkflame.BilibiliLiveChatRecorder.Transport
{
    [StructLayout(LayoutKind.Explicit)]
    public struct PacketHeader
    {
        public const int HeaderSize = 16;

        public PacketHeader(int bodySize, Operation operation, short headerLength = 16)
        {
            HeaderLength = headerLength;
            Version = 1;
            Sequence = 1;
            Length = bodySize + headerLength;
            // Body = new byte[length];
            Operation = operation;
        }

        [FieldOffset(0)]
        public int Length;
        [FieldOffset(4)]
        public short HeaderLength;
        [FieldOffset(6)]
        public short Version;
        [FieldOffset(8)]
        public Operation Operation;
        [FieldOffset(12)]
        public int Sequence;
        public int BodyLength => Length - HeaderLength;

        public static PacketHeader Parse(Span<byte> span)
        {
            span.Slice(0, 4).Reverse();
            span.Slice(4, 2).Reverse();
            span.Slice(6, 2).Reverse();
            span.Slice(8, 4).Reverse();
            span.Slice(12, 4).Reverse();
            return MemoryMarshal.Read<PacketHeader>(span);
        }

        public static int GetBytes(string body, Operation operation, Span<byte> dest, short headerLength = 16, int sequence = 1)
        {
            int size;
            size = Encoding.UTF8.GetBytes(body.AsSpan(), dest.Slice(headerLength));
            var packet = new PacketHeader(size, operation, headerLength)
            {
                Sequence = sequence
            };
            MemoryMarshal.Write(dest, ref packet);
            dest.Slice(0, 4).Reverse();
            dest.Slice(4, 2).Reverse();
            dest.Slice(6, 2).Reverse();
            dest.Slice(8, 4).Reverse();
            dest.Slice(12, 4).Reverse();
            return headerLength + size;
        }

        public override string ToString()
        {
            return $"{nameof(Length)}:{Length}\n{nameof(HeaderLength)}:{HeaderLength}\n{nameof(Version)}:{Version}\n{nameof(Operation)}:{Operation}\n{nameof(Sequence)}:{Sequence}";
        }
    }
}
