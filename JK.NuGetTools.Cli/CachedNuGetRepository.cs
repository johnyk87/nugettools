namespace JK.NuGetTools.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
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

        public CachedNuGetRepository(SourceRepository sourceRepository, SourceCacheContext sourceCacheContext, ILogger log)
            : base(sourceRepository, sourceCacheContext, log)
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
            IEnumerable<Regex> dependencyExclusionFilters,
            CancellationToken cancellationToken)
        {
            var key = BuildKey(nameof(this.GetPackageDependenciesAsync), package, targetFramework, dependencyExclusionFilters);

            if (!this.cache.ContainsKey(key))
            {
                this.cache[key] = await base
                    .GetPackageDependenciesAsync(package, targetFramework, dependencyExclusionFilters, cancellationToken)
                    .ConfigureAwait(false);
            }

            return (IEnumerable<PackageIdentity>)this.cache[key];
        }

        private static string BuildKey(string context, params object[] list)
        {
            var key = context;
            foreach (var item in list)
            {
                key += "_" + (item?.GetHashCode().ToString() ?? "null");
            }

            return key;
        }
    }
}
