// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NorthwindCustomer.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the NorthwindCustomer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Fakes
{
    public class NorthwindCustomer
    {
        public string CustomerID { get; set; } = null!;

        public string CompanyName { get; set; } = null!;

        public string ContactName { get; set; } = null!;

        public string ContactTitle { get; set; } = null!;

        public string Address { get; set; } = null!;

        public string City { get; set; } = null!;

        public string Region { get; set; } = null!;

        public string PostalCode { get; set; } = null!;

        public string Country { get; set; } = null!;

        public string Phone { get; set; } = null!;
    }
}