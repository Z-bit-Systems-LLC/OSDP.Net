﻿using NUnit.Framework;
using OSDP.Net.Messages.SecureChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSDP.Net.Tests.Messages.SecureChannel
{
    [TestFixture]
    public class SecureContextTest
    {
        [Test]
        public void IsDefaultKeyProperlySet()
        {
            var defaultKey = "0123456789:;<=>?"u8.ToArray();
            var nonDefaultKey = "0123-Bob-9:;<=>?"u8.ToArray();

            Assert.Multiple(() =>
            {
                Assert.True(new SecurityContext().IsUsingDefaultKey, "default constructor");
                Assert.True(new SecurityContext(SecurityContext.DefaultKey).IsUsingDefaultKey, "with static def key");
                Assert.True(new SecurityContext(defaultKey).IsUsingDefaultKey, "with local def key");
                Assert.False(new SecurityContext(nonDefaultKey).IsUsingDefaultKey, "non-def key");
            });
        }
    }
}
