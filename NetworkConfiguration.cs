using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlatRedNetwork
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
        /// Used to pass type along the wire as a minimum size.
        /// List is used instead of enum for implementation flexibility
        /// </summary>
        public List<Type> EntityStateTypes { get; set; }

#if DEBUG
        /// <summary>
        /// Simulated percentage of lost packets (0f - 1.0f)
        /// </summary>
        public float SimulatedLoss { get; set; }

        /// <summary>
        /// Simulated one-way latency for sent packets
        /// </summary>
        public float SimulatedMinimumLatency { get; set; }

        /// <summary>
        /// Additional random latency in seconds
        /// </summary>
        public float SimulatedRandomLatencySeconds { get; set; }

        /// <summary>
        /// Simulated percentage of duplicate packets (0f - 1.0f)
        /// </summary>
        public float SimulatedDuplicateChance { get; set; }
#endif

    }
}
