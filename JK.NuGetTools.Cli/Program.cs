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

        private static readonly NuGetFramework DefaultTargetFramework = NuGetFramework.AnyFramework;

        private readonly IConsole console;

        public Program(IConsole console)
        {
            this.console = console;
        }

        private enum ErrorCode
        {
            Success = 0,
            UnknownError = -1,
        }

        [Argument(0, Description = "The package identifier")]
        [Required]
        public string PackageId { get; }

        [Option("-t|--target-framework", Description = "The target framework. Default: any.")]
        [SupportedNuGetFramework]
        public string TargetFramework { get; }

        [Option("-s|--source-feed-url", Description = "The URL of the source feed. Default: \"" + DefaultFeedUrl + "\".")]
        public string SourceFeedUrl { get; }

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

        private async Task<int> OnExecuteAsync()
        {
            var targetFramework = this.TargetFramework == null ? DefaultTargetFramework : NuGetFramework.Parse(this.TargetFramework);
            var cancellationToken = CancellationToken.None;
            var sourceRepository = new CachedNuGetRepository(this.SourceFeedUrl ?? DefaultFeedUrl);

            try
            {
                var package = await sourceRepository.GetLatestPackageAsync(this.PackageId, null, cancellationToken).ConfigureAwait(false);

                var packageHierarchy = await sourceRepository.GetPackageHierarchyAsync(package, targetFramework, cancellationToken).ConfigureAwait(false);

                var exclusionFilters = new List<Regex>();
                exclusionFilters.Add(new Regex("^System"));
                exclusionFilters.Add(new Regex("^Microsoft"));

                // TODO: add a flag to control output mode
#if true
                this.PrintPackageHierarchyAsGraph(packageHierarchy, ref exclusionFilters);
#else
                this.PrintPackageHierarchyAsList(packageHierarchy, ref exclusionFilters);
#endif

                return (int)ErrorCode.Success;
            }
            catch (Exception ex)
            {
                this.console.Error.WriteLine($"Error: {ex.Message}. StackTrace: {ex.StackTrace}");

                return (int)ErrorCode.UnknownError;
            }
        }

        private void PrintPackageHierarchyAsGraph(PackageHierarchy packageHierarchy, ref List<Regex> exclusionFilters)
        {
            if (exclusionFilters == null)
            {
                exclusionFilters = new List<Regex>();
            }

            this.console.WriteLine($"digraph \"{packageHierarchy.Identity.ToString()}\" {{");

            this.PrintPackageHierarchyAsGraph(packageHierarchy, ref exclusionFilters, 1);

            this.console.WriteLine("}");
            this.console.WriteLine("The graph can be visualized with any graphviz based visualizer like the online tool http://viz-js.com/.");
        }

        private void PrintPackageHierarchyAsGraph(PackageHierarchy packageHierarchy, ref List<Regex> exclusionFilters, int level)
        {
            foreach (var child in packageHierarchy.Children)
            {
                this.console.WriteLine($"{Indent(1)}\"{packageHierarchy.Identity.ToString()}\" -> \"{child.Identity.ToString()}\"");

                if (exclusionFilters.Any(f => f.IsMatch(child.Identity.Id)))
                {
                    continue;
                }

                exclusionFilters.Add(new Regex($"^{child.Identity.Id}$"));

                this.PrintPackageHierarchyAsGraph(child, ref exclusionFilters, level + 1);
            }
        }

        private void PrintPackageHierarchyAsList(PackageHierarchy packageHierarchy, ref List<Regex> exclusionFilters, int level = 0)
        {
            if (exclusionFilters == null)
            {
                exclusionFilters = new List<Regex>();
            }

            this.console.WriteLine($"{Indent(level, "| ")}{packageHierarchy.Identity.ToString()}");

            if (exclusionFilters.Any(f => f.IsMatch(packageHierarchy.Identity.Id)))
            {
                return;
            }

            foreach (var child in packageHierarchy.Children)
            {
                this.PrintPackageHierarchyAsList(child, ref exclusionFilters, level + 1);
            }
        }
    }
}
