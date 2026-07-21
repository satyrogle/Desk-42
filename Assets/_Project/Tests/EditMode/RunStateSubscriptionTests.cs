using System.Reflection;
using Desk42.Core;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    public sealed class RunStateSubscriptionTests
    {
        [Test]
        public void ResumeRunRepeatedly_DoesNotStackRumorMillSubscriptions()
        {
            var go = new GameObject("RunState_Subscription_Test");
            var run = go.AddComponent<RunStateController>();
            var data = new RunData
            {
                MasterSeed = 421001,
                ArchetypeId = "auditor",
                ShiftNumber = 1,
                Sanity = 100f,
                SoulIntegrity = 100f,
            };

            try
            {
                run.ResumeRun(data, new MetaProgressData());
                run.ResumeRun(data, new MetaProgressData());

                RumorMill.PublishDeferred(new OfficeHazardEvent(
                    OfficeHazardType.PrinterJam, 0f, false));
                typeof(RumorMill)
                    .GetMethod("DrainQueue", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);

                Assert.AreEqual(95f, data.Sanity,
                    "The resumed controller must handle the hazard exactly once.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                RumorMill.FlushQueue();
            }
        }
    }
}
