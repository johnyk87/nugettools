namespace JK.NuGetTools.Cli
{
    using System.Collections.Generic;

    public interface IHierarchy
    {
        object Value { get; }

        IEnumerable<IHierarchy> Children { get; }
    }

    public interface IHierarchy<T> : IHierarchy
    {
        new T Value { get; }

        new IEnumerable<IHierarchy<T>> Children { get; }
    }
}
