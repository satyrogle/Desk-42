using System;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeAudioMixAuditReport
    {
        public bool PrimaryActionsReadable { get; internal set; }
        public bool AutomationReadableInRush { get; internal set; }
        public bool RecoveryReadableInBreak { get; internal set; }
        public bool CustomerWarningsProtected { get; internal set; }
        public bool ComfortableDefaults { get; internal set; }
        public bool NominalHeadroom { get; internal set; }

        public bool Passed => PrimaryActionsReadable && AutomationReadableInRush &&
            RecoveryReadableInBreak && CustomerWarningsProtected &&
            ComfortableDefaults && NominalHeadroom;
    }

    /// <summary>Deterministic static checks for the authored default mix.</summary>
    public static class OfficeAudioMixAudit
    {
        public static OfficeAudioMixAuditReport Evaluate(
            OfficeAudioCueCatalog catalog,
            OfficeAudioSettings settings)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            float calmBed = Effective(catalog, settings, "ambience.calm");
            float rushBed = Effective(catalog, settings, "ambience.rush") +
                Effective(catalog, settings, "music.pressure");
            float breakBed = Effective(catalog, settings, "music.break");
            float primary = Minimum(catalog, settings,
                "folder.take", "folder.send", "paper.correct", "money.correct",
                "calm.complete", "fix.complete");
            float automation = Minimum(catalog, settings,
                "automation.match", "automation.reject",
                "automation.copied-accepted");
            float recovery = Minimum(catalog, settings,
                "event.copier-stop", "event.copy-clear",
                "event.recovery-complete");
            float warning = Effective(catalog, settings, "customer.worried");
            float routineMaximum = Maximum(catalog, settings,
                "folder.take", "folder.send", "action.interact",
                "paper.selection", "trace.movement");
            float authoredPeak = 0f;
            for (int i = 0; i < catalog.Manifest.cues.Length; i++)
            {
                OfficeAudioCueRecord cue = catalog.Manifest.cues[i];
                if (cue == null) continue;
                authoredPeak = Math.Max(authoredPeak,
                    cue.base_volume * settings.BusGain(cue.bus));
            }

            return new OfficeAudioMixAuditReport
            {
                PrimaryActionsReadable = primary >= calmBed * 1.35f,
                AutomationReadableInRush = automation >= rushBed * 1.35f,
                RecoveryReadableInBreak = recovery >= breakBed * 2f,
                CustomerWarningsProtected = routineMaximum <= warning * 1.4f,
                ComfortableDefaults = settings.Master <= 0.85f &&
                    settings.Music <= 0.6f && settings.Sfx <= 0.85f &&
                    settings.Ambience <= 0.65f,
                NominalHeadroom = authoredPeak <= 0.7f,
            };
        }

        private static float Effective(
            OfficeAudioCueCatalog catalog,
            OfficeAudioSettings settings,
            string cueId)
        {
            for (int i = 0; i < catalog.Manifest.cues.Length; i++)
            {
                OfficeAudioCueRecord cue = catalog.Manifest.cues[i];
                if (cue != null && string.Equals(cue.cue_id, cueId,
                        StringComparison.Ordinal))
                    return cue.base_volume * settings.BusGain(cue.bus);
            }
            return 0f;
        }

        private static float Minimum(
            OfficeAudioCueCatalog catalog,
            OfficeAudioSettings settings,
            params string[] cueIds)
        {
            float value = float.MaxValue;
            for (int i = 0; i < cueIds.Length; i++)
                value = Math.Min(value, Effective(catalog, settings, cueIds[i]));
            return value == float.MaxValue ? 0f : value;
        }

        private static float Maximum(
            OfficeAudioCueCatalog catalog,
            OfficeAudioSettings settings,
            params string[] cueIds)
        {
            float value = 0f;
            for (int i = 0; i < cueIds.Length; i++)
                value = Math.Max(value, Effective(catalog, settings, cueIds[i]));
            return value;
        }
    }
}
