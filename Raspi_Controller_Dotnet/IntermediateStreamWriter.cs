using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raspi_Controller_Dotnet
{
    public class IntermediateStreamWriter : Stream
    {
        public Stream Output;
        public long AmountWritten { get; private set; }
        public override bool CanRead => Output.CanRead;

        public override bool CanSeek => Output.CanSeek;

        public override bool CanWrite => Output.CanWrite;

        public override long Length => Output.Length;

        public override long Position { get => Output.Position; set => Output.Position = value; }

        public IntermediateStreamWriter(Stream output)
        {
            Output = output;
            AmountWritten = 0;
        }

        public override void Flush()
        {
            Output.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Output.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Output.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Output.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Output.Write(buffer, offset, count);
            AmountWritten += count;
        }
    }
}
