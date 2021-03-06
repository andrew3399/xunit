﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

#if XUNIT_FRAMEWORK
public class SpyMessageSink<TFinalMessage> : IMessageSink
#else
public class SpyMessageSink<TFinalMessage> : LongLivedMarshalByRefObject, IMessageSink
#endif
{
    readonly Func<IMessageSinkMessage, bool> cancellationThunk;

    public SpyMessageSink(Func<IMessageSinkMessage, bool>? cancellationThunk = null)
    {
        this.cancellationThunk = cancellationThunk ?? (msg => true);
    }

    public ManualResetEvent Finished = new ManualResetEvent(initialState: false);

    public List<IMessageSinkMessage> Messages = new List<IMessageSinkMessage>();

    /// <inheritdoc/>
    public void Dispose()
    {
        Finished.Dispose();
    }

    public bool OnMessage(IMessageSinkMessage message)
    {
        Messages.Add(message);

        if (message is TFinalMessage)
            Finished.Set();

        return cancellationThunk(message);
    }
}
