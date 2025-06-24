using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zounds;

public class TestZounds : MonoBehaviour {

    void Start() {
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
            ZoundEngine.PlayZound("Grumpy Woman");
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
