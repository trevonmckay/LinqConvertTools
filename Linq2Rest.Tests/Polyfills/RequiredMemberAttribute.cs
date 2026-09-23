// The C# compiler requires this type to emit required members. It ships in the BCL starting with .NET 7,
// so it is declared here for the net6.0 target.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }
}
