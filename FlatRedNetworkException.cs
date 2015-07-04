using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin
{
    /// <summary>
    /// Custom exception type for this library. Allows catching
    /// of network-specific exceptions.
    /// </summary>
    public class RedGrinException : SystemException
    {
        public RedGrinException(string message)
            : base(message)
        {

        }

        public RedGrinException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
