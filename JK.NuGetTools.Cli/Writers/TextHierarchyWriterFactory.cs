namespace JK.NuGetTools.Cli.Writers
{
    using System;
    using System.IO;

    public class TextHierarchyWriterFactory
    {
        private readonly TextWriter textWriter;

        public TextHierarchyWriterFactory(TextWriter textWriter)
        {
            this.textWriter = textWriter ?? throw new ArgumentNullException(nameof(textWriter));
        }

        public enum WriterType
        {
            Tree = 0,
            Graph = 1,
        }

        public IHierarchyWriter Create(WriterType writerType)
        {
            switch (writerType)
            {
                case WriterType.Graph:
                    return new GraphTextWriter(this.textWriter);
                case WriterType.Tree:
                    return new TreeTextWriter(this.textWriter);
                default:
                    throw new NotImplementedException($"Unsupported writer type: {writerType}.");
            }
        }
    }
}
