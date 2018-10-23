namespace JK.NuGetTools.Cli
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using JK.NuGetTools.Cli.Exceptions;
    using NuGet.Common;
    using NuGet.Frameworks;
    using NuGet.Packaging.Core;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    public class NuGetRepository
    {
        private const NuGetVersionFloatBehavior VersionFloatBehavior = NuGetVersionFloatBehavior.Major;

        private static readonly ConcurrentDictionary<string, Task<IEnumerable<NuGetVersion>>> PackageVersionsCache =
            new ConcurrentDictionary<string, Task<IEnumerable<NuGetVersion>>>();

        private static readonly ConcurrentDictionary<string, Task<IPackageSearchMetadata>> PackageMetadataCache =
            new ConcurrentDictionary<string, Task<IPackageSearchMetadata>>();

        private readonly SourceRepository sourceRepository;
        private readonly Uri feedUrl;
        private readonly SourceCacheContext sourceCacheContext;
        private readonly ILogger logger;
        private readonly Lazy<Task<MetadataResource>> metadataResource;
        private readonly Lazy<Task<PackageMetadataResource>> packageMetadataResource;

        internal NuGetRepository(SourceRepository sourceRepository, SourceCacheContext sourceCacheContext, ILogger logger)
        {
            this.sourceRepository = sourceRepository;
            this.feedUrl = this.sourceRepository.PackageSource.SourceUri;
            this.sourceCacheContext = sourceCacheContext;
            this.logger = logger;
            this.metadataResource = new Lazy<Task<MetadataResource>>(this.CreateResourceAsync<MetadataResource>);
            this.packageMetadataResource = new Lazy<Task<PackageMetadataResource>>(this.CreateResourceAsync<PackageMetadataResource>);
        }

        public async Task<PackageIdentity> GetLatestPackageAsync(
            string packageId,
            VersionRange versionRange,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            versionRange = new VersionRange(versionRange ?? VersionRange.All, new FloatRange(VersionFloatBehavior));

            var packageVersions = await this.GetPackageVersionsAsync(packageId, cancellationToken).ConfigureAwait(false);
            if (!packageVersions.Any())
            {
                throw new PackageNotFoundException($"Package \"{packageId}\" not found in repository {this.feedUrl.ToString()}");
            }

            var packageVersion = versionRange.FindBestMatch(packageVersions) ??
                throw new PackageVersionNotFoundException($"Package \"{packageId}\" has no versions compatible with range \"{versionRange.ToString()}\".");

            return new PackageIdentity(packageId, packageVersion);
        }

        public async Task<IEnumerable<PackageIdentity>> GetPackageDependenciesAsync(
            PackageIdentity package,
            NuGetFramework targetFramework,
            IEnumerable<Regex> dependencyExclusionFilters,
            CancellationToken cancellationToken)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            if (dependencyExclusionFilters == null)
            {
                throw new ArgumentNullException(nameof(dependencyExclusionFilters));
            }

            var packageMetadata = await this.GetPackageMetadataAsync(package, cancellationToken).ConfigureAwait(false);

            var packageDependenciesTasks = this.ResolveDependencies(packageMetadata, targetFramework)
                .Where(d => !dependencyExclusionFilters.Any(f => f.IsMatch(d.Id)))
                .Select(p => this.GetLatestPackageAsync(p.Id, p.VersionRange, cancellationToken));

            return await Task.WhenAll(packageDependenciesTasks).ConfigureAwait(false);
        }

        public Task<PackageHierarchy> GetPackageHierarchyAsync(
            PackageIdentity package,
            NuGetFramework targetFramework,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters,
            CancellationToken cancellationToken)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            if (dependencyExclusionFilters == null)
            {
                throw new ArgumentNullException(nameof(dependencyExclusionFilters));
            }

            if (expansionExclusionFilters == null)
            {
                throw new ArgumentNullException(nameof(expansionExclusionFilters));
            }

            return this.GetPackageHierarchyAsync(
                package,
                targetFramework,
                dependencyExclusionFilters,
                expansionExclusionFilters,
                string.Empty,
                cancellationToken);
        }

        private static string ConcatenatePath(string existingPath, string currentLocation)
        {
            return string.IsNullOrEmpty(existingPath) ? currentLocation : string.Join(" => ", existingPath, currentLocation);
        }

        private static string BuildCacheKey(params object[] list)
        {
            var key = string.Empty;

            foreach (var item in list)
            {
                key = (string.IsNullOrEmpty(key) ? string.Empty : "_") + (item?.GetHashCode().ToString() ?? "null");
            }

            return key;
        }

        private async Task<PackageHierarchy> GetPackageHierarchyAsync(
            PackageIdentity package,
            NuGetFramework targetFramework,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters,
            string currentPath,
            CancellationToken cancellationToken)
        {
            if (expansionExclusionFilters.Any(f => f.IsMatch(package.Id)))
            {
                return new PackageHierarchy(package);
            }

            currentPath = ConcatenatePath(currentPath, package.ToString());

            var dependencies = default(IEnumerable<PackageIdentity>);
            try
            {
                dependencies = await this.GetPackageDependenciesAsync(
                    package,
                    targetFramework,
                    dependencyExclusionFilters,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Data["Path"] = currentPath;

                throw;
            }

            var dependencyHierarchiesTasks = dependencies
                .Select(
                    p => this.GetPackageHierarchyAsync(
                        p,
                        targetFramework,
                        dependencyExclusionFilters,
                        expansionExclusionFilters,
                        currentPath,
                        cancellationToken));

            var children = await Task.WhenAll(dependencyHierarchiesTasks).ConfigureAwait(false);

            return new PackageHierarchy(package, children);
        }

        private Task<T> CreateResourceAsync<T>()
            where T : class, INuGetResource
        {
            return this.sourceRepository.GetResourceAsync<T>();
        }

        private Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(string packageId, CancellationToken cancellationToken)
        {
            var cacheKey = BuildCacheKey(packageId);

            return PackageVersionsCache.GetOrAdd(cacheKey, async (_) =>
            {
                var metadataResource = await this.metadataResource.Value.ConfigureAwait(false);

                return await metadataResource
                    .GetVersions(packageId, true, true, this.sourceCacheContext, this.logger, cancellationToken)
                    .ConfigureAwait(false);
            });
        }

        private Task<IPackageSearchMetadata> GetPackageMetadataAsync(PackageIdentity package, CancellationToken cancellationToken)
        {
            var cacheKey = BuildCacheKey(package);

            return PackageMetadataCache.GetOrAdd(cacheKey, async (_) =>
            {
                var packageMetadataResource = await this.packageMetadataResource.Value.ConfigureAwait(false);

                return await packageMetadataResource
                    .GetMetadataAsync(package, this.sourceCacheContext, this.logger, cancellationToken)
                    .ConfigureAwait(false);
            });
        }

        private IEnumerable<PackageDependency> ResolveDependencies(
            IPackageSearchMetadata packageMetadata,
            NuGetFramework targetFramework)
        {
            if (packageMetadata.DependencySets.Any())
            {
                var packageDependencyGroup = NuGetFrameworkUtility.GetNearest(packageMetadata.DependencySets, targetFramework);
                if (packageDependencyGroup == null)
                {
                    throw new IncompatibleFrameworkException($"Package \"{packageMetadata.Identity.ToString()}\" is not compatible with {targetFramework.GetShortFolderName()}");
                }

                return packageDependencyGroup.Packages;
            }

            return Enumerable.Empty<PackageDependency>();
        }
    }
}
