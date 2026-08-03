using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal sealed class EndogenousDocketPulse
    {
        internal List<IncidentCandidate> DetectedIncidents = new();
        internal List<DocketObservation> ProjectedObservations = new();
        internal List<DocketCandidate> ComposedDocketCandidates = new();
        internal EndogenousInstitutionalCase AdmittedCase;
    }

    internal static class EndogenousIncidentDocketPipeline
    {
        internal static EndogenousDocketPulse Process(
            InstitutionalMaterialWorld world,
            SocietyState society,
            EndogenousDocketState state,
            bool admitOneCase = true)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            var pulse = new EndogenousDocketPulse
            {
                DetectedIncidents = EndogenousIncidentDetector.Detect(
                    world, society, state),
                ProjectedObservations = EndogenousObservationProjector.Project(
                    world, society, state),
                ComposedDocketCandidates = EndogenousDocketService.Compose(
                    society, state),
            };
            if (admitOneCase)
                pulse.AdmittedCase = EndogenousDocketService.AdmitNext(society, state);
            return pulse;
        }
    }
}
