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

        [Test]
        public void UpdateSecurityKey_SwapsKeyWithoutTearingDownSession()
        {
            var nonDefaultKey = "0123-Bob-9:;<=>?"u8.ToArray();
            var context = new SecurityContext(SecurityContext.DefaultKey)
            {
                IsSecurityEstablished = true,
                Enc = new byte[16],
                RMac = new byte[16]
            };

            // Re-key (as happens when a KEYSET is observed on an established channel)
            context.UpdateSecurityKey(nonDefaultKey);

            Assert.Multiple(() =>
            {
                Assert.That(context.IsUsingDefaultKey, Is.False, "IsUsingDefaultKey reflects the new key");
                using var cypher = context.CreateCypher(true);
                Assert.That(cypher.Key, Is.EqualTo(nonDefaultKey), "session key derivation uses the new key");
                Assert.That(context.IsSecurityEstablished, Is.True, "existing session is not torn down");
            });
        }

        [Test]
        public void UpdateSecurityKey_NullKeyIsNoOp()
        {
            var context = new SecurityContext(SecurityContext.DefaultKey);

            context.UpdateSecurityKey(null);

            using var cypher = context.CreateCypher(true);
            Assert.Multiple(() =>
            {
                Assert.That(context.IsUsingDefaultKey, Is.True);
                Assert.That(cypher.Key, Is.EqualTo(SecurityContext.DefaultKey));
            });
        }
    }
}
