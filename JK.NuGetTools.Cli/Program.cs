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
    using NuGet.Frameworks;
    using NuGet.Packaging.Core;

    public class Program
    {
        private const string DefaultFeedUrl = "https://api.nuget.org/v3/index.json";

        private const string DefaultDependencyExclusionFiltersString = "";

        private const string DefaultExpansionExclusionFiltersString = "^System|^Microsoft";

        private const DependencyDisplayMode DefaultDependencyDisplayMode = DependencyDisplayMode.Tree;

        private static readonly NuGetFramework DefaultTargetFramework = NuGetFramework.AnyFramework;

        private readonly IConsole console;
        private readonly CancellationTokenSource cancellationTokenSource;

        public Program(IConsole console)
        {
            this.console = console;
            this.cancellationTokenSource = new CancellationTokenSource();

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

        [Option("-t|--target-framework", Description = "The target framework. Default: \"any\".")]
        [SupportedNuGetFramework]
        public string TargetFramework { get; }

        [Option("-d|--display-mode", Description = "The mode in which to display the dependencies. Default: \"" + "tree" + "\".")]
        [SupportedEnumValue(typeof(DependencyDisplayMode))]
        public string DisplayMode { get; }

        [Option("-s|--source-feed-url", Description = "The URL of the source feed. Default: \"" + DefaultFeedUrl + "\".")]
        public string SourceFeedUrl { get; }

        [Option("-def|--dependency-exclusion-filter", Description = "The exclusion Regex filters to apply on the dependencies of each package. Packages matching the filter will not be listed as dependencies of other packages and won't not be expanded. Default: \"" + DefaultDependencyExclusionFiltersString + "\".")]
        public string[] DependencyExclusionFilters { get; }

        [Option("-eef|--expansion-exclusion-filter", Description = "The exclusion Regex filters to apply on the parent of a given dependency branch. Packages matching the filter may be listed but their dependencies will not be expanded. Default: \"" + DefaultExpansionExclusionFiltersString + "\".")]
        public string[] ExpansionExclusionFilters { get; }

        public static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);

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
            var targetFramework = this.TargetFramework == null ? DefaultTargetFramework : NuGetFramework.Parse(this.TargetFramework);
            var sourceRepository = new CachedNuGetRepository(this.SourceFeedUrl ?? DefaultFeedUrl);
            var cancellationToken = this.cancellationTokenSource.Token;
            var dependencyExclusionFilters = GetFilterList(this.DependencyExclusionFilters, DefaultDependencyExclusionFiltersString);
            var expansionExclusionFilters = GetFilterList(this.ExpansionExclusionFilters, DefaultExpansionExclusionFiltersString);

            try
            {
                var package = await sourceRepository.GetLatestPackageAsync(
                    this.PackageId, null, cancellationToken).ConfigureAwait(false);

                var packageHierarchy = await sourceRepository.GetPackageHierarchyAsync(
                    package, targetFramework, cancellationToken).ConfigureAwait(false);

                this.PrintPackageHierarchy(packageHierarchy, dependencyExclusionFilters, expansionExclusionFilters);

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

        private void PrintPackageHierarchy(
            PackageHierarchy packageHierarchy,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters)
        {
            var displayMode = this.DisplayMode == null
                ? DefaultDependencyDisplayMode
                : Enum.Parse(typeof(DependencyDisplayMode), this.DisplayMode, true);

            switch (displayMode)
            {
                case DependencyDisplayMode.Graph:
                    this.PrintPackageHierarchyAsGraph(packageHierarchy, dependencyExclusionFilters, expansionExclusionFilters);
                    break;
                case DependencyDisplayMode.Tree:
                default:
                    this.PrintPackageHierarchyAsTree(packageHierarchy, dependencyExclusionFilters, expansionExclusionFilters);
                    break;
            }
        }

        private void PrintPackageHierarchyAsGraph(
            PackageHierarchy packageHierarchy,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters)
        {
            var expandedPackages = new List<string>();

            this.console.WriteLine($"digraph \"{packageHierarchy.Identity.ToString()}\" {{");

            if (!expansionExclusionFilters.Any(f => f.IsMatch(packageHierarchy.Identity.Id)))
            {
                this.PrintPackageHierarchyChildrenAsGraph(packageHierarchy, dependencyExclusionFilters, expansionExclusionFilters, ref expandedPackages);
            }

            this.console.WriteLine("}");
            this.console.WriteLine("# The graph is represented in DOT language and can be visualized with any graphviz based visualizer like the online tool http://viz-js.com/.");
        }

        private void PrintPackageHierarchyChildrenAsGraph(
            PackageHierarchy packageHierarchy,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters,
            ref List<string> expandedPackages)
        {
            foreach (var child in packageHierarchy.Children)
            {
                if (dependencyExclusionFilters.Any(r => r.IsMatch(child.Identity.Id)))
                {
                    continue;
                }

                this.console.WriteLine($"{Indent(1)}\"{packageHierarchy.Identity.ToString()}\" -> \"{child.Identity.ToString()}\"");

                if (expandedPackages.Contains(child.Identity.Id))
                {
                    continue;
                }

                if (expansionExclusionFilters.Any(f => f.IsMatch(child.Identity.Id)))
                {
                    continue;
                }

                this.PrintPackageHierarchyChildrenAsGraph(child, dependencyExclusionFilters, expansionExclusionFilters, ref expandedPackages);

                expandedPackages.Add(child.Identity.Id);
            }
        }

        private void PrintPackageHierarchyAsTree(
            PackageHierarchy packageHierarchy,
            IEnumerable<Regex> dependencyExclusionFilters,
            IEnumerable<Regex> expansionExclusionFilters,
            int level = 0)
        {
            this.console.WriteLine($"{Indent(level, "| ")}{packageHierarchy.Identity.ToString()}");

            if (expansionExclusionFilters.Any(f => f.IsMatch(packageHierarchy.Identity.Id)))
            {
                return;
            }

            foreach (var child in packageHierarchy.Children)
            {
                if (dependencyExclusionFilters.Any(r => r.IsMatch(child.Identity.Id)))
                {
                    continue;
                }

                this.PrintPackageHierarchyAsTree(child, dependencyExclusionFilters, expansionExclusionFilters, level + 1);
            }
        }
    }
}
