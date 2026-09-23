// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StreamValueWriter.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the StreamValueWriter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Provider.Writers
{
    using System;
    using System.Globalization;
    using System.IO;

    internal class StreamValueWriter : ValueWriterBase<Stream>
    {
        public override string Write(object value)
        {
            var stream = (Stream)value;
            var buffer = stream.CanSeek ? ReadSeekable(stream) : ReadToEnd(stream);
            var base64 = Convert.ToBase64String(buffer);

            return string.Format(CultureInfo.InvariantCulture, "X'{0}'", base64);
        }

        private static byte[] ReadSeekable(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);

            var buffer = new byte[stream.Length];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                // Stream.Read may return fewer bytes than requested even before the end of the stream.
                var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead < buffer.Length)
            {
                Array.Resize(ref buffer, totalRead);
            }

            return buffer;
        }

        private static byte[] ReadToEnd(Stream stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}