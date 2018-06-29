﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Orleankka;
using Orleankka.Meta;

using Orleans.Concurrency;

namespace Example
{
    [Immutable] public class GetSelf : Query<ActorRef> {}
    [Immutable] public class GetStream : Query<StreamRef> {}
    [Immutable] public class GetReceived : Query<Item[]> {}

    public class Item : Result
    {
        public string Name;
        public int Price;
        public DateTimeOffset Paid;
    }

    public interface ISomeActor : IActorGrain
    {}

    public class SomeActor : DispatchActorGrain, ISomeActor
    {
        readonly List<Item> received = new List<Item>();
        StreamRef stream;

        async Task On(Activate _)
        {
            stream = System.StreamOf("sms", "test");
            await stream.Subscribe(this);
        }

        void On(Item item) => received.Add(item);
        Item[] On(GetReceived _) => received.ToArray();

        ActorRef On(GetSelf _) => Self;
        StreamRef On(GetStream _) => stream;
    }
}