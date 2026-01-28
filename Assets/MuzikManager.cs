using Zounds;
using UnityEngine;
using System;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[Serializable]
public class MuzikManager
{

    public enum MuzikState
    {
        None,
        Overworld,
        Faction,
        Combat,
        CombatSetup,
        AiTurn
    }

    private Dictionary<MuzikState, ZoundToken> stateDictionary;

    private ZoundToken currentlyPlaying;
    private MuzikState state = MuzikState.None;
    private float fadeDuration = 1.0f;

    public MuzikState State => state;

    [Button]
    public void Setup()
    {
        stateDictionary = new Dictionary<MuzikState, ZoundToken>() {
            { MuzikState.Overworld,     ZoundEngine.GetZoundToken("Music Overworld") },
            { MuzikState.Combat,        ZoundEngine.GetZoundToken("Music Combat") },
            { MuzikState.Faction,       ZoundEngine.GetZoundToken("Music Faction") },
            { MuzikState.CombatSetup,   ZoundEngine.GetZoundToken("Music Combat Setup") },
            { MuzikState.AiTurn,        ZoundEngine.GetZoundToken("Music AI Turn") },
        };
        Debug.Log("MuzikManager Setup Complete. " + stateDictionary.Count + " states initialized.");
    }

    void TryPauseCurrent()
    {
        if (currentlyPlaying != null && currentlyPlaying.state == ZoundToken.State.Playing)
            currentlyPlaying.Pause(fadeDuration);
    }

    void UnpauseOrPlayCurrent()
    {
        if (currentlyPlaying == null)
            return;

        if (currentlyPlaying.state == ZoundToken.State.Killed)
            currentlyPlaying = ZoundEngine.GetZoundToken(currentlyPlaying.zound.name);

        if (currentlyPlaying.state == ZoundToken.State.Paused)
            currentlyPlaying.Unpause(fadeDuration);
        else
            currentlyPlaying.Play();
    }

    void SwitchCurrent(MuzikState newState)
    {
        TryPauseCurrent();
        state = newState;
        if (stateDictionary.TryGetValue(newState, out ZoundToken token))
        {
            currentlyPlaying = token;
            UnpauseOrPlayCurrent();
        }
    }

    public void DebugCurrent()
    {
        string zoundName = currentlyPlaying == null ? "None" : (currentlyPlaying.zound.name + " = " + currentlyPlaying.state);
        Debug.Log($"State: {state}, Zound: {zoundName}");
    }

    [Button]
    public void PlayOverworldMusic()
    {
        SwitchCurrent(MuzikState.Overworld);
    }

    [Button]
    public void PlayCombatMusic()
    {
        SwitchCurrent(MuzikState.Combat);
    }

    [Button]
    public void PlayFactionMusic()
    {
        SwitchCurrent(MuzikState.Faction);
    }

    [Button]
    public void PlayCombatSetupMusic()
    {
        SwitchCurrent(MuzikState.CombatSetup);
    }

    [Button]
    internal void PlayAiTurnMusic()
    {
        SwitchCurrent(MuzikState.AiTurn);
    }


}