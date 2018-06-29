﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Orleankka;
using Orleankka.Meta;

using Orleans.Placement;

namespace Demo
{
    [Serializable]
    public class CreateTopic : Command
    {
        public readonly string Query;
        public readonly IDictionary<ActorRef, TimeSpan> Schedule;

        public CreateTopic(string query, IDictionary<ActorRef, TimeSpan> schedule)
        {
            Query = query;
            Schedule = schedule;
        }
    }

    public interface ITopic : IActorGrain
    {}

    [ActivationCountBasedPlacement]
    public class Topic : DispatchActorGrain, ITopic
    {
        readonly ITopicStorage storage;

        const int MaxRetries = 3;
        static readonly TimeSpan RetryPeriod = TimeSpan.FromSeconds(5);
        readonly IDictionary<string, int> retrying = new Dictionary<string, int>();

        public int total;
        string query;

        public Topic(ITopicStorage storage, string id = null, IActorRuntime runtime = null) 
            : base(id, runtime)
        {
            this.storage = storage;
        }

        async Task On(Activate _) => 
            total = await storage.ReadTotalAsync(Id);

        public async Task Handle(CreateTopic cmd)
        {
            query = cmd.Query;

            foreach (var entry in cmd.Schedule)
                await Reminders.Register(entry.Key.Path.Id, TimeSpan.Zero, entry.Value);
        }

        async Task On(Reminder x)
        {
            try
            {
                if (!IsRetrying(x.Name))
                    await Search(x.Name);
            }
            catch (ApiUnavailableException)
            {
                ScheduleRetries(x.Name);
            }
        }

        bool IsRetrying(string api)
        {
            return retrying.ContainsKey(api);
        }

        public void ScheduleRetries(string api)
        {
            retrying.Add(api, 0);
            Timers.Register(api, RetryPeriod, RetryPeriod, api, RetrySearch);
        }

        public async Task RetrySearch(object state)
        {
            var api = (string)state;
            
            try
            {
                await Search(api);
                CancelRetries(api);
            }
            catch (ApiUnavailableException)
            {
                RecordFailedRetry(api);

                if (MaxRetriesReached(api))
                {
                    DisableSearch(api);
                    CancelRetries(api);                   
                }
            }
        }

        void RecordFailedRetry(string api)
        {
            Log.Message(ConsoleColor.DarkRed, "[{0}] failed to obtain results from {1} ...", Id, api);
            retrying[api] += 1;
        }

        bool MaxRetriesReached(string api)
        {
            return retrying[api] == MaxRetries;
        }

        void CancelRetries(string api)
        {
            Timers.Unregister(api);
            retrying.Remove(api);
        }

        async Task Search(string api)
        {
            var provider = System.ActorOf<IApi>(api);

            total += await provider.Ask(new Search(query));
            Log.Message(ConsoleColor.DarkGray, "[{0}] succesfully obtained results from {1} ...", Id, api);

            await storage.WriteTotalAsync(Id, total);
        }

        void DisableSearch(string api)
        {
            Reminders.Unregister(api);
        }
    }
}
