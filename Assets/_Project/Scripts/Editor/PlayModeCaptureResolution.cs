#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Desk42.Editor
{
    /// <summary>
    /// Selects a fixed Game view size for visible PlayMode evidence captures.
    /// Unity's editor Test Runner ignores -screen-width/-screen-height, so the
    /// deterministic harness requests its render target through an environment
    /// variable instead. This editor-only adapter never enters a player build.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeCaptureResolution
    {
        private const string ResolutionVariable =
            "DESK42_TEST_CAPTURE_RESOLUTION";

        static PlayModeCaptureResolution()
        {
            if (!string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(ResolutionVariable)))
            {
                EditorApplication.delayCall += ApplyRequestedResolution;
            }
        }

        private static void ApplyRequestedResolution()
        {
            string requested =
                Environment.GetEnvironmentVariable(ResolutionVariable);
            if (!TryParse(requested, out int width, out int height))
            {
                Debug.LogError(
                    $"[CaptureResolution] {ResolutionVariable} must use " +
                    $"WIDTHxHEIGHT, received '{requested}'.");
                return;
            }

            try
            {
                Assembly editorAssembly = typeof(EditorWindow).Assembly;
                Type sizesType =
                    editorAssembly.GetType("UnityEditor.GameViewSizes", true);
                Type groupType =
                    editorAssembly.GetType("UnityEditor.GameViewSizeGroupType", true);
                Type sizeType =
                    editorAssembly.GetType("UnityEditor.GameViewSize", true);
                Type sizeKindType =
                    editorAssembly.GetType("UnityEditor.GameViewSizeType", true);
                Type gameViewType =
                    editorAssembly.GetType("UnityEditor.GameView", true);

                Type singletonType =
                    typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singletonType
                    .GetProperty("instance", BindingFlags.Public
                        | BindingFlags.Static)
                    ?.GetValue(null);
                object standalone = Enum.Parse(groupType, "Standalone");
                object group = sizesType
                    .GetMethod("GetGroup", BindingFlags.Public
                        | BindingFlags.Instance)
                    ?.Invoke(sizes, new[] { standalone });

                int selectedIndex = FindSize(
                    group, width, height, out int totalCount);
                if (selectedIndex < 0)
                {
                    object fixedResolution =
                        Enum.Parse(sizeKindType, "FixedResolution");
                    object newSize = Activator.CreateInstance(
                        sizeType,
                        fixedResolution,
                        width,
                        height,
                        $"Desk 42 Capture {width}x{height}");
                    group.GetType()
                        .GetMethod("AddCustomSize", BindingFlags.Public
                            | BindingFlags.Instance)
                        ?.Invoke(group, new[] { newSize });
                    selectedIndex = totalCount;
                }

                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                PropertyInfo selectedSize = gameViewType.GetProperty(
                    "selectedSizeIndex",
                    BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance);
                if (selectedSize == null)
                    throw new MissingMemberException(
                        gameViewType.FullName, "selectedSizeIndex");

                selectedSize.SetValue(gameView, selectedIndex);
                gameView.Repaint();
                Debug.Log(
                    $"[CaptureResolution] Game view set to {width}x{height}.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[CaptureResolution] Could not set the Game view: " +
                    exception);
            }
        }

        private static int FindSize(
            object group, int width, int height, out int totalCount)
        {
            if (group == null)
                throw new InvalidOperationException(
                    "Standalone Game view size group was not available.");

            Type groupRuntimeType = group.GetType();
            totalCount = (int)groupRuntimeType
                .GetMethod("GetTotalCount", BindingFlags.Public
                    | BindingFlags.Instance)
                .Invoke(group, null);
            MethodInfo getSize = groupRuntimeType.GetMethod(
                "GetGameViewSize", BindingFlags.Public
                    | BindingFlags.Instance);

            for (int index = 0; index < totalCount; index++)
            {
                object size = getSize.Invoke(group, new object[] { index });
                Type runtimeSizeType = size.GetType();
                int candidateWidth = (int)runtimeSizeType
                    .GetProperty("width", BindingFlags.Public
                        | BindingFlags.Instance)
                    .GetValue(size);
                int candidateHeight = (int)runtimeSizeType
                    .GetProperty("height", BindingFlags.Public
                        | BindingFlags.Instance)
                    .GetValue(size);
                if (candidateWidth == width && candidateHeight == height)
                    return index;
            }

            return -1;
        }

        private static bool TryParse(
            string value, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] dimensions = value.ToLowerInvariant().Split('x');
            return dimensions.Length == 2
                && int.TryParse(dimensions[0], out width)
                && int.TryParse(dimensions[1], out height)
                && width > 0
                && height > 0;
        }
    }
}
#endif
