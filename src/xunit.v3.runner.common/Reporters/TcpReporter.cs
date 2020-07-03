using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Xunit.Runner.Common
{
    /// <summary/>
    public class TcpReporter : IRunnerReporter
    {
        /// <summary/>
        public string Description => "Emit messages to TCP port for meta-runners";

        /// <summary/>
        public bool IsEnvironmentallyEnabled => false;

        /// <summary/>
        public string? RunnerSwitch => "tcp";

        /// <summary/>
        public IMessageSink CreateMessageHandler(IRunnerLogger logger) =>
            new TcpReporterMessageHandler(logger);
    }
}
