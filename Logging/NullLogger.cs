using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin.Logging
{
    /// <summary>
    /// Default ILogger instance, swallows all logs.
    /// </summary>
    internal class NullLogger : ILogger
    {
        public void Debug(string message)
        {
            // swallow message
        }

        public void Info(string message)
        {
            // swallow message
        }

        public void Warning(string message)
        {
            // swallow message
        }

        public void Error(string message)
        {
            // swallow message
        }
    }
}
