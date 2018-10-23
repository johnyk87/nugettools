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

    internal class NuGetRepository
    {
        private readonly SourceRepository sourceRepository;
        private readonly SourceCacheContext sourceCacheContext;
        private readonly ILogger logger;
        private MetadataResource metadataResource;
        private PackageMetadataResource packageMetadataResource;

        public NuGetRepository(SourceRepository sourceRepository, SourceCacheContext sourceCacheContext, ILogger logger)
        {
            this.sourceRepository = sourceRepository;
            this.sourceCacheContext = sourceCacheContext;
            this.logger = logger;
        }

        public Uri FeedUrl => this.sourceRepository.PackageSource.SourceUri;

        protected MetadataResource MetadataResource
        {
            get
            {
                lock (this)
                {
                    if (this.metadataResource == null)
                    {
                        this.metadataResource = this.sourceRepository.GetResource<MetadataResource>();
                    }

                    return this.metadataResource;
                }
            }
        }

        protected PackageMetadataResource PackageMetadataResource
        {
            get
            {
                lock (this)
                {
                    if (this.packageMetadataResource == null)
                    {
                        this.packageMetadataResource = this.sourceRepository.GetResource<PackageMetadataResource>();
                    }

                    return this.packageMetadataResource;
                }
            }
        }

        public virtual async Task<PackageIdentity> GetLatestPackageAsync(
            string packageId,
            VersionRange versionRange,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            var packageVersions = await this.MetadataResource
                .GetVersions(packageId, true, true, this.sourceCacheContext, this.logger, cancellationToken)
                .ConfigureAwait(false);

            versionRange = new VersionRange(versionRange ?? VersionRange.All, new FloatRange(NuGetVersionFloatBehavior.Major));

            var packageVersion = versionRange.FindBestMatch(packageVersions);

            if (packageVersion == null)
            {
                throw new Exception($"Package \"{packageId}\" not found in repository {this.FeedUrl.ToString()}");
            }

            return new PackageIdentity(packageId, packageVersion);
        }

        public virtual async Task<IEnumerable<PackageIdentity>> GetPackageDependenciesAsync(
            PackageIdentity package,
            NuGetFramework targetFramework,
            IEnumerable<Regex> dependencyExclusionFilters,
            CancellationToken cancellationToken)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            dependencyExclusionFilters = dependencyExclusionFilters ?? Enumerable.Empty<Regex>();

            var packageMetadata = await this.PackageMetadataResource
                .GetMetadataAsync(package, this.sourceCacheContext, this.logger, cancellationToken)
                .ConfigureAwait(false);

            if (!this.TryResolveDependencies(packageMetadata, targetFramework, out var packageDependencies, out var packageFramework))
            {
                throw new Exception($"Package \"{packageMetadata.Identity.ToString()}\" is not compatible with {targetFramework.GetShortFolderName()}");
            }

            var packageDependenciesTasks = packageDependencies
                .Where(d => !dependencyExclusionFilters.Any(f => f.IsMatch(d.Id)))
                .Select(p => this.GetLatestPackageAsync(p.Id, p.VersionRange, cancellationToken));

            return await Task.WhenAll(packageDependenciesTasks).ConfigureAwait(false);
        }

        public virtual Task<PackageHierarchy> GetPackageHierarchyAsync(
            PackageIdentity package,
            NuGetFramework targetFramework,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters,
            CancellationToken cancellationToken)
        {
            return this.GetPackageHierarchyAsync(
                package,
                targetFramework,
                dependencyExclusionFilters,
                expansionExclusionFilters,
                string.Empty,
                cancellationToken);
        }

        protected virtual async Task<PackageHierarchy> GetPackageHierarchyAsync(
            PackageIdentity package,
            NuGetFramework targetFramework,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters,
            string currentPath,
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

            expansionExclusionFilters = expansionExclusionFilters ?? Enumerable.Empty<Regex>();

            currentPath = (string.IsNullOrEmpty(currentPath) ? string.Empty : currentPath + " => ") + package.ToString();

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
                throw new Exception($"Error obtaining dependencies at {currentPath}: {ex.Message}", ex);
            }

            var dependencyHierarchiesTasks = dependencies
                .Select(
                    p => expansionExclusionFilters.Any(f => f.IsMatch(p.Id))
                        ? Task.FromResult(new PackageHierarchy(p))
                        : this.GetPackageHierarchyAsync(
                            p,
                            targetFramework,
                            dependencyExclusionFilters,
                            expansionExclusionFilters,
                            currentPath,
                            cancellationToken));

            var children = await Task.WhenAll(dependencyHierarchiesTasks).ConfigureAwait(false);

            return new PackageHierarchy(package, children);
        }

        private bool TryResolveDependencies(
            IPackageSearchMetadata packageMetadata,
            NuGetFramework targetFramework,
            out IEnumerable<PackageDependency> packageDependencies,
            out NuGetFramework packageFramework)
        {
            packageDependencies = null;
            packageFramework = null;

            if (!packageMetadata.DependencySets.Any())
            {
                packageDependencies = Array.Empty<PackageDependency>();
                packageFramework = NuGetFramework.AnyFramework;
            }
            else
            {
                var packageDependencyGroup = NuGetFrameworkUtility.GetNearest(packageMetadata.DependencySets, targetFramework);
                if (packageDependencyGroup == null)
                {
                    return false;
                }

                packageDependencies = packageDependencyGroup.Packages;
                packageFramework = packageDependencyGroup.TargetFramework;
            }

            return true;
        }
    }
}
