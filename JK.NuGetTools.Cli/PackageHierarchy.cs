namespace JK.NuGetTools.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NuGet.Packaging.Core;

    public class PackageHierarchy : IHierarchy<PackageIdentity>
    {
        internal PackageHierarchy(PackageIdentity identity)
            : this(identity, null)
        {
        }

        internal PackageHierarchy(PackageIdentity identity, IEnumerable<PackageHierarchy> children)
        {
            this.Value = identity;
            this.Children = children ?? Enumerable.Empty<PackageHierarchy>();
        }

        public PackageIdentity Value { get; }

        object IHierarchy.Value => this.Value;

        public IEnumerable<PackageHierarchy> Children { get; }

        IEnumerable<IHierarchy> IHierarchy.Children => this.Children.Cast<IHierarchy>();

        IEnumerable<IHierarchy<PackageIdentity>> IHierarchy<PackageIdentity>.Children => this.Children.Cast<IHierarchy<PackageIdentity>>();

        public override string ToString()
        {
            return this.Value.ToString();
        }
    }
}
