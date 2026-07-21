using Desk42.Core;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class ServiceRegistryTests
    {
        private sealed class TestService { }

        [Test]
        public void StaleInstance_CannotUnregisterReplacement()
        {
            var oldInstance = new TestService();
            var replacement = new TestService();

            Desk42Services.Register(oldInstance);
            Desk42Services.Register(replacement);
            Desk42Services.Unregister(oldInstance);

            Assert.AreSame(replacement, Desk42Services.Get<TestService>());
            Desk42Services.Unregister(replacement);
        }
    }
}
