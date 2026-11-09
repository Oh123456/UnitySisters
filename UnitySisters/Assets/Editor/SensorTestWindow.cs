using System.Reflection;
using _3DModule.Sensing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnitySisters.Controller;
using UnitySisters.Controller.Interface;

namespace UnitySisters.Editor
{
    internal sealed class SensorTestWindow : EditorWindow
    {
        private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private Label status;
        private VisualElement controls;
        [MenuItem("UnitySisters/Sensors/Test Panel")]
        private static void Open() { GetWindow<SensorTestWindow>("Sensor Test"); }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.Add(new HelpBox("SensorTestScene: Play, then use WASD / Jump / Tab. Freeze AI movement to test a stationary observer. Gizmos show actual eye direction.", HelpBoxMessageType.Info));
            status = new Label(); rootVisualElement.Add(status);
            controls = new VisualElement(); rootVisualElement.Add(controls);
            AddButton("Freeze AI movement / facing", (ai, actor, candidate) => actor.GetComponent<IMoveControl>()?.LockMove());
            AddButton("Allow AI movement", (ai, actor, candidate) => actor.GetComponent<IMoveControl>()?.UnlockMove());
            AddButton("Face forward (+Z)", (ai, actor, candidate) => actor.SensorEye.rotation = Quaternion.identity);
            AddButton("Reset characters to test spawns", (ai, actor, candidate) =>
            {
                var observer = GameObject.Find("ObserverSpawn"); var spawn = GameObject.Find("PlayerSpawn");
                if (observer == null || spawn == null) return;
                Move(actor, observer.transform); Move(candidate, spawn.transform);
                ai.ConnectCharacter(actor, candidate.transform); actor.SensorEye.rotation = Quaternion.identity;
            });
            AddButton("Emit sound 4m to AI's right", (ai, actor, candidate) =>
            {
                var scene = Object.FindFirstObjectByType<SensorScene>();
                if (scene == null) return;
                var sound = new SensorStimulus { kind = StimulusKind.Sound, source = candidate.transform,
                    position = actor.transform.position + Vector3.right * 4, strength = 10, category = 1, time = Time.time };
                ((ISensorStimulusReceiver)scene).Receive(sound);
            });
            AddButton("Select AI character", (ai, actor, candidate) => Selection.activeGameObject = actor.gameObject);
            OnInspectorUpdate();
        }
        private bool TryGet(out AIController ai, out Character actor, out Character candidate)
        {
            ai = null; actor = null; candidate = null;
            if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SensorTestScene") return false;
            ai = Object.FindFirstObjectByType<AIController>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (ai == null || player == null) return false;
            actor = (Character)typeof(AIController).GetField("currentControlCharacter", flags).GetValue(ai);
            candidate = (Character)typeof(PlayerController).GetField("currentControlCharacter", flags).GetValue(player);
            return actor != null && candidate != null;
        }
        private void AddButton(string text, System.Action<AIController, Character, Character> action)
        {
            controls.Add(new Button(() => { if (TryGet(out var ai, out var actor, out var candidate)) action(ai, actor, candidate); }) { text = text });
        }
        private void OnInspectorUpdate()
        {
            if (status == null) return;
            bool ready = TryGet(out _, out var actor, out _); controls.SetEnabled(ready);
            status.text = ready ? $"AI: {actor.name}\nVision: {actor.PerceptionModel.Visible}\nProximity: {actor.PerceptionModel.Nearby}\nInvestigation: {actor.PerceptionModel.HasInvestigation}" : "Open SensorTestScene and enter Play mode.";
        }
        private static void Move(Character character, Transform spawn)
        {
            var capsule = character.GetComponent<CharacterController>();
            bool enabled = capsule.enabled; capsule.enabled = false;
            character.transform.SetPositionAndRotation(spawn.position, spawn.rotation); capsule.enabled = enabled;
        }
    }
}
