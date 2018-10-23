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
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Extensions.DependencyInjection;
    using NuGet.Frameworks;
    using NuGet.Packaging.Core;

    public class Program
    {
        private const string DefaultTargetFrameworkString = FrameworkConstants.SpecialIdentifiers.Any;
        private const string DefaultDependencyDisplayModeString = "Tree";
        private const string DefaultDependencyExclusionFiltersString = "";
        private const string DefaultExpansionExclusionFiltersString = "^System|^Microsoft";

        private readonly IConsole console;
        private readonly NuGetRepositoryBuilder sourceRepositoryBuilder;
        private readonly CancellationTokenSource cancellationTokenSource;

        public Program(
            IConsole console,
            NuGetRepositoryBuilder sourceRepositoryBuilder,
            CancellationTokenSource cancellationTokenSource)
        {
            this.console = console;
            this.sourceRepositoryBuilder = sourceRepositoryBuilder;
            this.cancellationTokenSource = cancellationTokenSource;

            this.console.CancelKeyPress += this.OnCancelKeyPress;
        }

        private enum ErrorCode
        {
            Success = 0,
            UnknownError = -1,
            OperationCanceled = -2,
        }

        private enum DependencyDisplayMode
        {
            Tree = 0,
            Graph = 1,
        }

        [Argument(0, Description = "The package identifier")]
        [Required]
        public string PackageId { get; }

        [Option("-s|--source-feed-url", Description = "The URL of the source feed. Default: \"" + NuGetRepositoryBuilder.DefaultFeedUrl + "\".")]
        public string SourceFeedUrl { get; }

        [Option("-t|--target-framework", Description = "The target framework. Default: \"" + DefaultTargetFrameworkString + "\".")]
        [SupportedNuGetFramework]
        public string TargetFramework { get; }

        [Option("-d|--display-mode", Description = "The mode in which to display the dependencies. Default: \"" + DefaultDependencyDisplayModeString + "\".")]
        [SupportedEnumValue(typeof(DependencyDisplayMode))]
        public string DisplayMode { get; }

        [Option("-def|--dependency-exclusion-filter", Description = "The exclusion Regex filters to apply on the dependencies of each package. Packages matching the filter will not be listed as dependencies of other packages and won't not be expanded. Default: \"" + DefaultDependencyExclusionFiltersString + "\".")]
        public string[] DependencyExclusionFilters { get; }

        [Option("-eef|--expansion-exclusion-filter", Description = "The exclusion Regex filters to apply on the parent of a given dependency branch. Packages matching the filter may be listed but their dependencies will not be expanded. Default: \"" + DefaultExpansionExclusionFiltersString + "\".")]
        public string[] ExpansionExclusionFilters { get; }

        public static int Main(string[] args)
        {
            var services = new ServiceCollection();

            var console = PhysicalConsole.Singleton;
            services.AddSingleton(console);

            services.AddSingleton<NuGetRepositoryBuilder>();

            services.AddSingleton<CancellationTokenSource>();

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

                this.PrintPackageHierarchy(packageHierarchy);

                return (int)ErrorCode.Success;
            }
            catch (OperationCanceledException)
            {
                return (int)ErrorCode.OperationCanceled;
            }
            catch (Exception ex)
            {
                this.console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
                this.console.Error.WriteLine($"StackTrace:{Environment.NewLine}{ex.StackTrace}");

                return (int)ErrorCode.UnknownError;
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            this.console.Error.WriteLine("Operation cancellation requested.");
            this.cancellationTokenSource.Cancel();
        }

        private void PrintPackageHierarchy(PackageHierarchy packageHierarchy)
        {
            var displayMode = Enum.Parse(typeof(DependencyDisplayMode), this.DisplayMode ?? DefaultDependencyDisplayModeString, true);

            switch (displayMode)
            {
                case DependencyDisplayMode.Graph:
                    this.PrintPackageHierarchyAsGraph(packageHierarchy);
                    break;
                case DependencyDisplayMode.Tree:
                default:
                    this.PrintPackageHierarchyAsTree(packageHierarchy);
                    break;
            }
        }

        private void PrintPackageHierarchyAsGraph(PackageHierarchy packageHierarchy)
        {
            var expandedPackages = new List<string>();

            this.console.WriteLine($"digraph \"{packageHierarchy.Identity.ToString()}\" {{");

            this.PrintPackageHierarchyChildrenAsGraph(packageHierarchy, ref expandedPackages);

            this.console.WriteLine("}");
            this.console.WriteLine("# The graph is represented in DOT language and can be visualized with any graphviz based visualizer like the online tool http://viz-js.com/.");
        }

        private void PrintPackageHierarchyChildrenAsGraph(PackageHierarchy packageHierarchy, ref List<string> expandedPackages)
        {
            foreach (var child in packageHierarchy.Children)
            {
                this.console.WriteLine($"{Indent(1)}\"{packageHierarchy.Identity.ToString()}\" -> \"{child.Identity.ToString()}\"");

                if (expandedPackages.Contains(child.Identity.Id))
                {
                    continue;
                }

                this.PrintPackageHierarchyChildrenAsGraph(child, ref expandedPackages);

                expandedPackages.Add(child.Identity.Id);
            }
        }

        private void PrintPackageHierarchyAsTree(PackageHierarchy packageHierarchy, int level = 0)
        {
            this.console.WriteLine($"{Indent(level, "| ")}{packageHierarchy.Identity.ToString()}");

            foreach (var child in packageHierarchy.Children)
            {
                this.PrintPackageHierarchyAsTree(child, level + 1);
            }
        }
    }
}
