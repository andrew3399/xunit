﻿using Xunit.Abstractions;

namespace Xunit.Runner.Common
{
    /// <summary>
    /// The default implementation of <see cref="IRunnerReporter"/>, used
    /// by runners when there is no other overridden reporter. It returns
    /// an instance of <see cref="DefaultRunnerReporterWithTypesMessageHandler"/>.
    /// </summary>
    public class DefaultRunnerReporterWithTypes : IRunnerReporter
    {
        /// <inheritdoc/>
        public virtual string Description => "Default runner report (high performance)";

        /// <inheritdoc/>
        public virtual bool IsEnvironmentallyEnabled => false;

        /// <inheritdoc/>
        public virtual string? RunnerSwitch => null;

        /// <inheritdoc/>
        public virtual IMessageSink CreateMessageHandler(IRunnerLogger logger) =>
            new DefaultRunnerReporterWithTypesMessageHandler(logger);
    }
}
