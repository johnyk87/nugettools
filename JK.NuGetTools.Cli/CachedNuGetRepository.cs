namespace JK.NuGetTools.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NuGet.Common;
    using NuGet.Frameworks;
    using NuGet.Packaging.Core;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    internal class CachedNuGetRepository : NuGetRepository
    {
        private readonly Dictionary<string, object> cache = new Dictionary<string, object>();

        public CachedNuGetRepository(string feedUrl)
            : base(feedUrl)
        {
        }

        public CachedNuGetRepository(string feedUrl, SourceCacheContext sourceCacheContext, ILogger log)
            : base(feedUrl, sourceCacheContext, log)
        {
        }

        public override async Task<PackageIdentity> GetLatestPackageAsync(
            string packageId,
            VersionRange versionRange,
            CancellationToken cancellationToken)
        {
            var key = BuildKey(nameof(this.GetLatestPackageAsync), packageId, versionRange);

            if (!this.cache.ContainsKey(key))
            {
                this.cache[key] = await base.GetLatestPackageAsync(packageId, versionRange, cancellationToken).ConfigureAwait(false);
            }

            return (PackageIdentity)this.cache[key];
        }

        public override async Task<IEnumerable<PackageIdentity>> GetPackageDependenciesAsync(
            PackageIdentity package,
            NuGetFramework targetFramework,
            CancellationToken cancellationToken)
        {
            var key = BuildKey(nameof(this.GetPackageDependenciesAsync), package, targetFramework.GetShortFolderName());

            if (!this.cache.ContainsKey(key))
            {
                this.cache[key] = await base.GetPackageDependenciesAsync(package, targetFramework, cancellationToken).ConfigureAwait(false);
            }

            return (IEnumerable<PackageIdentity>)this.cache[key];
        }

        private static string BuildKey(string context, params object[] list)
        {
            var key = context;
            foreach (var item in list)
            {
                key += "_" + (item?.ToString() ?? "null");
            }

            return key;
        }
    }
}
