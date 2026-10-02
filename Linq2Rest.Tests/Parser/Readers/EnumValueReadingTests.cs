// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnumValueReadingTests.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the EnumValueReadingTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Parser.Readers
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using LinqConvertTools.Parser.Readers;
    using NUnit.Framework;

    /// <summary>
    /// An enum literal is read against the type of the member it is compared with: an unqualified member name, or a
    /// name qualified with the enum type, resolved by name and case-insensitively. A qualifier that names a different
    /// type is rejected, and no enum type is resolved from the literal's own text (so no assembly is scanned).
    /// </summary>
    [TestFixture]
    public class EnumValueReadingTests
    {
        private static object? Read(string token, bool enumNamesOnly = false)
        {
            var reader = new ParameterValueReader(Enumerable.Empty<IValueExpressionFactory>(), enumNamesOnly);
            return (reader.Read(typeof(Choice), token, CultureInfo.InvariantCulture) as ConstantExpression)?.Value;
        }

        [TestCase("'That'")]
        [TestCase("\"That\"")]
        [TestCase("That")]
        [TestCase("'that'")]
        [TestCase("LinqConvertTools.Tests.Choice'That'")]
        [TestCase("Choice'That'")]
        public void ReadsEnumMemberByName(string token)
        {
            Assert.AreEqual(Choice.That, Read(token));
        }

        [Test]
        public void ReadsFlagsCombinationByName()
        {
            Assert.AreEqual(Choice.Either, Read("'This, That'"));
        }

        [TestCase("Wrong.Type'That'")]
        [TestCase("System.DayOfWeek'That'")]
        public void RejectsQualifierThatIsNotTheMemberType(string token)
        {
            Assert.Throws<FormatException>(() => Read(token));
        }

        [TestCase("blah")]
        [TestCase("'Nope'")]
        public void RejectsUndefinedName(string token)
        {
            Assert.Throws<FormatException>(() => Read(token));
        }

        [Test]
        public void ReadsNumericValueWhenNamesOnlyIsOff()
        {
            Assert.AreEqual(Choice.This, Read("'1'"));
        }

        [TestCase("'1'")]
        [TestCase("'Nope'")]
        public void RejectsNumericOrUndefinedWhenNamesOnlyIsOn(string token)
        {
            Assert.Throws<FormatException>(() => Read(token, enumNamesOnly: true));
        }

        [Test]
        public void ReadsNameWhenNamesOnlyIsOn()
        {
            Assert.AreEqual(Choice.That, Read("'That'", enumNamesOnly: true));
        }

        [Test]
        public void ReadsFlagsCombinationWhenNamesOnlyIsOn()
        {
            Assert.AreEqual(Choice.Either, Read("'This, That'", enumNamesOnly: true));
        }

        [Test]
        public void NotAMemberMessageListsMembersByShortTypeName()
        {
            var ex = Assert.Throws<FormatException>(() => Read("'Nope'"));

            Assert.That(ex!.Message, Does.Contain("Nope"));
            Assert.That(ex.Message, Does.Contain("Choice"));
            Assert.That(ex.Message, Does.Contain("This").And.Contain("That").And.Contain("Either"));
            Assert.That(ex.Message, Does.Not.Contain("LinqConvertTools"));
        }

        [Test]
        public void RejectsCommaCombinationOnNonFlagsEnumWhenNamesOnlyIsOn()
        {
            // DayOfWeek is not a [Flags] enum, so combining its members is meaningless; without this guard
            // Enum.Parse would still OR them into a value equal to some unrelated member.
            var reader = new ParameterValueReader(Enumerable.Empty<IValueExpressionFactory>(), enumNamesOnly: true);
            Assert.Throws<FormatException>(() => reader.Read(typeof(DayOfWeek), "'Monday,Tuesday'", CultureInfo.InvariantCulture));
        }
    }
}
