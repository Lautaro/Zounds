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

        Debug.Log("Zounds async load time: " + (Time.realtimeSinceStartup - startTime));

        StartCoroutine(TestCoroutine());
    }

    private IEnumerator TestCoroutine() {
        ZoundEngine.PlayZound("Jester Major");

        for (int i = 0; i < 100; i++) {
            ZoundEngine.PlayZound("Grumpy Woman");
            yield return new WaitForSeconds(0.05f);
        }
    }

}
