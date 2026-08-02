using System;
using System.IO;
using System.Text;
using Desk42.Institutional;
using Newtonsoft.Json;
using UnityEngine;

namespace Desk42.Institutional.Runtime
{
    /// <summary>
    /// Persists the institutional society independently from the legacy card-run save.
    /// Keeping this boundary separate lets the new simulation evolve without making an
    /// unfinished migration part of RunData's established contract.
    /// </summary>
    public sealed class InstitutionalSocietyStore
    {
        public const int CurrentSaveVersion = 1;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
        };

        private readonly string _primaryPath;
        private readonly string _backupPath;

        public InstitutionalSocietyStore(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("A save directory is required.", nameof(directoryPath));

            _primaryPath = Path.Combine(directoryPath, "society.json");
            _backupPath = Path.Combine(directoryPath, "society.json.bak");
        }

        public static InstitutionalSocietyStore CreateDefault()
            => new(Application.persistentDataPath);

        public bool Save(SocietyState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            try
            {
                SocietyStateValidator.Validate(state);
                string directory = Path.GetDirectoryName(_primaryPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var envelope = new SocietySaveEnvelope
                {
                    SaveVersion = CurrentSaveVersion,
                    SavedAtUtc = DateTime.UtcNow.ToString("o"),
                    Society = state,
                };

                string json = JsonConvert.SerializeObject(envelope, JsonSettings);
                string temporaryPath = _primaryPath + ".tmp";

                if (File.Exists(_primaryPath))
                    File.Copy(_primaryPath, _backupPath, overwrite: true);

                File.WriteAllText(temporaryPath, json, Encoding.UTF8);
                if (File.Exists(_primaryPath)) File.Delete(_primaryPath);
                File.Move(temporaryPath, _primaryPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[InstitutionalSocietyStore] Save failed: {exception.Message}");
                return false;
            }
        }

        public SocietyState Load()
        {
            SocietyState primary = TryLoad(_primaryPath);
            if (primary != null) return primary;

            if (!File.Exists(_backupPath)) return null;

            Debug.LogWarning("[InstitutionalSocietyStore] Primary society save was unavailable; loading its backup.");
            return TryLoad(_backupPath);
        }

        private static SocietyState TryLoad(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                SocietySaveEnvelope envelope =
                    JsonConvert.DeserializeObject<SocietySaveEnvelope>(json, JsonSettings);

                if (envelope == null || envelope.Society == null)
                    return null;

                if (envelope.SaveVersion != CurrentSaveVersion)
                {
                    Debug.LogError(
                        $"[InstitutionalSocietyStore] Save version {envelope.SaveVersion} " +
                        $"is not supported by version {CurrentSaveVersion}; no migration exists yet.");
                    return null;
                }

                SocietyStateValidator.Validate(envelope.Society);
                return envelope.Society;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[InstitutionalSocietyStore] Load failed for {path}: {exception.Message}");
                return null;
            }
        }

        [Serializable]
        private sealed class SocietySaveEnvelope
        {
            public int SaveVersion = CurrentSaveVersion;
            public string SavedAtUtc;
            public SocietyState Society;
        }
    }
}
