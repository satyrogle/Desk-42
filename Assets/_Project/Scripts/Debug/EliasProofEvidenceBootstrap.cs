#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections;
using System.IO;
using Desk42.Core;
using Desk42.Encounter;
using Desk42.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Desk42.Debugging
{
    /// <summary>
    /// Opt-in development-player evidence route. It uses the live Shift scene,
    /// real EncounterManager transaction, and real protected receipt presenter.
    /// It is inert unless -desk42ProofEvidencePath is supplied.
    /// </summary>
    public sealed class EliasProofEvidenceBootstrap : MonoBehaviour
    {
        private const string OutputArgument =
            "-desk42ProofEvidencePath";
        private const string BranchArgument =
            "-desk42ProofEvidenceBranch";
        private const int FrameCount = 100;
        private const int FramesPerSecond = 10;
        private const int ProcedureFrame = 10;

        private static bool _created;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            if (_created)
                return;
            string outputPath = ReadArgument(OutputArgument);
            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            _created = true;
            Application.runInBackground = true;
            Screen.SetResolution(1280, 720, fullscreen: false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            var host = new GameObject(
                nameof(EliasProofEvidenceBootstrap));
            DontDestroyOnLoad(host);
            var bootstrap =
                host.AddComponent<EliasProofEvidenceBootstrap>();
            bootstrap.StartCoroutine(bootstrap.Capture(
                Path.GetFullPath(outputPath),
                ReadArgument(BranchArgument)));
        }

        private IEnumerator Capture(
            string outputDirectory, string branchArgument)
        {
            if (!TryParseRoute(
                    branchArgument,
                    out EliasProcedureActionId shift2Action,
                    out EliasShift2Branch branch,
                    out string branchLabel))
            {
                Fail($"Unknown evidence branch '{branchArgument}'.");
                yield break;
            }

            Directory.CreateDirectory(outputDirectory);
            string framesDirectory =
                Path.Combine(outputDirectory, "receipt-frames");
            Directory.CreateDirectory(framesDirectory);

            float deadline = Time.realtimeSinceStartup + 45f;
            while ((GameManager.Instance == null
                    || SceneManager.GetActiveScene().name != "MainMenu")
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                Fail("GameManager never reached MainMenu.");
                yield break;
            }

            var fixtureMeta = new MetaProgressData
            {
                TutorialCompleted = true,
                HighestPhaseReached = 4,
                GlobalShiftNumber = 1,
            };
            manager.SetMetaForTesting(fixtureMeta);
            EliasProofSessionController proof = manager.EliasProof;
            proof.BeginProofSession($"evidence-{branchLabel}");

            // Establish Shift 1 with the same controller validators used by
            // play, then enter an authored Shift 2 run.
            manager.Run.BeginNewRun(
                424201, "auditor", 1, fixtureMeta);
            proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            proof.RecordDisposition(
                EliasProofContent.Shift1AppearanceKey,
                BuildSyntheticDisposition(
                    EliasProofContent.Shift1AppearanceKey,
                    ClaimResolutionKind.Approve));

            manager.Run.BeginNewRun(
                424202 + (int)branch, "auditor", 2, fixtureMeta);
            manager.LoadScene(SceneID.Shift);

            yield return WaitForNewShiftEncounter(
                previous: null,
                result => _activeEncounter = result);
            if (_activeEncounter == null)
                yield break;
            HideTutorial();

            yield return AdvanceToElias(
                _activeEncounter,
                EliasProofContent.Shift2AppearanceKey);
            if (_activeEncounter?.ActiveClaim == null)
                yield break;

            string shift2Context =
                Path.Combine(outputDirectory, "shift2-context.png");
            yield return CaptureFrame(shift2Context);

            AppliedEliasProcedure applied = default;
            for (int frame = 0; frame < FrameCount; frame++)
            {
                if (frame == ProcedureFrame)
                {
                    bool succeeded =
                        _activeEncounter.TryApplyEliasProcedure(
                            shift2Action,
                            out applied,
                            out string unavailableReason);
                    if (!succeeded)
                    {
                        Fail(
                            $"Could not apply {shift2Action}: " +
                            $"{unavailableReason}");
                        yield break;
                    }
                }

                yield return CaptureFrame(
                    Path.Combine(
                        framesDirectory,
                        $"frame_{frame:000}.png"));
                yield return new WaitForSecondsRealtime(
                    1f / FramesPerSecond);
            }

            EliasProcedureReceiptPresenter presenter =
                FindObjectOfType<EliasProcedureReceiptPresenter>(true);
            if (presenter != null && presenter.IsPresenting)
            {
                Fail("Receipt exceeded the ten-second evidence window.");
                yield break;
            }
            if (string.IsNullOrWhiteSpace(applied.ReceiptId))
            {
                Fail("Receipt transaction was not applied.");
                yield break;
            }

            _activeEncounter.Approve();
            yield return null;
            if (proof.State.Shift2FinalDisposition
                != ClaimResolutionKind.Approve)
            {
                Fail("Shift 2 ordinary disposition was not recorded.");
                yield break;
            }

            EncounterManager previousEncounter = _activeEncounter;
            manager.Run.BeginNewRun(
                424205 + (int)branch, "auditor", 5, fixtureMeta);
            manager.LoadScene(SceneID.Shift);
            _activeEncounter = null;
            yield return WaitForNewShiftEncounter(
                previousEncounter,
                result => _activeEncounter = result);
            if (_activeEncounter == null)
                yield break;
            HideTutorial();

            yield return AdvanceToElias(
                _activeEncounter,
                EliasProofContent.Shift5AppearanceKey);
            if (_activeEncounter?.ActiveClaim == null)
                yield break;

            string shift5Context =
                Path.Combine(outputDirectory, "shift5-context.png");
            yield return CaptureFrame(shift5Context);

            string manifest =
                $"branch={branch}{Environment.NewLine}" +
                $"shift2Action={shift2Action}{Environment.NewLine}" +
                $"receiptId={applied.ReceiptId}{Environment.NewLine}" +
                $"frames={FrameCount}{Environment.NewLine}" +
                $"framesPerSecond={FramesPerSecond}{Environment.NewLine}" +
                $"procedureFrame={ProcedureFrame}{Environment.NewLine}" +
                $"shift2Context={Path.GetFileName(shift2Context)}" +
                $"{Environment.NewLine}" +
                $"shift5Context={Path.GetFileName(shift5Context)}" +
                $"{Environment.NewLine}";
            File.WriteAllText(
                Path.Combine(outputDirectory, "manifest.txt"),
                manifest);

            Debug.Log(
                $"[EliasProofEvidence] Captured {branch} to " +
                $"{outputDirectory}.");
            yield return new WaitForSecondsRealtime(0.25f);
            Application.Quit(0);
        }

        private EncounterManager _activeEncounter;

        private static IEnumerator WaitForNewShiftEncounter(
            EncounterManager previous,
            Action<EncounterManager> assign)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            EncounterManager encounter = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name == "Shift")
                {
                    encounter = FindObjectOfType<EncounterManager>();
                    if (encounter != null
                        && !ReferenceEquals(encounter, previous))
                    {
                        break;
                    }
                }
                yield return null;
            }

            assign(encounter);
            if (encounter == null)
                Fail("Shift encounter did not become ready.");
        }

        private static IEnumerator AdvanceToElias(
            EncounterManager encounter, string appearanceKey)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < deadline)
            {
                ActiveClaimData claim = encounter.ActiveClaim;
                if (claim == null)
                {
                    yield return null;
                    continue;
                }
                if (string.Equals(
                        claim.AuthoredAppearanceKey,
                        appearanceKey,
                        StringComparison.Ordinal)
                    && string.Equals(
                        claim.ClientVariantId,
                        EliasProofContent.CanonicalClaimantId,
                        StringComparison.Ordinal))
                {
                    yield break;
                }

                encounter.Approve();
                ResolvePendingDilemmaForCapture();
                yield return null;
            }

            Fail($"Elias appearance '{appearanceKey}' was not reached.");
        }

        private static void ResolvePendingDilemmaForCapture()
        {
            MoralDilemmaPanel panel =
                FindObjectOfType<MoralDilemmaPanel>(true);
            panel?.TryResolvePendingForAutomation(
                choseEthical: true);
        }

        private static IEnumerator CaptureFrame(string outputPath)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath));
            yield return new WaitForEndOfFrame();
            Texture2D texture =
                ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null)
            {
                Fail($"Could not capture '{outputPath}'.");
                yield break;
            }
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            Destroy(texture);
        }

        private static AppliedClaimResolution BuildSyntheticDisposition(
            string claimId, ClaimResolutionKind kind)
            => new(
                claimId,
                EliasProofContent.CanonicalClaimantId,
                "moth_accountant",
                kind,
                creditsDelta: 0,
                sanityDelta: 0f,
                soulIntegrityDelta: 0f,
                darkIntelligenceDelta: 0,
                quotaBefore: 0,
                quotaAfter: 1,
                quotaRequired: 3,
                complianceStreakBefore: 1f,
                complianceStreakAfter: 1f);

        private static bool TryParseRoute(
            string value,
            out EliasProcedureActionId action,
            out EliasShift2Branch branch,
            out string label)
        {
            label = value?.Trim().ToUpperInvariant();
            switch (label)
            {
                case "A":
                    action = EliasProcedureActionId.AmendRecord;
                    branch = EliasShift2Branch.NormalisedAddress;
                    return true;
                case "B":
                    action = EliasProcedureActionId.RetainLegacyUnit;
                    branch = EliasShift2Branch.LegacyException;
                    return true;
                case "C":
                    action = EliasProcedureActionId.ReferForReview;
                    branch = EliasShift2Branch.PhysicalVerification;
                    return true;
                default:
                    action = EliasProcedureActionId.Unspecified;
                    branch = EliasShift2Branch.None;
                    return false;
            }
        }

        private static void HideTutorial()
        {
            GameObject tutorialCanvas =
                GameObject.Find("TutorialCanvas");
            if (tutorialCanvas != null)
                tutorialCanvas.SetActive(false);
        }

        private static string ReadArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(
                        args[i], key, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[EliasProofEvidence] {message}");
            Application.Quit(1);
        }
    }
}

#endif
