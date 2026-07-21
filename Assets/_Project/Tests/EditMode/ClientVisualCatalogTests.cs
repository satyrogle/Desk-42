using Desk42.Core;
using Desk42.UI;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class ClientVisualCatalogTests
    {
        private ClientVisualCatalog _catalog;
        private Texture2D _texture;
        private Sprite _neutral;
        private Sprite _agitated;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(2, 2);
            _neutral = Sprite.Create(_texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            _agitated = Sprite.Create(_texture, new Rect(1, 0, 1, 1), Vector2.one * 0.5f);
            _catalog = ScriptableObject.CreateInstance<ClientVisualCatalog>();
            _catalog.Profiles = new[]
            {
                new ClientVisualCatalog.Profile
                {
                    SpeciesIds = new[] { "moth_accountant", "human_standard" },
                    Pending = _neutral,
                    Agitated = _agitated
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_neutral);
            Object.DestroyImmediate(_agitated);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void ResolveSprite_UsesStateSpecificFrame()
        {
            Assert.AreSame(
                _agitated,
                _catalog.ResolveSprite("moth_accountant", ClientStateID.Agitated));
        }

        [Test]
        public void ResolveSprite_AcceptsLegacyAlias()
        {
            Assert.AreSame(
                _neutral,
                _catalog.ResolveSprite("human_standard", ClientStateID.Pending));
        }

        [Test]
        public void ResolveSprite_MissingStateFallsBackToPending()
        {
            Assert.AreSame(
                _neutral,
                _catalog.ResolveSprite("moth_accountant", ClientStateID.Paranoid));
        }
    }
}
