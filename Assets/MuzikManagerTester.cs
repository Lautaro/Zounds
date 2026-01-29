using UnityEngine;
using UnityEngine.InputSystem;
using Zounds;

public class MuzikManagerTester : MonoBehaviour
{
    [SerializeField] private TextAsset json;
    [SerializeReference] public MuzikManager muzikManager;

    public void Awake()
    {
#if !UNITY_EDITOR
        ZoundsProject.LoadFromJSON(json);
#endif
        ZoundEngine.Initialize();
        Debug.Log("[ZOUNDS INIT]  = " + ZoundEngine.IsInitialized());
        muzikManager.Setup();
    }

    void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            ZoundEngine.PlayZound("Jester");

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            muzikManager.PlayCombatMusic();

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            muzikManager.PlayFactionMusic();

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            ZoundEngine.PlayZound("Click_Mouse");

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            ZoundEngine.PlayZound("Grunt_1");
    }

}
