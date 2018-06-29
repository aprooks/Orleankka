﻿using NUnit.Framework;

namespace Orleankka.Checks
{
    [TestFixture]
    public class StreamRefFixture
    {
        [Test]
        public void Equatable_by_path()
        {
            var path = StreamPath.From("sms", "42");

            var ref1 = new StreamRef(path, null);
            var ref2 = new StreamRef(path, null);
            
            Assert.True(ref1 == ref2);
            Assert.True(ref1.Equals(ref2));
        }
    }
}
