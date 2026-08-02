using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// Explicit outcome of requesting an official recognition state. A no-op is a
    /// successful result with Changed=false and no fabricated mutation record.
    /// </summary>
    [Serializable]
    public sealed class StatusMutationResult
    {
        public bool Changed;
        public bool CurrentRecognisedState;
        public OfficialStatusMutation RecordedMutation;
    }
}
