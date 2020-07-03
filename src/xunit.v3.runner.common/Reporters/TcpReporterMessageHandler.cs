using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.Runner.Common
{
    /// <summary/>
    public class TcpReporterMessageHandler : LongLivedMarshalByRefObject, IMessageSinkWithTypes, IMessageSink
    {
        readonly IRunnerLogger logger;

        /// <summary/>
        public TcpReporterMessageHandler(IRunnerLogger logger)
        {
            this.logger = Guard.ArgumentNotNull(nameof(logger), logger);
        }

        void MapTestResult(ITestResultMessage testResultMessage, Dictionary<string, object> data)
        {
            data["executionTime"] = testResultMessage.ExecutionTime;
            data["output"] = testResultMessage.Output;
        }

        void MapTestPassed(ITestPassed testPassed, Dictionary<string, object> data)
        {
            data["type"] = "TestPassed";
            data["state"] = "passed";

            //return new
            //{
            //    testPassed.ExecutionTime,
            //    testPassed.Output,
            //    testPassed.Test.DisplayName,
            //    Method = testPassed.TestMethod.Method.Name,
            //    Class = testPassed.TestClass.Class.Name,
            //    Assembly = testPassed.TestAssembly.Assembly.AssemblyPath,
            //    ConfigFile = testPassed.TestAssembly.ConfigFileName,
            //    TestCollection = testPassed.TestCollection.DisplayName
            //};
        }

        ///// <summary/>
        //protected override void HandleTestPassed(MessageHandlerArgs<ITestPassed> args)
        //{
        //    var msg = new
        //    {
        //        args.Message.ExecutionTime,
        //        args.Message.Output,
        //        args.Message.Test.DisplayName,
        //        Method = args.Message.TestMethod.Method.Name,
        //        Class = args.Message.TestClass.Class.Name,
        //        Assembly = args.Message.TestAssembly.Assembly.Name,
        //        TestCollection = args.Message.TestCollection.DisplayName
        //    };

        //    var text = JsonSerializer.Serialize(msg);
        //    Console.WriteLine(text);

        //    base.HandleTestPassed(args);
        //}

        void Dispatch<TMessage>(IMessageSinkMessage message, HashSet<string>? messageTypes, Dictionary<string, object> data, Action<TMessage, Dictionary<string, object>> converter)
            where TMessage : class, IMessageSinkMessage
        {
            var castMessage = message.Cast<TMessage>(messageTypes);
            if (castMessage != null)
                converter(castMessage, data);
        }

        /// <summary/>
        public bool OnMessageWithTypes(IMessageSinkMessage message, HashSet<string>? messageTypes)
        {
            var data = new Dictionary<string, object>();

            Dispatch<ITestResultMessage>(message, messageTypes, data, MapTestResult);
            Dispatch<ITestPassed>(message, messageTypes, data, MapTestPassed);

            if (data.Count > 0)
            {
                var text = JsonSerializer.Serialize(data);
                Console.WriteLine(text);
            }

            return true;
        }

        /// <summary/>
        public void Dispose()
        {

        }

        /// <summary/>
        public bool OnMessage(IMessageSinkMessage message)
        {
            Guard.ArgumentNotNull(nameof(message), message);

            return OnMessageWithTypes(message, MessageSinkAdapter.GetImplementedInterfaces(message));
        }
    }
}
