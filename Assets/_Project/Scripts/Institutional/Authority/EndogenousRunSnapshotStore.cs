using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Desk42.Institutional
{
    internal static class EndogenousRunSnapshotStore
    {
        private const int EnvelopeVersion = 1;
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly JsonSerializerSettings Settings = CreateSettings();

        internal static void Save(string path, EndogenousRunSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A snapshot path is required.", nameof(path));
            EndogenousRunSnapshotValidator.Validate(snapshot);
            string payload = SerializePayload(snapshot);
            var envelope = new SnapshotEnvelope
            {
                EnvelopeVersion = EnvelopeVersion,
                PayloadSha256 = Sha256(payload),
                Snapshot = snapshot,
            };
            string json = JsonConvert.SerializeObject(envelope, Settings);
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Snapshot path has no parent directory.");
            Directory.CreateDirectory(directory);
            string temporaryPath = fullPath + ".tmp";
            string backupPath = fullPath + ".bak";

            WriteDurably(temporaryPath, json);
            if (!File.Exists(fullPath))
            {
                File.Move(temporaryPath, fullPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, fullPath, backupPath, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                RecoverableReplace(temporaryPath, fullPath, backupPath);
            }
        }

        internal static EndogenousRunSnapshot Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A snapshot path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            try
            {
                return LoadExact(fullPath);
            }
            catch (Exception primaryError)
            {
                string backupPath = fullPath + ".bak";
                if (!File.Exists(backupPath)) throw;
                try
                {
                    return LoadExact(backupPath);
                }
                catch (Exception backupError)
                {
                    throw new AggregateException(
                        "Primary and backup endogenous snapshots are invalid.",
                        primaryError,
                        backupError);
                }
            }
        }

        internal static string SerializePayload(EndogenousRunSnapshot snapshot)
        {
            EndogenousRunSnapshotValidator.Validate(snapshot);
            return JsonConvert.SerializeObject(snapshot, Settings);
        }

        private static EndogenousRunSnapshot LoadExact(string path)
        {
            string json = File.ReadAllText(path, Utf8WithoutBom);
            SnapshotEnvelope envelope = JsonConvert.DeserializeObject<SnapshotEnvelope>(
                json, Settings);
            if (envelope == null || envelope.EnvelopeVersion != EnvelopeVersion ||
                envelope.Snapshot == null || string.IsNullOrWhiteSpace(envelope.PayloadSha256))
            {
                throw new InvalidDataException("Snapshot envelope is incomplete or unsupported.");
            }
            string payload = SerializePayload(envelope.Snapshot);
            if (!string.Equals(
                    envelope.PayloadSha256,
                    Sha256(payload),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Snapshot checksum does not match its payload.");
            }
            EndogenousRunSnapshotValidator.Validate(envelope.Snapshot);
            return envelope.Snapshot;
        }

        private static void WriteDurably(string path, string json)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
        }

        private static void RecoverableReplace(
            string temporaryPath,
            string primaryPath,
            string backupPath)
        {
            File.Copy(primaryPath, backupPath, overwrite: true);
            File.Delete(primaryPath);
            File.Move(temporaryPath, primaryPath);
        }

        private static string Sha256(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Utf8WithoutBom.GetBytes(value));
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2"));
                return result.ToString();
            }
        }

        private static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new InstanceFieldContractResolver(),
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                Culture = System.Globalization.CultureInfo.InvariantCulture,
            };
        }

        private sealed class SnapshotEnvelope
        {
            public int EnvelopeVersion;
            public string PayloadSha256;
            public EndogenousRunSnapshot Snapshot;
        }

        private sealed class InstanceFieldContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(
                Type type,
                MemberSerialization memberSerialization)
            {
                List<JsonProperty> properties = type
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => !field.IsStatic && !field.IsNotSerialized)
                    .Select(field => CreateProperty(field, MemberSerialization.Fields))
                    .OrderBy(property => property.PropertyName, StringComparer.Ordinal)
                    .ToList();
                for (int i = 0; i < properties.Count; i++)
                {
                    properties[i].Readable = true;
                    properties[i].Writable = true;
                }
                return properties;
            }
        }
    }
}
