﻿using System.Linq;
using System.Text;

namespace System.IO
{
    public static class BinaryReaderExtensions
    {
        public static byte[] ReadBytesWithGrouping(this BinaryReader reader)
        {
            const int groupSize = 16;
            using (var ms = new MemoryStream())
            {
                int padding;
                do
                {
                    var group = reader.ReadBytes(groupSize);
                    padding = reader.ReadByte();
                    if (padding > groupSize)
                        throw new FormatException();
                    var count = groupSize - padding;
                    if (count > 0)
                        ms.Write(group, 0, count);
                } while (padding == 0);
                return ms.ToArray();
            }
        }

        public static string ReadFixedString(this BinaryReader reader, int length)
        {
            var data = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(data.TakeWhile(p => p != 0).ToArray());
        }

        public static byte[] ReadVarBytes(this BinaryReader reader, int max = 0X7fffffc7)
        {
            return reader.ReadBytes((int)reader.ReadVarInt((ulong)max));
        }

        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            var fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;

            if (value > max)
                throw new FormatException("MaxLength");

            return value;
        }

        public static string ReadVarString(this BinaryReader reader, int max = 0X7fffffc7)
        {
            return Encoding.UTF8.GetString(reader.ReadVarBytes(max));
        }
    }
}