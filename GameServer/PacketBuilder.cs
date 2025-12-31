using System;
using System.Text;

namespace GameServer
{
    /// <summary>
    /// 简单的数据包构建器
    /// </summary>
    public class PacketBuilder
    {
        private readonly System.IO.MemoryStream _stream;
        private readonly System.IO.BinaryWriter _writer;

        public PacketBuilder()
        {
            _stream = new System.IO.MemoryStream();
            _writer = new System.IO.BinaryWriter(_stream);
        }

        public void WriteByte(byte value) => _writer.Write(value);
        public void WriteSByte(sbyte value) => _writer.Write(value);
        public void WriteUInt16(ushort value) => _writer.Write(value);
        public void WriteInt16(short value) => _writer.Write(value);
        public void WriteUInt32(uint value) => _writer.Write(value);
        public void WriteInt32(int value) => _writer.Write(value);
        public void WriteUInt64(ulong value) => _writer.Write(value);
        public void WriteInt64(long value) => _writer.Write(value);
        public void WriteFloat(float value) => _writer.Write(value);
        public void WriteDouble(double value) => _writer.Write(value);
        public void WriteBoolean(bool value) => _writer.Write(value);
        
        public void WriteString(string value)
        {
            var bytes = Encoding.GetEncoding("GBK").GetBytes(value);
            _writer.Write(bytes);
            WriteByte(0); // null terminator
        }
        
        public void WriteBytes(byte[] bytes) => _writer.Write(bytes);
        
        public byte[] Build()
        {
            _writer.Flush();
            return _stream.ToArray();
        }
    }
}
