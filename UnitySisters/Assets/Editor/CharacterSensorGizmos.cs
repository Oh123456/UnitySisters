using _3DModule.Sensing;
using UnityEditor;
using UnityEngine;
using UnitySisters.Controller;
using UnitySisters.Table;

namespace UnitySisters.Editor
{
    internal static class CharacterSensorGizmos
    {
        private static readonly Color[] colors =
        {
            new Color(1f, 0.85f, 0.15f), new Color(0.15f, 0.75f, 1f),
            new Color(1f, 0.3f, 0.25f), new Color(0.25f, 1f, 0.45f),
            new Color(0.85f, 0.4f, 1f), new Color(1f, 0.6f, 0.2f),
        };

        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy | GizmoType.NonSelected)]
        private static void Draw(Character character, GizmoType type)
        {
            bool selected = (type & (GizmoType.Selected | GizmoType.InSelectionHierarchy)) != 0;
            if (!selected && (!Application.isPlaying || !character.SensorModel.IsActive)) return;
            Color oldGizmo = Gizmos.color;
            Color oldHandle = Handles.color;
            try
            {
                CharacterAIData data = FindProfile(character);
                Vector3 origin = character.SensorEye.position;
                string label = character.SensorKey + (Application.isPlaying
                    ? (character.SensorModel.IsActive ? " | sensors active" : " | sensors inactive") : " | table preview");
                if (data == null)
                {
                    Handles.Label(origin + Vector3.up * 0.3f, label + "\nNo matching scene AI table row");
                    return;
                }
                Handles.Label(origin + Vector3.up * 0.3f, label);
                for (int i = 0; i < data.Sensors.Count; i++)
                {
                    SensorSettings settings = data.Sensors[i];
                    if ((int)settings.kind < 0 || (int)settings.kind >= colors.Length) continue;
                    Color color = colors[(int)settings.kind];
                    color.a = selected ? 0.8f : 0.25f;
                    Gizmos.color = color; Handles.color = color;
                    Transform basis = settings.kind == SensorKind.Vision ? character.SensorEye :
                        settings.kind == SensorKind.Hearing ? character.SensorEar : character.transform;
                    if (settings.kind == SensorKind.Vision) DrawVision(basis, settings);
                    else if (selected) Gizmos.DrawWireSphere(basis.position, settings.range);
                    if (selected)
                        Handles.Label(origin + Vector3.down * (0.25f * (i + 1)),
                            $"{settings.kind}: {settings.range:0.#}m" +
                            (settings.kind == SensorKind.Vision ? $" / {settings.viewAngle:0.#}°" : ""));
                }
                if (!Application.isPlaying) return;
                var model = character.PerceptionModel;
                if (model.DetectedTarget != null)
                {
                    Handles.color = Color.red;
                    Handles.DrawAAPolyLine(3, origin, model.DetectedTarget.position);
                    Handles.Label(model.DetectedTarget.position + Vector3.up, "Detected: chase");
                }
                else if (model.SearchPending)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawDottedLine(origin, model.LastSeenPosition, 5);
                    Handles.DrawWireDisc(model.LastSeenPosition, Vector3.up, .6f);
                    Handles.Label(model.LastSeenPosition + Vector3.up, "Search last seen");
                }
                else if (model.HasInvestigation)
                {
                    Handles.color = Color.cyan;
                    Handles.DrawDottedLine(origin, model.InvestigationPosition, 5);
                    Handles.DrawWireDisc(model.InvestigationPosition, Vector3.up, 0.5f);
                    Handles.Label(model.InvestigationPosition + Vector3.up * 0.3f, "Investigate");
                }
                else
                {
                    var behavior = character.AIBehaviorModel;
                    Handles.color = Color.green;
                    Handles.DrawWireDisc(behavior.RoamCenter, Vector3.up, data.Behavior.roamRadius);
                    if (behavior.HasDestination) Handles.DrawDottedLine(origin, behavior.Destination, 5);
                    Handles.Label(origin + Vector3.up * .7f, "Roam");
                }
                if (character.SensorModel.Control.QueryOverflowed)
                    Handles.Label(origin + Vector3.up, "Sensor query capacity reached (partial results)");
            }
            finally { Gizmos.color = oldGizmo; Handles.color = oldHandle; }
        }

        private static void DrawVision(Transform eye, in SensorSettings settings)
        {
            float half = settings.viewAngle * 0.5f;
            Vector3 left = Quaternion.AngleAxis(-half, eye.up) * eye.forward;
            Vector3 right = Quaternion.AngleAxis(half, eye.up) * eye.forward;
            Handles.DrawWireArc(eye.position, eye.up, left, settings.viewAngle, settings.range);
            Handles.DrawLine(eye.position, eye.position + left * settings.range);
            Handles.DrawLine(eye.position, eye.position + right * settings.range);
            Vector3 top = Quaternion.AngleAxis(-half, eye.right) * eye.forward;
            Handles.DrawWireArc(eye.position, eye.right, top, settings.viewAngle, settings.range);
            Handles.DrawLine(eye.position, eye.position + top * settings.range);
            Handles.DrawLine(eye.position, eye.position + Quaternion.AngleAxis(half, eye.right) * eye.forward * settings.range);
            Handles.DrawWireDisc(eye.position + eye.forward * Mathf.Cos(half * Mathf.Deg2Rad) * settings.range,
                eye.forward, Mathf.Sin(half * Mathf.Deg2Rad) * settings.range);
        }

        private static CharacterAIData FindProfile(Character character)
        {
            foreach (SensorScene scene in Object.FindObjectsByType<SensorScene>(FindObjectsSortMode.None))
            {
                if (scene.gameObject.scene != character.gameObject.scene) continue;
                using (var serialized = new SerializedObject(scene))
                {
                    SerializedProperty rows = serialized.FindProperty("profiles");
                    for (int i = 0; i < rows.arraySize; i++)
                    {
                        var row = rows.GetArrayElementAtIndex(i).objectReferenceValue as CharacterAIData;
                        if (row != null && row.Key == character.SensorKey) return row;
                    }
                }
            }
            return null;
        }
    }
}

