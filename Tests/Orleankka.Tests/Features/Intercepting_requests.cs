﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using NUnit.Framework;
using Orleans.Runtime;

namespace Orleankka.Features
{
    namespace Intercepting_requests
    {
        using Meta;
        using Testing;

        [Serializable]
        public class SetText : Command
        {
            public string Text;
        }

        [Serializable]
        public class GetText : Query<string>
        {}

        [Serializable]
        public class CheckRef : Query<string>
        {}

        [Serializable]
        public class StreamItem : Event
        {
            public string Text;
        }

        public interface ITestActor : IActorGrain
        {}

        public abstract class TestActorBase : DispatchActorGrain
        {}

        /// middleware is set for the base actor type
        public class TestActor : TestActorBase, ITestActor
        {
            string text = "";

            void On(SetText cmd) => text = cmd.Text;
            string On(GetText q) => text;

            string On(CheckRef cmd) => (string) RequestContext.Get("SetByActorRefMiddleware");

            readonly List<string> fromStream = new List<string>();
            List<string> On(GetReceivedFromStream x) => fromStream;

            Task On(Subscribe x) => x.Stream.Subscribe(this);
            void On(StreamItem x) => fromStream.Add(x.Text);
        }

        [Serializable]
        public class DoTell : Command
        {
            public ActorRef Target;
            public object Message;
        }

        [Serializable]
        public class DoAsk : Query<string>
        {
            public ActorRef Target;
            public object Message;
        }

        public interface ITestInsideActor : IActorGrain
        {}

        public class TestInsideActor : DispatchActorGrain, ITestInsideActor
        {
            public async Task Handle(DoTell cmd) => await cmd.Target.Tell(cmd.Message);
            public Task<string> Handle(DoAsk query) => query.Target.Ask<string>(query.Message);
        }

        [Serializable]
        public class Subscribe : Command
        {
            public StreamRef Stream;
        }

        [Serializable]
        public class GetReceivedFromStream : Query<List<string>>
        {}

        public class TestActorMiddleware : ActorMiddleware
        {
            public override Task<object> Receive(ActorGrain actor, object message, Func<object, Task<object>> receiver)
            {
                switch (message)
                {
                    case SetText msg:
                        
                        if (msg.Text == "interrupt")
                            throw new InvalidOperationException();
                        
                        msg.Text += ".intercepted";
                        break;

                    case StreamItem item:
                        item.Text += ".intercepted";
                        break;
                }

                return Next.Receive(actor, message, receiver);
            }
        }

        public class TestActorRefMiddleware : ActorRefMiddleware
        {
            public override Task<TResult> Send<TResult>(ActorPath actor, object message, Func<object, Task<object>> sender)
            {
                if (message is CheckRef)
                    RequestContext.Set("SetByActorRefMiddleware", "it works!");

                return base.Send<TResult>(actor, message, sender);
            }
        }

        [TestFixture]
        [RequiresSilo]
        public class Tests
        {
            IActorSystem system;

            [SetUp]
            public void SetUp()
            {
                system = TestActorSystem.Instance;
            }

            [Test]
            public async Task Client_to_actor()
            {
                var actor = system.FreshActorOf<ITestActor>();

                await actor.Tell(new SetText {Text = "c-a"});
                Assert.AreEqual("c-a.intercepted", await actor.Ask(new GetText()));
            }

            [Test]
            public async Task Actor_to_actor()
            {
                var one = system.FreshActorOf<ITestInsideActor>();
                var another = system.FreshActorOf<ITestActor>();

                await one.Tell(new DoTell {Target = another, Message = new SetText {Text = "a-a"}});
                Assert.AreEqual("a-a.intercepted", await one.Ask(new DoAsk {Target = another, Message = new GetText()}));
            }

            [Test]
            public async Task Interrupting_requests()
            {
                var actor = system.FreshActorOf<ITestActor>();

                Assert.ThrowsAsync<InvalidOperationException>(async ()=> await 
                    actor.Tell(new SetText { Text = "interrupt" }));

                Assert.AreEqual("", await actor.Ask(new GetText()));
            }

            [Test]
            public async Task Intercepting_stream_messages()
            {
                var stream = system.StreamOf("sms", "test-stream-interception");
                
                var actor = system.FreshActorOf<ITestActor>();
                await actor.Tell(new Subscribe {Stream = stream});

                await stream.Push(new StreamItem {Text = "foo"});
                await Task.Delay(TimeSpan.FromMilliseconds(10));

                var received = await actor.Ask(new GetReceivedFromStream());
                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0], Is.EqualTo("foo.intercepted"));
            }

            [Test]
            public async Task Intercepting_actor_ref()
            {
                var actor = system.FreshActorOf<ITestActor>();
                var result = await actor.Ask<string>(new CheckRef());
                Assert.That(result, Is.EqualTo("it works!"));
            }
        }
    }
}