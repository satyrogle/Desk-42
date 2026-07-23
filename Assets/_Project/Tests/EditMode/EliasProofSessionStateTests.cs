using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Tests.EditMode
{
    public sealed class EliasProofSessionStateTests
    {
        private GameObject _host;
        private EliasProofSessionController _controller;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("EliasProofSessionTests");
            _controller = _host.AddComponent<EliasProofSessionController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void BeginProofSession_CreatesCanonicalCleanState()
        {
            EliasProofSessionState state =
                _controller.BeginProofSession("proof-session-test");

            Assert.IsTrue(state.IsActive);
            Assert.AreEqual("proof-session-test", state.ProofSessionId);
            Assert.AreEqual(EliasShift1Disposition.None,
                state.Shift1Disposition);
            Assert.AreEqual(EliasShift2Branch.None, state.Shift2Branch);
            Assert.AreEqual(ClaimResolutionKind.Unspecified,
                state.Shift2FinalDisposition);
            Assert.IsEmpty(state.RecordedAppearanceKeys);
            Assert.IsNotNull(state.ActiveAftermathModifier);
            Assert.IsFalse(state.ActiveAftermathModifier.IsActive);
        }

        [Test]
        public void BeginAndEnd_AreExplicitResetBoundaries()
        {
            EliasProofSessionState first =
                _controller.BeginProofSession("first");
            EliasProofSessionState second =
                _controller.BeginProofSession("second");

            Assert.AreNotSame(first, second);
            Assert.AreEqual("second", second.ProofSessionId);
            Assert.IsTrue(_controller.HasActiveSession);

            _controller.EndProofSession();

            Assert.IsFalse(_controller.HasActiveSession);
            Assert.IsFalse(_controller.State.IsActive);
            Assert.IsNull(_controller.State.ProofSessionId);
            Assert.IsEmpty(_controller.State.RecordedAppearanceKeys);
        }

        [Test]
        public void State_IsSerializableButNotOwnedByRunOrMetaSchemas()
        {
            Assert.IsTrue(Attribute.IsDefined(
                typeof(EliasProofSessionState), typeof(SerializableAttribute)));

            AssertSchemaDoesNotOwnProofState(typeof(RunData));
            AssertSchemaDoesNotOwnProofState(typeof(MetaProgressData));
        }

        private static void AssertSchemaDoesNotOwnProofState(Type schema)
        {
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public | BindingFlags.NonPublic;
            bool ownsProofState = schema.GetFields(flags)
                    .Any(field => field.FieldType
                        == typeof(EliasProofSessionState))
                || schema.GetProperties(flags)
                    .Any(property => property.PropertyType
                        == typeof(EliasProofSessionState));
            Assert.IsFalse(ownsProofState,
                $"{schema.Name} must not own Elias proof-session state.");
        }
    }
}
