using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine;
using Zounds;

public class TestZounds : MonoBehaviour {

    [Button]
    public void Init()
    {
        ZoundEngine.Initialize();

    }

    [Button]
  public void PlayZound(string zoundName) {
        
        
        ZoundEngine.PlayZound(zoundName);
        
    }
}
