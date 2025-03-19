using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zounds {

    [ExecuteAlways]
    public class ZoundEngine : MonoBehaviour {
        
        private static ZoundEngine instance;
        internal static ZoundEngine Instance {
            get {
                if (instance == null) {
                    var go = new GameObject();
                    instance = go.AddComponent<ZoundEngine>();
                    if (Application.isPlaying) {
                        go.name = "ZoundEngine";
                        DontDestroyOnLoad(go);
                    }
                    else {
                        go.name = "ZoundEngine [EditMode|NonSavable]";
                        Debug.Log("Edit mode instance created.");
                        go.hideFlags = HideFlags.DontSave;
                    }

#if UNITY_EDITOR
                    DetermineUpdater();
#else //Use runtime updater in build
                    UseRuntimeUpdater();
#endif

                }
                return instance;
            }
        }

        [SerializeField] private ZoundPool pool = new ZoundPool();

        private Dictionary<Zound, float> zoundLastPlayedTimes = new Dictionary<Zound, float>();
        private Dictionary<Zound, LinkedList<ZoundToken>> cullingGroups = new Dictionary<Zound, LinkedList<ZoundToken>>();
        private List<ZoundToken> tokens = new List<ZoundToken>();

        // this is to prevent calling ZoundsProject.Instance multiple times, which involves comparation agains null instance
        private static float masterVolume;

        internal static ZoundPool Pool => Instance.pool;
        internal static Dictionary<Zound, LinkedList<ZoundToken>> CullingGroups => Instance.cullingGroups;

        public static void Initialize() {
            if (!Application.isPlaying) {
                Debug.LogError("Can't initialize ZoundEngine during edit mode.");
                return;
            }
            var inst = Instance;
#if ADDRESSABLES_INSTALLED
            ZoundDictionary.Initialize();
#endif
        }

        public static async Task InitializeAsync() {
            if (!Application.isPlaying) {
                Debug.LogError("Can't initialize ZoundEngine during edit mode.");
                return;
            }
#if ADDRESSABLES_INSTALLED
            await ZoundDictionary.InitializeAsync();
#endif
        }

        public static void StopAllZounds(bool cleanupPool = false) {
            var inst = Instance;
            foreach (var token in inst.tokens) {
                token.Kill();
            }
            inst.tokens.Clear();
            foreach (var cullingGroup in inst.cullingGroups.Values) {
                cullingGroup.Clear();
            }
            inst.pool.StopAllSources(cleanupPool);
        }

        public static ZoundToken PlayZound(string zoundName) {
            if (ZoundDictionary.TryGetZoundByName(zoundName, out Zound zound)) {
                return PlayZound(zound);
            }
            else {
                Debug.LogError("Error playing " + zoundName + ": Zound name doesn't exist.");
                return null;
            }
        }

        public static ZoundToken PlayZound(Zound zound) {
            float currentRealtime = Time.realtimeSinceStartup;
            var inst = Instance;
            var projectSettings = ZoundsProject.Instance.projectSettings;
            if (inst.zoundLastPlayedTimes.TryGetValue(zound, out float lastPlayedTime)) {
                float cooldownDuration = projectSettings.cooldownDuration;
                if (currentRealtime - lastPlayedTime < cooldownDuration) {
                    return null;
                }
            }

            float chanceResult = Random.Range(0f, 1f);
            if (chanceResult > zound.chance + Mathf.Epsilon) {
                return null;
            }

            if (inst.zoundLastPlayedTimes.ContainsKey(zound)) {
                inst.zoundLastPlayedTimes[zound] = currentRealtime;
            }
            else {
                inst.zoundLastPlayedTimes.Add(zound, currentRealtime);
            }

            var audioSource = inst.pool.RequestAudioSource();
            var token = new ZoundToken(zound, audioSource);
            inst.tokens.Add(token);

            if (!inst.cullingGroups.TryGetValue(zound, out var zoundTokenList)) {
                zoundTokenList = new LinkedList<ZoundToken>();
                inst.cullingGroups.Add(zound, zoundTokenList);
            }
            if (zoundTokenList.Count >= projectSettings.maxPlayedZoundInstances) {
                var dequeuedToken = zoundTokenList.First.Value;
                zoundTokenList.RemoveFirst();
                dequeuedToken.FadeAndKill(0.4f);
            }
            zoundTokenList.AddLast(token);

            token.Start();
            return token;
        }

        public static float GetMasterVolume() {
            return masterVolume;
        }

        public static float GetRemainingCooldownTime(Zound zound) {
            if (Instance.zoundLastPlayedTimes.TryGetValue(zound, out float lastPlayedTime)) {
                float cooldownDuration = ZoundsProject.Instance.projectSettings.cooldownDuration;
                float delta = Time.realtimeSinceStartup - lastPlayedTime;
                float remainingTime = cooldownDuration - delta;
                if (remainingTime < 0) return 0f;
                else return remainingTime;
            }
            else {
                return 0f;
            }
        }

        private void OnEnable() {
            if (instance == null) {
                instance = this;
#if UNITY_EDITOR
                DetermineUpdater();
#else //Use runtime updater in build
                UseRuntimeUpdater();
#endif
            }
        }

        private void OnUpdate() {
            var projectSettings = ZoundsProject.Instance.projectSettings;
            masterVolume = Application.isPlaying ? projectSettings.playerVolume : projectSettings.editorVolume;
            masterVolume *= projectSettings.systemVolumeModifier;

            List<int> removedIndices = null; // only allocate the list if there's at least 1 token being killed.

            for (int i=0; i<tokens.Count; i++) {
                ZoundToken token = tokens[i];
                if (token.state == ZoundToken.State.Killed) {
                    if (removedIndices == null) removedIndices = new List<int>();
                    removedIndices.Add(i);
                    continue;
                }

                token.OnUpdate();
            }

            if (removedIndices != null) {
                removedIndices.Reverse();
                foreach (var index in removedIndices) {
                    var token = tokens[index];
                    if (cullingGroups.TryGetValue(token.zound, out var zoundTokenList)) {
                        zoundTokenList.Remove(token);
                    }
                    pool.ReturnAudioSource(token.audioSource);
                    tokens.RemoveAt(index);
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && tokens.Count > 0) {
                EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        private static void UseRuntimeUpdater() {
            RuntimeUpdater.Instance.onUpdate = Instance.OnUpdate;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditMode() {
            AssemblyReloadEvents.afterAssemblyReload += DetermineUpdater;
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        }

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange stateChange) {
            if (instance == null) return;

            if (stateChange == PlayModeStateChange.ExitingEditMode || stateChange == PlayModeStateChange.EnteredEditMode) {
                System.Action<UnityEngine.Object> destroyHandler;
                if (Application.isPlaying) destroyHandler = GameObject.Destroy;
                else destroyHandler = GameObject.DestroyImmediate;

                destroyHandler(instance.gameObject);
                instance = null;
                Debug.Log("Destroyed: " + stateChange);
            }

            if (stateChange == PlayModeStateChange.EnteredPlayMode) {
                //Debug.Log("Enter Play Mode, Is Playing: " + Application.isPlaying);
                DetermineUpdater();
            }
            else if (stateChange == PlayModeStateChange.EnteredEditMode) {
                //Debug.Log("Enter Edit Mode, Is Playing: " + Application.isPlaying);
                DetermineUpdater();

            }
        }

        internal static void DetermineUpdater() {
            if (instance == null) return;
            if (Application.isPlaying) {
                UnityEditor.EditorApplication.update -= instance.OnEditorUpdateMode;
                UseRuntimeUpdater();
            }
            else {
                UnityEditor.EditorApplication.update += instance.OnEditorUpdateMode;
            }
        }

        private void OnEditorUpdateMode() {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            OnUpdate();
        }
#endif

    }

}