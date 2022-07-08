namespace JK.NuGetTools.Cli
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Threading;
    using System.Threading.Tasks;
    using JK.NuGetTools.Cli.Utils;
    using JK.NuGetTools.Cli.Writers;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Extensions.DependencyInjection;
    using NuGet.Frameworks;
    using NuGet.Versioning;

    public class Program
    {
        private const string DefaultTargetFrameworkString = FrameworkConstants.SpecialIdentifiers.Any;
        private const string DefaultHierarchyWriterTypeString = nameof(TextHierarchyWriterFactory.WriterType.Tree);
        private const string DefaultDependencyExclusionFiltersString = "";
        private const string DefaultExpansionExclusionFiltersString = "";

        private readonly IConsole console;
        private readonly NuGetRepositoryBuilder sourceRepositoryBuilder;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly TextHierarchyWriterFactory hierarchyWriterFactory;

        public Program(
            IConsole console,
            NuGetRepositoryBuilder sourceRepositoryBuilder,
            CancellationTokenSource cancellationTokenSource,
            TextHierarchyWriterFactory hierarchyWriterFactory)
        {
            this.console = console;
            this.sourceRepositoryBuilder = sourceRepositoryBuilder;
            this.cancellationTokenSource = cancellationTokenSource;
            this.hierarchyWriterFactory = hierarchyWriterFactory;

            this.console.CancelKeyPress += this.OnCancelKeyPress;
        }

        private enum ErrorCode
        {
            Success = 0,
            UnknownError = -1,
            OperationCanceled = -2,
        }

        [Argument(0, Description = "The package identifier")]
        [Required]
        public string PackageId { get; }

        [Option("-s|--source-feed-url", Description = "The URL of the source feed. Default: \"" + NuGetRepositoryBuilder.DefaultFeedUrl + "\".")]
        public string SourceFeedUrl { get; }

        [Option("-t|--target-framework", Description = "The target framework. Default: \"" + DefaultTargetFrameworkString + "\".")]
        [SupportedNuGetFramework]
        public string TargetFramework { get; }

        [Option("-wt|--writer-type", Description = "The type of writer to use to print the dependencies. Default: \"" + DefaultHierarchyWriterTypeString + "\".")]
        [SupportedEnumValue(typeof(TextHierarchyWriterFactory.WriterType))]
        public string WriterType { get; }

        [Option("-def|--dependency-exclusion-filter", Description = "The exclusion Regex filters to apply on the dependencies of each package. Packages matching the filter will not be listed as dependencies of other packages and won't not be expanded. Default: \"" + DefaultDependencyExclusionFiltersString + "\".")]
        public string[] DependencyExclusionFilters { get; }

        [Option("-eef|--expansion-exclusion-filter", Description = "The exclusion Regex filters to apply on the parent of a given dependency branch. Packages matching the filter may be listed but their dependencies will not be expanded. Default: \"" + DefaultExpansionExclusionFiltersString + "\".")]
        public string[] ExpansionExclusionFilters { get; }

        [Option("-u|--username", Description = "Username to use for authenticated feed.")]
        public string Username { get; }

        [Option("-p|--password", Description = "Password to use for authenticated feed.")]
        public string Password { get; }

        [Option("-pv|--package-version", Description = "Version of the target package to start with. Default: the latest version available.")]
        public string PackageVersion { get; }

        public static int Main(string[] args)
        {
            var serviceProvider = BuildServiceProvider();

            var app = new CommandLineApplication<Program>(serviceProvider.GetRequiredService<IConsole>());

            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(serviceProvider);

            app.OnExecuteAsync(app.Model.OnExecuteAsync);

            return app.Execute(args);
        }

        private static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            var consoleInstance = PhysicalConsole.Singleton;
            services.AddSingleton(consoleInstance);
            services.AddSingleton(consoleInstance.Out);

            services.AddSingleton<NuGetRepositoryBuilder>();

            services.AddSingleton<CancellationTokenSource>();

            services.AddSingleton<TextHierarchyWriterFactory>();

            return services.BuildServiceProvider();
        }

        private async Task<int> OnExecuteAsync(CancellationToken cancellationToken)
        {
            var sourceRepository = this.sourceRepositoryBuilder
                .WithFeedUrl(this.SourceFeedUrl ?? NuGetRepositoryBuilder.DefaultFeedUrl)
                .WithUsername(this.Username)
                .WithPassword(this.Password)
                .Build();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                this.cancellationTokenSource.Token, cancellationToken);
            var targetFramework = NuGetFramework.Parse(this.TargetFramework ?? DefaultTargetFrameworkString);
            var dependencyExclusionFilters = ConsoleUtils.ToRegexEnumerable(this.DependencyExclusionFilters, DefaultDependencyExclusionFiltersString);
            var expansionExclusionFilters = ConsoleUtils.ToRegexEnumerable(this.ExpansionExclusionFilters, DefaultExpansionExclusionFiltersString);

            try
            {
                var versionRange = string.IsNullOrEmpty(this.PackageVersion)
                    ? null
                    : VersionRange.Parse(this.PackageVersion);

                var package = await sourceRepository.GetLatestPackageAsync(
                    this.PackageId, versionRange, cts.Token).ConfigureAwait(false);

                var packageHierarchy = await sourceRepository.GetPackageHierarchyAsync(
                    package, targetFramework, dependencyExclusionFilters, expansionExclusionFilters, cts.Token).ConfigureAwait(false);

                await this.WriteHierarchyAsync(packageHierarchy).ConfigureAwait(false);

                return (int)ErrorCode.Success;
            }
            catch (OperationCanceledException)
            {
                return (int)ErrorCode.OperationCanceled;
            }
            catch (Exception ex)
            {
                await this.WriteExceptionAsync(ex).ConfigureAwait(false);

                return (int)ErrorCode.UnknownError;
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            this.console.Error.WriteLine("Operation cancellation requested.");
            this.cancellationTokenSource.Cancel();
        }

        private Task WriteHierarchyAsync(IHierarchy hierarchy)
        {
            var writerType = ConsoleUtils.ToEnum<TextHierarchyWriterFactory.WriterType>(this.WriterType, DefaultHierarchyWriterTypeString);

            var hierarchyWriter = this.hierarchyWriterFactory.Create(writerType);

            return hierarchyWriter.WriteAsync(hierarchy);
        }

        private async Task WriteExceptionAsync(Exception ex)
        {
            await this.console.Error.WriteLineAsync($"{ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);

            foreach (var dataKey in ex.Data.Keys)
            {
                await this.console.Error.WriteLineAsync($"{dataKey}: {ex.Data[dataKey]}").ConfigureAwait(false);
            }

            await this.console.Error.WriteLineAsync($"StackTrace:{Environment.NewLine}{ex.StackTrace}").ConfigureAwait(false);
        }
    }
}
