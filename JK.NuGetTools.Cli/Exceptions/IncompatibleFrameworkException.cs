namespace JK.NuGetTools.Cli.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    public class IncompatibleFrameworkException : NuGetToolsException
    {
        public IncompatibleFrameworkException()
        {
        }

        public IncompatibleFrameworkException(string message)
            : base(message)
        {
        }

        public IncompatibleFrameworkException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected IncompatibleFrameworkException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
