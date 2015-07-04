using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlatRedNetwork
{
    /// <summary>
    /// Custom exception type for this library. Allows catching
    /// of network-specific exceptions.
    /// </summary>
    public class FlatRedNetworkException : SystemException
    {
        public FlatRedNetworkException(string message)
            : base(message)
        {

        }

        public FlatRedNetworkException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
