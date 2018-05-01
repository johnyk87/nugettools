using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                var package = await sourceRepository.GetLatestPackageAsync(packageId, null, cancellationToken);

                var packageHierarchy = await sourceRepository.GetPackageHierarchyAsync(package, targetFramework, cancellationToken);

                PrintPackageHierarchy(packageHierarchy);
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

        private static void PrintPackageHierarchy(PackageHierarchy packageHierarchy, int level = 0)
        {
            Console.WriteLine($"{Indent(level)}{packageHierarchy.Identity.ToString()}");

            foreach(var child in packageHierarchy.Children)
            {
                PrintPackageHierarchy(child, level + 1);
            }
        }

        private static string Indent(int level)
        {
            string indent = string.Empty;

            while(level > 0)
            {
                indent += "| ";

                level--;
            }

            return indent;
        }
    }
}
