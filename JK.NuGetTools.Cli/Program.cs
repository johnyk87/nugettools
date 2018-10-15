using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace JK.NuGetTools.Cli
{
    class Program
    {
        private static readonly NuGetFramework DefaultTargetFramework = NuGetFramework.AnyFramework;
        private static readonly string DefaultFeedUrl = "https://api.nuget.org/v3/index.json";

        static async Task Main(string[] args)
        {
            if (!TryParseArguments(args, out string packageId, out NuGetFramework targetFramework, out string feedUrl, out string errorMessage))
            {
                Console.WriteLine(errorMessage);
                ShowUsage();
                return;
            }

            var cancellationToken = CancellationToken.None;
            var sourceRepository = new CachedNuGetRepository(feedUrl);
            
            try
            {
                var package = await sourceRepository.GetLatestPackageAsync(packageId, null, cancellationToken).ConfigureAwait(false);

                var packageHierarchy = await sourceRepository.GetPackageHierarchyAsync(package, targetFramework, cancellationToken).ConfigureAwait(false);

                var exclusionFilters = new List<Regex>();
                exclusionFilters.Add(new Regex("^System"));
                exclusionFilters.Add(new Regex("^Microsoft"));

                PrintPackageHierarchyAsGraph(packageHierarchy, ref exclusionFilters);
                //PrintPackageHierarchyAsList(packageHierarchy, ref exclusionFilters);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}. StackTrace: {ex.StackTrace}");
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine($"$> dotnet run {Assembly.GetEntryAssembly().GetName().Name} <packageId> [<targetFramework>] [<feedUrl>]");
        }

        private static bool TryParseArguments(string[] args, out string packageId, out NuGetFramework targetFramework, out string feedUrl, out string errorMessage)
        {
            packageId = null;
            targetFramework = DefaultTargetFramework;
            feedUrl = DefaultFeedUrl;
            errorMessage = string.Empty;
            var argIdx = 0;

            if (args.Length == 0)
            {
                errorMessage = "Invalid number of arguments. See usage.";
                return false;
            }

            if (args.Length > argIdx)
            {
                packageId = args[argIdx];
            }
            ++argIdx;
            
            if (args.Length > argIdx)
            {
                targetFramework = NuGetFramework.Parse(args[argIdx]);
                if (targetFramework.IsUnsupported)
                {
                    errorMessage = $"Unsupported target framework: {args[argIdx]}";
                    return false;
                }
            }
            ++argIdx;

            if (args.Length > argIdx)
            {
                feedUrl = args[argIdx];
            }
            ++argIdx;

            return true;
        }

        private static void PrintPackageHierarchyAsGraph(PackageHierarchy packageHierarchy, ref List<Regex> exclusionFilters)
        {
            if (exclusionFilters == null)
            {
                exclusionFilters = new List<Regex>();
            }

            Console.WriteLine($"digraph \"{packageHierarchy.Identity.ToString()}\" {{");

            PrintPackageHierarchyAsGraph(packageHierarchy, ref exclusionFilters, 1);

            Console.WriteLine("}");
            Console.WriteLine("The graph can be visualized with any graphviz based visualizer like the online tool http://viz-js.com/.");
        }

        private static void PrintPackageHierarchyAsGraph(PackageHierarchy packageHierarchy, ref List<Regex> exclusionFilters, int level)
        {
            foreach(var child in packageHierarchy.Children)
            {
                Console.WriteLine($"{Indent(level)}\"{packageHierarchy.Identity.ToString()}\" -> \"{child.Identity.ToString()}\"");

                if (exclusionFilters.Any(f => f.IsMatch(child.Identity.Id)))
                {
                    continue;
                }

                exclusionFilters.Add(new Regex($"^{child.Identity.Id}$"));

                PrintPackageHierarchyAsGraph(child, ref exclusionFilters, level + 1);
            }
        }

        private static void PrintPackageHierarchyAsList(PackageHierarchy packageHierarchy, ref List<Regex> exclusionFilters, int level = 0)
        {
            if (exclusionFilters == null)
            {
                exclusionFilters = new List<Regex>();
            }

            Console.WriteLine($"{Indent(level, "| ")}{packageHierarchy.Identity.ToString()}");

            if (exclusionFilters.Any(f => f.IsMatch(packageHierarchy.Identity.Id)))
            {
                return;
            }

            foreach(var child in packageHierarchy.Children)
            {
                PrintPackageHierarchyAsList(child, ref exclusionFilters, level + 1);
            }
        }

        private static string Indent(int level, string indentString = "  ")
        {
            string indent = string.Empty;

            while(level > 0)
            {
                indent += indentString;

                level--;
            }

            return indent;
        }
    }
}
