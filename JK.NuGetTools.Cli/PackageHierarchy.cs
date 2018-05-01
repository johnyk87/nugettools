using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;

namespace JK.NuGetTools.Cli
{
    public class PackageHierarchy
    {
        public PackageHierarchy(PackageIdentity identity) : this(identity, null) { }

        public PackageHierarchy(PackageIdentity identity, ICollection<PackageHierarchy> children)
        {
            this.Identity = identity;
            this.Children = children ?? new List<PackageHierarchy>();
        }

        public PackageIdentity Identity { get; set; }

        public ICollection<PackageHierarchy> Children { get; set; }
    }
}
