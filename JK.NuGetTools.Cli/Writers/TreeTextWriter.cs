namespace JK.NuGetTools.Cli.Writers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    public class TreeTextWriter : IHierarchyWriter
    {
        private readonly TextWriter textWriter;

        public TreeTextWriter(TextWriter textWriter)
        {
            this.textWriter = textWriter ?? throw new System.ArgumentNullException(nameof(textWriter));
        }

        public Task WriteAsync(IHierarchy hierarchy)
        {
            return this.WriteAsync(hierarchy, 0);
        }

        private static string Indent(int level, string indentString)
        {
            var stringBuilder = new StringBuilder();

            while (level > 0)
            {
                stringBuilder.Append(indentString);

                level--;
            }

            return stringBuilder.ToString();
        }

        private async Task WriteAsync(IHierarchy hierarchy, int level)
        {
            await this.textWriter.WriteLineAsync($"{Indent(level, "| ")}{hierarchy.Value.ToString()}").ConfigureAwait(false);

            foreach (var child in hierarchy.Children)
            {
                await this.WriteAsync(child, level + 1).ConfigureAwait(false);
            }
        }
    }
}
