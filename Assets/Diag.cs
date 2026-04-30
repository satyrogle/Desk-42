using UnityEngine;
using UnityEditor;
using Desk42.UI;
public class Diag {
    public static void Run() {
        var ch = Object.FindFirstObjectByType<CardHandView>();
        Debug.Log("CardHand: " + (ch != null ? ch.name : "null"));
        if (ch != null) {
            var so = new SerializedObject(ch);
            Debug.Log("Prefab: " + so.FindProperty("_cardButtonPrefab").objectReferenceValue);
            Debug.Log("Container: " + so.FindProperty("_cardContainer").objectReferenceValue);
            Debug.Log("Machine: " + so.FindProperty("_machine").objectReferenceValue);
        }
    }
}
