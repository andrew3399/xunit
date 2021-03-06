using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Xunit.Runner.Common
{
    /// <summary>
    /// An implementation of <see cref="IMessageSink" /> and <see cref="IMessageSinkWithTypes" /> that
    /// supports <see cref="AppVeyorReporter" />.
    /// </summary>
    public class AppVeyorReporterMessageHandler : DefaultRunnerReporterWithTypesMessageHandler
    {
        const int MaxLength = 4096;

        int assembliesInFlight;
        readonly ConcurrentDictionary<string, (string assemblyFileName, Dictionary<string, int> testMethods)> assemblyNames = new ConcurrentDictionary<string, (string, Dictionary<string, int>)>();
        readonly string baseUri;
        AppVeyorClient? client;
        readonly object clientLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="AppVeyorReporterMessageHandler" /> class.
        /// </summary>
        /// <param name="logger">The logger used to report messages</param>
        /// <param name="baseUri">The base AppVeyor API URI</param>
        public AppVeyorReporterMessageHandler(IRunnerLogger logger, string baseUri)
            : base(logger)
        {
            Guard.ArgumentNotNull(nameof(baseUri), baseUri);

            this.baseUri = baseUri.TrimEnd('/');

            Execution.TestAssemblyStartingEvent += HandleTestAssemblyStarting;
            Execution.TestStartingEvent += HandleTestStarting;
            Execution.TestAssemblyFinishedEvent += HandleTestAssemblyFinished;
        }

        AppVeyorClient Client
        {
            get
            {
                lock (clientLock)
                {
                    if (client == null)
                        client = new AppVeyorClient(Logger, baseUri);
                }

                return client;
            }
        }

        void HandleTestAssemblyFinished(MessageHandlerArgs<ITestAssemblyFinished> args)
        {
            lock (clientLock)
            {
                assembliesInFlight--;

                if (assembliesInFlight == 0)
                {
                    client?.Dispose(CancellationToken.None);
                    client = null;
                }
            }
        }

        void HandleTestAssemblyStarting(MessageHandlerArgs<ITestAssemblyStarting> args)
        {
            lock (clientLock)
            {
                assembliesInFlight++;

                // Look for the TFM attrib to disambiguate
                var attrib = args.Message.TestAssembly.Assembly.GetCustomAttributes("System.Runtime.Versioning.TargetFrameworkAttribute").FirstOrDefault();
                var assemblyFileName = Path.GetFileName(args.Message.TestAssembly.Assembly.AssemblyPath);
                if (attrib?.GetConstructorArguments().FirstOrDefault() is string arg)
                    assemblyFileName = $"{assemblyFileName} ({arg})";

                assemblyNames[args.Message.TestAssembly.Assembly.Name] = (assemblyFileName, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            }
        }

        void HandleTestStarting(MessageHandlerArgs<ITestStarting> args)
        {
            var testName = args.Message.Test.DisplayName;
            var testMethods = assemblyNames[args.Message.TestAssembly.Assembly.Name].testMethods;

            lock (testMethods)
                if (testMethods.ContainsKey(testName))
                    testName = $"{testName} {testMethods[testName]}";

            Client.AddTest(GetRequestMessage(
                testName,
                "xUnit",
                assemblyNames[args.Message.TestAssembly.Assembly.Name].assemblyFileName,
                "Running"
            ));
        }

        /// <inheritdoc />
        protected override void HandleTestPassed(MessageHandlerArgs<ITestPassed> args)
        {
            var testPassed = args.Message;
            var testMethods = assemblyNames[args.Message.TestAssembly.Assembly.Name].testMethods;

            Client.UpdateTest(GetRequestMessage(
                GetFinishedTestName(testPassed.Test.DisplayName, testMethods),
                "xUnit",
                assemblyNames[args.Message.TestAssembly.Assembly.Name].assemblyFileName,
                "Passed",
                Convert.ToInt64(testPassed.ExecutionTime * 1000),
                stdOut: testPassed.Output
            ));

            base.HandleTestPassed(args);
        }

        /// <inheritdoc />
        protected override void HandleTestSkipped(MessageHandlerArgs<ITestSkipped> args)
        {
            var testSkipped = args.Message;
            var testMethods = assemblyNames[args.Message.TestAssembly.Assembly.Name].testMethods;

            Client.UpdateTest(GetRequestMessage(
                GetFinishedTestName(testSkipped.Test.DisplayName, testMethods),
                "xUnit",
                assemblyNames[args.Message.TestAssembly.Assembly.Name].assemblyFileName,
                "Skipped",
                Convert.ToInt64(testSkipped.ExecutionTime * 1000)
            ));

            base.HandleTestSkipped(args);
        }

        /// <inheritdoc />
        protected override void HandleTestFailed(MessageHandlerArgs<ITestFailed> args)
        {
            var testFailed = args.Message;
            var testMethods = assemblyNames[args.Message.TestAssembly.Assembly.Name].testMethods;

            Client.UpdateTest(GetRequestMessage(
                GetFinishedTestName(testFailed.Test.DisplayName, testMethods),
                "xUnit",
                assemblyNames[args.Message.TestAssembly.Assembly.Name].assemblyFileName,
                "Failed",
                Convert.ToInt64(testFailed.ExecutionTime * 1000),
                ExceptionUtility.CombineMessages(testFailed),
                ExceptionUtility.CombineStackTraces(testFailed),
                testFailed.Output
            ));

            base.HandleTestFailed(args);
        }

        // AppVeyor API helpers

        static string GetFinishedTestName(string methodName, Dictionary<string, int> testMethods)
        {
            lock (testMethods)
            {
                var testName = methodName;
                var number = 0;

                if (testMethods.ContainsKey(methodName))
                {
                    number = testMethods[methodName];
                    testName = $"{methodName} {number}";
                }

                testMethods[methodName] = number + 1;
                return testName;
            }
        }

        Dictionary<string, object?> GetRequestMessage(
            string testName,
            string testFramework,
            string fileName,
            string outcome,
            long? durationMilliseconds = null,
            string? errorMessage = null,
            string? errorStackTrace = null,
            string? stdOut = null)
        {
            return new Dictionary<string, object?>
            {
                { "testName", testName },
                { "testFramework", testFramework },
                { "fileName", fileName },
                { "outcome", outcome },
                { "durationMilliseconds", durationMilliseconds },
                { "ErrorMessage", errorMessage },
                { "ErrorStackTrace", errorStackTrace },
                { "StdOut", stdOut?.Length > MaxLength ? stdOut.Substring(0, MaxLength) : stdOut },
            };
        }
    }
}
