using Sirenix.OdinInspector;
using System;
using UnityEngine;
using Zounds;

public class TestZAPI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    [Button("Create Klip")]
    void CrateKlip(string klipName, AudioClip ac)
    {
        var klip = ZoundAPI.CreateKlip(ac, klipName);

        ZoundAPI.AddTagToZound(klip, "API Made", DateTime.Now.ToShortTimeString());
    }
}
