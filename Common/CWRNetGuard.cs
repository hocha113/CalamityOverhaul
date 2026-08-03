using System;
using System.IO;
using System.Text;

namespace CalamityOverhaul.Common
{
    /// <summary>网络反序列化的长度边界</summary>
    internal static class CWRNetGuard
    {
        public static void WriteString(BinaryWriter writer, string value, int maxByteLength) {
            value ??= string.Empty;
            if (Encoding.UTF8.GetByteCount(value) <= maxByteLength) {
                writer.Write(value);
                return;
            }
            byte[] buffer = new byte[maxByteLength];
            Encoding.UTF8.GetEncoder().Convert(value.AsSpan(), buffer.AsSpan(), true,
                out int charsUsed, out _, out _);
            writer.Write(value[..charsUsed]);
        }

        public static int ReadCount(BinaryReader reader, int maxCount, string fieldName) {
            int count = reader.ReadInt32();
            if (count < 0 || count > maxCount) {
                throw new IOException($"{fieldName} count {count} exceeds 0..{maxCount}");
            }
            return count;
        }

        public static string ReadString(BinaryReader reader, int maxByteLength, string fieldName) {
            int byteLength;
            try {
                byteLength = reader.Read7BitEncodedInt();
            }
            catch (System.FormatException ex) {
                throw new IOException($"{fieldName} has an invalid length prefix", ex);
            }
            if (byteLength < 0 || byteLength > maxByteLength) {
                throw new IOException($"{fieldName} length {byteLength} exceeds 0..{maxByteLength}");
            }
            byte[] bytes = reader.ReadBytes(byteLength);
            if (bytes.Length != byteLength) {
                throw new EndOfStreamException($"{fieldName} ended after {bytes.Length} of {byteLength} bytes");
            }
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
