using NUnit.Framework;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.Messages.SecureChannel
{
    [TestFixture]
    [Category("Unit")]
    public class SecureContextTest
    {
        [Test]
        public void IsDefaultKeyProperlySet()
        {
            var defaultKey = "0123456789:;<=>?"u8.ToArray();
            var nonDefaultKey = "0123-Bob-9:;<=>?"u8.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(new SecurityContext().IsUsingDefaultKey, Is.True, "default constructor");
                Assert.That(new SecurityContext(SecurityContext.DefaultKey).IsUsingDefaultKey, Is.True, "with static def key");
                Assert.That(new SecurityContext(defaultKey).IsUsingDefaultKey, Is.True, "with local def key");
                Assert.That(new SecurityContext(nonDefaultKey).IsUsingDefaultKey, Is.False, "non-def key");
            });
        }
    }
}
