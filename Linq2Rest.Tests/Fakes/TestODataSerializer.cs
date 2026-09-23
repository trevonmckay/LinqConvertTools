// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestODataSerializer.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the TestODataSerializer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Fakes
{
    using LinqConvertTools.Provider;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    public class TestODataSerializer<T> : ISerializer<T>
    {
        private readonly DataContractJsonSerializer _innerSerializer = new DataContractJsonSerializer(typeof(ODataResponse<T>));

        public T Deserialize(Stream input)
        {
            return ReadResults(input).FirstOrDefault() ?? throw new SerializationException("The OData response contains no results.");
        }

        public IEnumerable<T> DeserializeList(Stream input)
        {
            return ReadResults(input);
        }

        public Stream Serialize(T item)
        {
            throw new NotImplementedException();
        }

        private List<T> ReadResults(Stream input)
        {
            var response = (ODataResponse<T>?)_innerSerializer.ReadObject(input);
            return response?.Results ?? throw new SerializationException("The payload does not contain an OData value array.");
        }
    }
}