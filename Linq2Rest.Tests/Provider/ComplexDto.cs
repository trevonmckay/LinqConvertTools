// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ComplexDto.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ComplexDto type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Provider
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal class ComplexDto
    {
        public int ID { get; set; }

        public string Content { get; set; } = null!;

        public double Value { get; set; }

        public DateTime Date { get; set; }

        public Choice Choice { get; set; }

        public ChildDto Child { get; set; } = null!;

        public ValueObject ValueObject { get; set; }
    }

    internal readonly struct ValueObject : IEquatable<string>, IEquatable<ValueObject>, IEquatable<ValueObject?>
    {
        private readonly string _value;

        public static readonly ValueObject FirstValueObject = new("first");

        public static readonly ValueObject SecondValueObject = new("second");

        private ValueObject(string value)
        {
            _value = value;
        }

        public bool Equals([NotNullWhen(true)] string? other)
        {
            return _value is not null && _value.Equals(other, StringComparison.Ordinal);
        }

        public bool Equals(ValueObject other)
        {
            return Equals(other._value);
        }

        public bool Equals([NotNullWhen(true)] ValueObject? other)
        {
            return other.HasValue && Equals(other.Value._value);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is string otherValue)
            {
                return Equals(otherValue);
            }
            else if (obj is ValueObject other)
            {
                return Equals(other);
            }
            
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hashCode = new();
            hashCode.Add(_value, StringComparer.Ordinal);

            return hashCode.ToHashCode();
        }

        public override readonly string ToString()
        {
            return _value;
        }

        [return: NotNullIfNotNull(nameof(valueObject))]
        public static implicit operator string?(ValueObject? valueObject)
        {
            if (!valueObject.HasValue)
            {
                return null;
            }

            return valueObject.Value.ToString();
        }

        public static bool operator ==(ValueObject? left, ValueObject? right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        public static bool operator !=(ValueObject? left, ValueObject? right)
        {
            return !(left == right);
        }
    }
}