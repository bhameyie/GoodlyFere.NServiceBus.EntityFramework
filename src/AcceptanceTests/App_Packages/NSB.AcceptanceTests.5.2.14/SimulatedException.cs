using System;
using System.Runtime.Serialization;

namespace AcceptanceTests.App_Packages.NSB.AcceptanceTests._5._2._14
{
    public class SimulatedException : Exception
    {
        public SimulatedException()
        {
        }

        public SimulatedException(string message)
            : base(message)
        {
        }

        public SimulatedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SimulatedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}