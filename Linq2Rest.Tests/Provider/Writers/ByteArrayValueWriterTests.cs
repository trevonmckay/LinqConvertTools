// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ByteArrayValueWriterTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ByteArrayValueWriterTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Provider.Writers
{
    using LinqConvertTools.Provider.Writers;
    using NUnit.Framework;

    [TestFixture]
    public class ByteArrayValueWriterTests
    {
        private ByteArrayValueWriter? _writer;

        [SetUp]
        public void Setup()
        {
            _writer = new ByteArrayValueWriter();
        }

        [Test]
        public void WhenWritingByteArrayThenEnclosesInSingleQuote()
        {
            ArgumentNullException.ThrowIfNull(_writer);

            var byteArray = new byte[] { 1, 2, 3, 4 };
            var result = _writer.Write(byteArray);

            Assert.AreEqual("X'AQIDBA=='", result);
        }
    }
}