namespace JK.NuGetTools.Cli.Writers
{
    using System.Threading.Tasks;

    public interface IHierarchyWriter
    {
        Task WriteAsync(IHierarchy hierarchy);
    }
}
