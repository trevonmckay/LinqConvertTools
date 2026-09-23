// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ByteValueWriterTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ByteValueWriterTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Provider.Writers
{
    using LinqConvertTools.Provider.Writers;
    using NUnit.Framework;

    [TestFixture]
    public class ByteValueWriterTests
    {
        private ByteValueWriter? _writer;

        [SetUp]
        public void Setup()
        {
            _writer = new ByteValueWriter();
        }

        [Test]
        public void WhenWritingByteValueThenWritesString()
        {
            ArgumentNullException.ThrowIfNull(_writer);

            const byte Value = 255;
            var result = _writer.Write(Value);

            Assert.AreEqual("FF", result);
        }
    }
}