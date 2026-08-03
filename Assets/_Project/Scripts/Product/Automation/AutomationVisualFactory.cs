using UnityEngine;

namespace Desk42.Product.Automation
{
    internal static class AutomationVisualFactory
    {
        internal static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color colour)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            value.GetComponent<Renderer>().material = Material(colour);
            return value;
        }

        internal static GameObject CreateStation(
            Transform parent,
            string name,
            Vector3 position,
            Color colour,
            string verb)
        {
            GameObject root = new(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            CreateBlock(root.transform, "Selection Plinth", new Vector3(0f, 0.06f, 0f),
                new Vector3(2.95f, 0.08f, 2.05f), new Color(0.16f, 0.18f, 0.17f));
            CreateBlock(root.transform, "Cabinet", new Vector3(0f, 0.55f, 0f),
                new Vector3(2.5f, 1.1f, 1.65f), colour);
            CreateBlock(root.transform, "Cabinet Shadow", new Vector3(0f, 0.48f, -0.84f),
                new Vector3(2.26f, 0.82f, 0.08f), colour * 0.56f);
            CreateBlock(root.transform, "Left Foot", new Vector3(-0.88f, -0.05f, 0f),
                new Vector3(0.22f, 0.26f, 1.30f), new Color(0.08f, 0.10f, 0.095f));
            CreateBlock(root.transform, "Right Foot", new Vector3(0.88f, -0.05f, 0f),
                new Vector3(0.22f, 0.26f, 1.30f), new Color(0.08f, 0.10f, 0.095f));
            CreateBlock(root.transform, "Worktop", new Vector3(0f, 1.18f, 0f),
                new Vector3(2.72f, 0.16f, 1.82f), colour * 1.12f);
            CreateBlock(root.transform, "Input Tray", new Vector3(-0.72f, 1.38f, 0f),
                new Vector3(0.75f, 0.16f, 1.1f), new Color(0.18f, 0.20f, 0.19f));
            CreateBlock(root.transform, "Output Tray", new Vector3(0.26f, 1.37f, 0f),
                new Vector3(0.55f, 0.12f, 0.94f), new Color(0.10f, 0.12f, 0.115f));
            CreateBlock(root.transform, "Terminal", new Vector3(0.72f, 1.42f, -0.22f),
                new Vector3(0.45f, 0.46f, 0.12f), new Color(0.055f, 0.075f, 0.067f));
            CreateBlock(root.transform, "Terminal Glow", new Vector3(0.72f, 1.43f, -0.29f),
                new Vector3(0.33f, 0.27f, 0.025f), new Color(0.28f, 0.73f, 0.57f));
            CreateCylinder(root.transform, "Feed Roller A", new Vector3(-0.86f, 1.50f, 0f),
                new Vector3(0.12f, 0.44f, 0.12f), new Color(0.10f, 0.11f, 0.10f),
                new Vector3(0f, 0f, 90f));
            CreateCylinder(root.transform, "Feed Roller B", new Vector3(-0.57f, 1.50f, 0f),
                new Vector3(0.12f, 0.44f, 0.12f), new Color(0.10f, 0.11f, 0.10f),
                new Vector3(0f, 0f, 90f));
            CreateBlock(root.transform, "Machine Light", new Vector3(0.9f, 1.45f, 0f),
                new Vector3(0.22f, 0.22f, 0.22f), new Color(0.86f, 0.58f, 0.18f));
            CreateWorldLabel(root.transform, StationLabel(name),
                new Vector3(0f, 2.18f, 0.14f),
                0.068f, new Color(0.88f, 0.84f, 0.67f),
                TextAnchor.MiddleCenter);
            CreateWorldLabel(root.transform, verb,
                new Vector3(-0.52f, 1.72f, 0f),
                0.046f, new Color(0.58f, 0.65f, 0.60f),
                TextAnchor.MiddleCenter);
            CreateWorldLabel(root.transform, "Q 00",
                new Vector3(0.72f, 1.72f, 0f),
                0.043f, new Color(0.95f, 0.64f, 0.22f),
                TextAnchor.MiddleCenter);
            return root;
        }

        internal static GameObject CreateCylinder(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color colour,
            Vector3 rotation)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            value.transform.localEulerAngles = rotation;
            value.GetComponent<Renderer>().material = Material(colour);
            return value;
        }

        internal static GameObject CreateStaff(
            Transform parent,
            string name,
            Vector3 position,
            Color colour)
        {
            GameObject root = new(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(0.36f, 0.55f, 0.36f);
            body.GetComponent<Renderer>().material = Material(colour);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            head.transform.localScale = Vector3.one * 0.42f;
            head.GetComponent<Renderer>().material = Material(
                new Color(0.69f, 0.61f, 0.48f));
            CreateBlock(root.transform, "Visor", new Vector3(0f, 1.58f, -0.20f),
                new Vector3(0.34f, 0.12f, 0.055f), new Color(0.16f, 0.38f, 0.38f));
            CreateBlock(root.transform, "Badge", new Vector3(0.18f, 0.90f, -0.31f),
                new Vector3(0.13f, 0.17f, 0.04f), new Color(0.84f, 0.64f, 0.22f));
            root.AddComponent<AutomationStaffBob>();
            return root;
        }

        internal static Color IssueColour(string issue)
        {
            issue ??= string.Empty;
            if (issue.IndexOf("Collective", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.73f, 0.27f, 0.64f);
            if (issue.IndexOf("Access", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.22f, 0.59f, 0.82f);
            if (issue.IndexOf("Identity", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.55f, 0.38f, 0.86f);
            if (issue.IndexOf("Dependency", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.91f, 0.38f, 0.27f);
            return new Color(0.88f, 0.66f, 0.24f);
        }

        internal static void CreateIssueGlyph(Transform parent, string issue, Color colour)
        {
            issue ??= string.Empty;
            if (issue.IndexOf("Identity", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CreateBlock(parent, "Identity Before", new Vector3(-0.11f, 0.30f, 0.15f),
                    new Vector3(0.16f, 0.05f, 0.24f), colour);
                CreateBlock(parent, "Identity After", new Vector3(0.11f, 0.32f, 0.15f),
                    new Vector3(0.16f, 0.05f, 0.24f), colour * 1.16f);
                return;
            }
            if (issue.IndexOf("Dependency", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CreateBlock(parent, "Dependent", new Vector3(-0.10f, 0.30f, 0.15f),
                    new Vector3(0.13f, 0.05f, 0.15f), colour * 1.14f);
                CreateBlock(parent, "Support", new Vector3(0.11f, 0.30f, 0.15f),
                    new Vector3(0.20f, 0.05f, 0.28f), colour);
                return;
            }
            if (issue.IndexOf("Collective", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                for (int i = 0; i < 3; i++)
                    CreateBlock(parent, "Collective Member " + i,
                        new Vector3(-0.18f + i * 0.18f, 0.30f, 0.15f),
                        new Vector3(0.11f, 0.05f, 0.20f), colour);
                return;
            }
            if (issue.IndexOf("Access", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CreateBlock(parent, "Access Gate L", new Vector3(-0.12f, 0.30f, 0.15f),
                    new Vector3(0.08f, 0.05f, 0.30f), colour);
                CreateBlock(parent, "Access Gate R", new Vector3(0.12f, 0.30f, 0.15f),
                    new Vector3(0.08f, 0.05f, 0.30f), colour);
                return;
            }
            CreateBlock(parent, "Possession Asset", new Vector3(0f, 0.30f, 0.15f),
                new Vector3(0.28f, 0.05f, 0.28f), colour);
        }

        internal static GameObject CreateFolderToken(
            Transform parent,
            string label,
            Color colour)
        {
            GameObject root = new(label);
            root.transform.SetParent(parent, false);
            GameObject folder = CreateBlock(root.transform, "Folder", Vector3.zero,
                new Vector3(0.82f, 0.20f, 1.05f), colour);
            folder.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            CreateBlock(root.transform, "Paper", new Vector3(0f, 0.14f, 0f),
                new Vector3(0.68f, 0.06f, 0.86f), new Color(0.91f, 0.86f, 0.70f));
            CreateBlock(root.transform, "Urgency Tab", new Vector3(0.29f, 0.18f, 0.38f),
                new Vector3(0.18f, 0.09f, 0.25f), new Color(0.68f, 0.23f, 0.20f));
            CreateWorldLabel(root.transform, label,
                new Vector3(0f, 0.46f, -0.08f),
                0.030f, new Color(0.95f, 0.90f, 0.73f),
                TextAnchor.MiddleCenter);
            return root;
        }

        private static string StationLabel(string value)
        {
            return value switch
            {
                "PUBLIC INTAKE" => "PUBLIC\nINTAKE",
                "EVIDENCE SPLIT" => "EVIDENCE\nSPLIT",
                "OUTPUT GATE" => "OUTPUT\nGATE",
                "AUX VERIFICATION" => "AUX\nVERIFICATION",
                "LEGAL / APPEALS" => "LEGAL\nAPPEALS",
                _ => value,
            };
        }

        internal static GameObject CreateWorldLabel(
            Transform parent,
            string text,
            Vector3 localPosition,
            float characterSize,
            Color colour,
            TextAnchor anchor)
        {
            GameObject root = new("Label " + text);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.Euler(52f, 0f, 0f);
            TextMesh mesh = root.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.color = colour;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.GetComponent<Renderer>().material = mesh.font.material;
            return root;
        }

        private static Material Material(Color colour)
        {
            Shader shader = Shader.Find("Desk42/AutomationLit");
            if (shader == null)
                throw new MissingReferenceException(
                    "Desk42/AutomationLit must be present in a Resources folder.");
            var material = new Material(shader) { color = colour };
            return material;
        }
    }

    internal sealed class AutomationStaffBob : MonoBehaviour
    {
        private Vector3 _origin;
        private float _phase;

        private void Awake()
        {
            _origin = transform.localPosition;
            _phase = StablePhase(name);
        }

        private void Update()
        {
            transform.localPosition = _origin + Vector3.up *
                (Mathf.Sin(Time.time * 2.2f + _phase) * 0.035f);
        }

        private static float StablePhase(string value)
        {
            int total = 0;
            for (int i = 0; i < value.Length; i++) total = total * 31 + value[i];
            return Mathf.Abs(total % 628) / 100f;
        }
    }
}
