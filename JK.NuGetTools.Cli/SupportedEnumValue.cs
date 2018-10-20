namespace JK.NuGetTools.Cli
{
    using System;
    using System.ComponentModel.DataAnnotations;

    internal class SupportedEnumValue : ValidationAttribute
    {
        private readonly Type enumType;

        public SupportedEnumValue(Type enumType)
            : base($"The value for {{0}} must be a supported {enumType.Name}.")
        {
            this.enumType = enumType;
        }

        public override bool IsValid(object value)
        {
            return Enum.TryParse(this.enumType, value as string, true, out var _);
        }
    }
}