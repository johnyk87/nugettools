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

    internal class NuGetRepository
    {
        private readonly SourceRepository sourceRepository;
        private readonly ILogger log;
        private readonly SourceCacheContext sourceCacheContext;
        private MetadataResource metadataResource;
        private PackageMetadataResource packageMetadataResource;

        public NuGetRepository(string feedUrl) : this(feedUrl, new SourceCacheContext(), NullLogger.Instance) { }

        public NuGetRepository(string feedUrl, SourceCacheContext sourceCacheContext, ILogger log)
        {
            this.sourceRepository = Repository.Factory.GetCoreV3(feedUrl);
            this.log = log;
            this.sourceCacheContext = sourceCacheContext;
        }

        public Uri FeedUrl => this.sourceRepository.PackageSource.SourceUri;

        protected MetadataResource MetadataResource
        {
            get
            {
                lock(this)
                {
                    if (metadataResource == null)
                    {
                        metadataResource = sourceRepository.GetResource<MetadataResource>();
                    }

                    return metadataResource;
                }
            }
        }

        protected PackageMetadataResource PackageMetadataResource
        {
            get
            {
                lock(this)
                {
                    if (packageMetadataResource == null)
                    {
                        packageMetadataResource = sourceRepository.GetResource<PackageMetadataResource>();
                    }

                    return packageMetadataResource;
                }
            }
        }

        public virtual async Task<PackageIdentity> GetLatestPackageAsync(string packageId
                                                                        , VersionRange versionRange
                                                                        , CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentNullException(nameof(packageId));

            var packageVersions = await this.MetadataResource.GetVersions(packageId, true, true, this.sourceCacheContext, this.log, cancellationToken).ConfigureAwait(false);

            versionRange = new VersionRange(versionRange ?? VersionRange.All, new FloatRange(NuGetVersionFloatBehavior.Major));

            var packageVersion = versionRange.FindBestMatch(packageVersions);
            
            if (packageVersion == null)
            {
                throw new Exception($"Package \"{packageId}\" not found in repository {this.FeedUrl.ToString()}");
            }

            return new PackageIdentity(packageId, packageVersion);
        }

        public virtual async Task<IEnumerable<PackageIdentity>> GetPackageDependenciesAsync(PackageIdentity package
                                                                                           , NuGetFramework targetFramework
                                                                                           , CancellationToken cancellationToken)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            var packageMetadata = await this.PackageMetadataResource.GetMetadataAsync(package, this.sourceCacheContext, this.log, cancellationToken).ConfigureAwait(false);

            if (!TryResolveDependencies(packageMetadata, targetFramework, out var packageDependencies, out var packageFramework))
            {
                throw new Exception($"Package \"{packageMetadata.Identity.ToString()}\" is not compatible with {targetFramework.GetShortFolderName()}");
            }

            return await Task.WhenAll(packageDependencies.Select(p => this.GetLatestPackageAsync(p.Id, p.VersionRange, cancellationToken))).ConfigureAwait(false);
        }


        public virtual Task<PackageHierarchy> GetPackageHierarchyAsync(PackageIdentity package
                                                                            , NuGetFramework targetFramework
                                                                            , CancellationToken cancellationToken)
        {
            return GetPackageHierarchyAsync(package, targetFramework, string.Empty, cancellationToken);
        }


        protected virtual async Task<PackageHierarchy> GetPackageHierarchyAsync(PackageIdentity package
                                                                               , NuGetFramework targetFramework
                                                                               , string currentPath
                                                                               , CancellationToken cancellationToken)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (targetFramework == null) throw new ArgumentNullException(nameof(targetFramework));
            
            currentPath = (string.IsNullOrEmpty(currentPath) ? "" : currentPath + " => ") + package.ToString();

            var dependencies = default(IEnumerable<PackageIdentity>);

            try
            {
                dependencies = await GetPackageDependenciesAsync(package, targetFramework, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error obtaining dependencies at {currentPath}: {ex.Message}", ex);
            }

            var children = await Task.WhenAll(dependencies.Select(p => GetPackageHierarchyAsync(p, targetFramework, currentPath, cancellationToken))).ConfigureAwait(false);

            return new PackageHierarchy(package, children);
        }

        private bool TryResolveDependencies(IPackageSearchMetadata packageMetadata
                                           , NuGetFramework targetFramework
                                           , out IEnumerable<PackageDependency> packageDependencies
                                           , out NuGetFramework packageFramework)
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
