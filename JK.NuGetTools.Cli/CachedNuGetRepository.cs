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

namespace JK.NuGetTools.Cli
{
    public class CachedNuGetRepository : NuGetRepository
    {
        private Dictionary<string, object> cache = new Dictionary<string, object>();

        public CachedNuGetRepository(string feedUrl) : base(feedUrl) { }

        public CachedNuGetRepository(string feedUrl, SourceCacheContext sourceCacheContext, ILogger log) : base(feedUrl, sourceCacheContext, log) { }

        public override async Task<PackageIdentity> GetLatestPackageAsync(string packageId
                                                                        , VersionRange versionRange
                                                                        , CancellationToken cancellationToken)
        {
            var key = BuildKey(nameof(GetLatestPackageAsync), packageId, versionRange);

            if (!cache.ContainsKey(key))
            {
                cache[key] = await base.GetLatestPackageAsync(packageId, versionRange, cancellationToken);
            }

            return (PackageIdentity)cache[key];
        }

        public override async Task<IEnumerable<PackageIdentity>> GetPackageDependenciesAsync(PackageIdentity package, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            var key = BuildKey(nameof(GetLatestPackageAsync), package, targetFramework.GetShortFolderName());

            if (!cache.ContainsKey(key))
            {
                cache[key] = await base.GetPackageDependenciesAsync(package, targetFramework, cancellationToken);
            }

            return (IEnumerable<PackageIdentity>)cache[key];
        }

        private string BuildKey(string context, params object[] list)
        {
            var key = context;
            foreach(var item in list)
            {
                key += "_" + (item?.ToString() ?? "null");
            }

            return key;
        }
    }
}
