namespace JK.NuGetTools.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NuGet.Packaging.Core;

    public class PackageHierarchy
    {
        internal PackageHierarchy(PackageIdentity identity)
            : this(identity, null)
        {
        }

        internal PackageHierarchy(PackageIdentity identity, IEnumerable<PackageHierarchy> children)
        {
            this.Identity = identity;
            this.Children = children ?? Enumerable.Empty<PackageHierarchy>();
        }

        public PackageIdentity Identity { get; }

        public IEnumerable<PackageHierarchy> Children { get; }
    }
}
