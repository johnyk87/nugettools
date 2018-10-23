namespace JK.NuGetTools.Cli.Writers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class GraphTextWriter : IHierarchyWriter
    {
        private readonly TextWriter textWriter;

        public GraphTextWriter(TextWriter textWriter)
        {
            this.textWriter = textWriter ?? throw new System.ArgumentNullException(nameof(textWriter));
        }

        public async Task WriteAsync(IHierarchy hierarchy)
        {
            var expandedPackages = new HashSet<string>();

            await this.WriteLineAsync($"digraph \"{hierarchy.Value.ToString()}\" {{").ConfigureAwait(false);

            await this.WriteChildrenAsync(hierarchy, expandedPackages).ConfigureAwait(false);

            await this.WriteLineAsync("}").ConfigureAwait(false);
            await this.WriteLineAsync(
                "# The graph is represented in DOT language and can be visualized with any graphviz based visualizer like the online tool http://viz-js.com/.")
                .ConfigureAwait(false);
        }

        private async Task WriteChildrenAsync(IHierarchy hierarchy, HashSet<string> expandedPackages)
        {
            foreach (var child in hierarchy.Children)
            {
                var childDescription = child.Value.ToString();

                await this.WriteLineAsync($"  \"{hierarchy.Value.ToString()}\" -> \"{childDescription}\"").ConfigureAwait(false);

                if (expandedPackages.Contains(childDescription))
                {
                    continue;
                }

                await this.WriteChildrenAsync(child, expandedPackages);

                expandedPackages.Add(childDescription);
            }
        }

        private Task WriteLineAsync(string line)
        {
            return this.textWriter.WriteLineAsync(line);
        }
    }
}
