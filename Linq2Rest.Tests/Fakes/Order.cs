namespace LinqConvertTools.Tests.Fakes
{
    internal interface IOrderSchema
    {
        string? Name { get; }

        IOrderAddressSchema? Address { get; }

        IEnumerable<IOrderLineSchema> Lines { get; }

        IEnumerable<string> Tags { get; }

        int? Priority { get; }
    }

    internal interface IOrderAddressSchema
    {
        string? City { get; }
    }

    internal interface IOrderLineSchema
    {
        string? Sku { get; }

        int? Quantity { get; }
    }

    internal class Order
    {
        public string Name { get; set; } = string.Empty;

        public OrderAddress? Address { get; set; }

        /// <summary>
        /// Shares its name with <see cref="OrderAddress.City"/>, so a path flattened to its leaf member binds here instead.
        /// </summary>
        public string? City { get; set; }

        public List<OrderLine> Lines { get; set; } = new();

        public List<string> Tags { get; set; } = new();

        public int Priority { get; set; }
    }

    internal class OrderAddress
    {
        public string? City { get; set; }
    }

    internal class OrderLine
    {
        public string Sku { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
