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

        internal PackageHierarchy(PackageIdentity identity, ICollection<PackageHierarchy> children)
        {
            this.Identity = identity;
            this.Children = children ?? new List<PackageHierarchy>();
        }

        public PackageIdentity Identity { get; set; }

        public ICollection<PackageHierarchy> Children { get; set; }
    }
}
