#if NETSTANDARD2_0
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler shim enabling C# <c>init</c>-only setters on netstandard2.0, where the framework
    /// does not define this type.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
