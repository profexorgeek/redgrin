using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin
{
    public class NetworkConfiguration
    {
        /// <summary>
        /// The name of the application employing the NetworkManager
        /// </summary>
        public string ApplicationName {get;set;}

        /// <summary>
        /// The port the application will attempt to connect on
        /// </summary>
        public int ApplicationPort { get; set; }

        /// <summary>
        /// A list of state types with a numerical index.
        /// Using a list allows network to pass a very small
        /// integer value describing message payload instead of
        /// a fully-qualified type string.
        /// 
        /// NOTE: Every EntityState that will be transferred through the network
        /// must be enumerated in the config.
        /// </summary>
        public List<Type> EntityStateTypes { get; set; }

#if DEBUG
        /// <summary>
        /// Simulated percentage of lost packets (0f - 1.0f)
        /// </summary>
        public float SimulatedLoss { get; set; }

        /// <summary>
        /// Simulated one-way latency for sent packets in seconds.
        /// </summary>
        public float SimulatedMinimumLatencySeconds { get; set; }

        /// <summary>
        /// Additional random latency in seconds.
        /// </summary>
        public float SimulatedRandomLatencySeconds { get; set; }

        /// <summary>
        /// Simulated percentage of duplicate packets (0f - 1.0f)
        /// </summary>
        public float SimulatedDuplicateChance { get; set; }
#endif

    }
}
