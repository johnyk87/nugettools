namespace JK.NuGetTools.Cli
{
    using System.ComponentModel.DataAnnotations;
    using NuGet.Frameworks;

    internal class SupportedNuGetFrameworkAttribute : ValidationAttribute
    {
        public SupportedNuGetFrameworkAttribute()
            : base("The value for {0} must be a supported NuGetFramework.")
        {
        }

        public override bool IsValid(object value)
        {
            var framework = NuGetFramework.Parse(value as string);

            return !framework.IsUnsupported;
        }
    }
}