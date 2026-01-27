using UnityEngine;
using UnityEngine.InputSystem;
using Zounds;

public class MuzikManagerTester : MonoBehaviour
{
  [SerializeReference]public MuzikManager muzikManager;

    void Update()
    {

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            muzikManager.PlayOverworldMusic();

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            muzikManager.PlayCombatMusic();

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            muzikManager.PlayFactionMusic();

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            ZoundEngine.PlayZound("Grunt 1");

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            ZoundEngine.PlayZound("Click_Mouse");
    }

}
