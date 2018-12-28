﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin.Logging
{
    /// <summary>
    /// A basic logging interface the library
    /// uses to log messages at different levels.
    /// </summary>
    public interface ILogger
    {
        LogLevels Level {get;set;}
        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }
}
