using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedGrin.Messaging
{
    /// <summary>
    /// The types of message the network is capable of sending.
    /// </summary>
    internal enum NetworkMessageType
    {
        Create,
        Update,
        Destroy,
        Reckoning,
    }
}
