﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Orleankka.Utility;

namespace Orleankka
{
    public static class StreamRefExtensions
    {
        public static async Task Subscribe(this StreamRef stream, ActorGrain actor, StreamFilter filter = null)
        {
            Requires.NotNull(actor, nameof(actor));

            var subscriptions = await stream.Subscriptions();
            if (subscriptions.Count == 1)
                return;

            Debug.Assert(subscriptions.Count == 0,
                "We should keep only one active subscription per-stream per-actor");

            await stream.Subscribe(actor.ReceiveRequest, filter);
        }

        public static async Task Unsubscribe(this StreamRef stream, ActorGrain actor)
        {
            Requires.NotNull(actor, nameof(actor));

            var subscriptions = await stream.Subscriptions();
            if (subscriptions.Count == 0)
                return;

            Debug.Assert(subscriptions.Count == 1,
                "We should keep only one active subscription per-stream per-actor");

            await subscriptions[0].Unsubscribe();
        }

        public static async Task Resume(this StreamRef stream, ActorGrain actor)
        {
            Requires.NotNull(actor, nameof(actor));

            var subscriptions = await stream.Subscriptions();
            if (subscriptions.Count == 0)
                return;

            Debug.Assert(subscriptions.Count == 1,
                "We should keep only one active subscription per-stream per-actor");

            await subscriptions[0].Resume(actor.ReceiveRequest);
        }
    }
}