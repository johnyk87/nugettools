namespace JK.NuGetTools.Cli.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class NuGetToolsException : Exception
    {
        public NuGetToolsException()
        {
        }

        public NuGetToolsException(string message)
            : base(message)
        {
        }

        public NuGetToolsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected NuGetToolsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
