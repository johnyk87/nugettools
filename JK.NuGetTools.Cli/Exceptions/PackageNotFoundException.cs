namespace JK.NuGetTools.Cli.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    public class PackageNotFoundException : NuGetToolsException
    {
        public PackageNotFoundException()
        {
        }

        public PackageNotFoundException(string message)
            : base(message)
        {
        }

        public PackageNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PackageNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
