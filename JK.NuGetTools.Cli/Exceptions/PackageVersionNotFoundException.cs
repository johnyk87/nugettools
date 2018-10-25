namespace JK.NuGetTools.Cli.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class PackageVersionNotFoundException : NuGetToolsException
    {
        public PackageVersionNotFoundException()
        {
        }

        public PackageVersionNotFoundException(string message)
            : base(message)
        {
        }

        public PackageVersionNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PackageVersionNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
