namespace JK.NuGetTools.Cli
{
    using System;
    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;

    public class NuGetRepositoryBuilder
    {
        public const string DefaultFeedUrl = "https://api.nuget.org/v3/index.json";

        private SourceCacheContext sourceCacheContext;
        private ILogger logger;
        private string feedUrl;
        private string username;
        private string password;

        public NuGetRepositoryBuilder()
        {
            this.sourceCacheContext = new SourceCacheContext();
            this.logger = NullLogger.Instance;
            this.feedUrl = DefaultFeedUrl;
        }

        public NuGetRepositoryBuilder WithSourceCacheContext(SourceCacheContext sourceCacheContext)
        {
            this.sourceCacheContext = sourceCacheContext ?? throw new ArgumentNullException(nameof(sourceCacheContext));
            return this;
        }

        public NuGetRepositoryBuilder WithLogger(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        public NuGetRepositoryBuilder WithFeedUrl(string feedUrl)
        {
            if (string.IsNullOrEmpty(feedUrl))
            {
                throw new ArgumentException("Argument must not be null or empty", nameof(feedUrl));
            }

            this.feedUrl = feedUrl;
            return this;
        }

        public NuGetRepositoryBuilder WithUsername(string username)
        {
            this.username = username;
            return this;
        }

        public NuGetRepositoryBuilder WithPassword(string password)
        {
            this.password = password;
            return this;
        }

        public NuGetRepository Build()
        {
            var source = new PackageSource(this.feedUrl);

            if (!string.IsNullOrEmpty(this.username))
            {
                source.Credentials = new PackageSourceCredential(
                    this.feedUrl,
                    this.username,
                    this.password,
                    isPasswordClearText: true);
            }

            var sourceRepository = Repository.Factory.GetCoreV2(source);

            return new NuGetRepository(sourceRepository, this.sourceCacheContext, this.logger);
        }
    }
}
