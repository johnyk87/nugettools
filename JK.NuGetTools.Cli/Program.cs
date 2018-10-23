namespace JK.NuGetTools.Cli
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using JK.NuGetTools.Cli.Writers;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Extensions.DependencyInjection;
    using NuGet.Frameworks;
    using NuGet.Packaging.Core;

    public class Program
    {
        private const string DefaultTargetFrameworkString = FrameworkConstants.SpecialIdentifiers.Any;
        private const string DefaultHierarchyWriterTypeString = "Tree";
        private const string DefaultDependencyExclusionFiltersString = "";
        private const string DefaultExpansionExclusionFiltersString = "^System|^Microsoft";

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

        public static int Main(string[] args)
        {
            var services = new ServiceCollection();

            var console = PhysicalConsole.Singleton;
            services.AddSingleton(console);
            services.AddSingleton(console.Out);

            services.AddSingleton<NuGetRepositoryBuilder>();

            services.AddSingleton<CancellationTokenSource>();

            services.AddSingleton<TextHierarchyWriterFactory>();

            var app = new CommandLineApplication<Program>(console);

            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(services.BuildServiceProvider());

            return app.Execute(args);
        }

        private static string Indent(int level, string indentString = "  ")
        {
            var indent = string.Empty;

            while (level > 0)
            {
                indent += indentString;

                level--;
            }

            return indent;
        }

        private static IEnumerable<string> GetOptionAsEnumerable(string[] filterValue, string defaultValueString)
        {
            return filterValue == null ? (defaultValueString ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries) : filterValue;
        }

        private static T GetOptionAsEnumValue<T>(string optionValue, string defaultValueString, bool ignoreCase = true)
        {
            return (T)Enum.Parse(typeof(T), optionValue ?? defaultValueString, ignoreCase);
        }

        private static IEnumerable<Regex> GetFilterList(string[] filterValue, string defaultValueString)
        {
            return GetOptionAsEnumerable(filterValue, defaultValueString).Select(i => new Regex(i));
        }

        private async Task<int> OnExecuteAsync()
        {
            var sourceRepository = this.sourceRepositoryBuilder
                .WithFeedUrl(this.SourceFeedUrl ?? NuGetRepositoryBuilder.DefaultFeedUrl)
                .Build();
            var cancellationToken = this.cancellationTokenSource.Token;
            var targetFramework = NuGetFramework.Parse(this.TargetFramework ?? DefaultTargetFrameworkString);
            var dependencyExclusionFilters = GetFilterList(this.DependencyExclusionFilters, DefaultDependencyExclusionFiltersString);
            var expansionExclusionFilters = GetFilterList(this.ExpansionExclusionFilters, DefaultExpansionExclusionFiltersString);

            try
            {
                var package = await sourceRepository.GetLatestPackageAsync(
                    this.PackageId, null, cancellationToken).ConfigureAwait(false);

                var packageHierarchy = await sourceRepository.GetPackageHierarchyAsync(
                    package, targetFramework, dependencyExclusionFilters, expansionExclusionFilters, cancellationToken).ConfigureAwait(false);

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
            var writerType = GetOptionAsEnumValue<TextHierarchyWriterFactory.WriterType>(this.WriterType, DefaultHierarchyWriterTypeString);

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
