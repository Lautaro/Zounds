using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using Zounds;

public class TestZounds : MonoBehaviour {

    public TextAsset jsonProject;
    public AudioMixer mixer;

    void Start() {
        // Warning: only call LoadFromJSON in build,
        // because calling it in editor will reset your unsaved data
#if !UNITY_EDITOR
        ZoundsProject.LoadFromJSON(jsonProject);
#endif

        Init();
    }

    private async void Init() {
        float startTime = Time.realtimeSinceStartup;

        await ZoundEngine.InitializeAsync();
        //ZoundEngine.Initialize();

        Debug.Log("Zounds async load time: " + (Time.realtimeSinceStartup - startTime));

        StartCoroutine(TestCoroutine());
    }

    private IEnumerator TestCoroutine() {

        for (int i = 0; i < 100; i++) {
            ZoundEngine.PlayZound("Golem Lord");
            yield return new WaitForSeconds(0.05f);
        }

        for (int i = 0; i < 100; i++) {
            ZoundEngine.PlayZound("Purple Prisoner Grab 1");
            yield return new WaitForSeconds(0.05f);
        }

        yield return null;

        ZoundEngine.PlayZound("Jester Major");

        yield return null;

        for (int i = 0; i < 100; i++) {
            ZoundEngine.PlayZound("Slime Bite Smol 1");
            yield return new WaitForSeconds(0.05f);
        }

        yield return null;

        for (int i = 0; i < 100; i++) {
            ZoundEngine.PlayZound("Grumpy Woman");
            yield return new WaitForSeconds(0.05f);
        }
    }

}
