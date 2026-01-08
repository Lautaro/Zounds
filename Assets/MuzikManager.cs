using Zounds;
using UnityEngine;
using System;
using Sirenix.OdinInspector;
[Serializable]
public class MuzikManager
{
    public ZoundToken OverworldMusic;
    public ZoundToken FactionMusic;
    public ZoundToken CombatMusic;
    public ZoundToken AiTurnMusic;
    public ZoundToken CombatSetupMusic;
    ZoundToken currentlyPlaying;
    public MuzikState State = MuzikState.None;
    public enum MuzikState
    {
        None,
        Overworld,
        Faction,
        Combat,
        CombatSetup,
        AiTurn
    }


    public MuzikManager()
    {
        Setup();
    }
    [Button]
    public void Setup()
    {
        OverworldMusic = ZoundEngine.GetZoundToken("Music Overworld");
        CombatMusic = ZoundEngine.GetZoundToken("Music Combat");
        FactionMusic = ZoundEngine.GetZoundToken("Music Faction");
        CombatSetupMusic = ZoundEngine.GetZoundToken("Music Combat Setup");
        AiTurnMusic = ZoundEngine.GetZoundToken("Music AI Turn");
    }

    void TryPauseCurrent()
    {
        if (currentlyPlaying != null && currentlyPlaying.state == ZoundToken.State.Playing)
            currentlyPlaying.Pause();
    }

    void UnpauseOrPlayCurrent()
    {
        if (currentlyPlaying == null)
            return;

        if (currentlyPlaying.state == ZoundToken.State.Killed)
            currentlyPlaying = ZoundEngine.GetZoundToken(currentlyPlaying.zound.name);

        if (currentlyPlaying.state == ZoundToken.State.Paused)
            currentlyPlaying.Unpause();
        else
            currentlyPlaying.Play();
    }

    void SwitchCurrent(ZoundToken token, MuzikState newState)
    {
        //if (currentlyPlaying != null)
        //    currentlyPlaying.Kill();

        //currentlyPlaying = ZoundEngine.PlayZound(token.zound.name);
        //return;
        TryPauseCurrent();
        currentlyPlaying = token;
        UnpauseOrPlayCurrent();
    }

    public void DebugCurrent()
    {
        Debug.Log(currentlyPlaying.zound.name + " = " + currentlyPlaying.state);
    }
    [Button]
    public void PlayOverworldMusic()
    {
        SwitchCurrent(OverworldMusic, MuzikState.Overworld);
    }
    [Button]
    public void PlayCombatMusic()
    {
        SwitchCurrent(CombatMusic, MuzikState.Combat);
    }
    [Button]
    public void PlayFactionMusic()
    {
        SwitchCurrent(FactionMusic, MuzikState.Faction);
    }

    [Button]
    public void PlayCombatSetupMusic()
    {
        SwitchCurrent(CombatSetupMusic, MuzikState.CombatSetup);
    }

    [Button]
    internal void PlayAiTurnMusic()
    {
        SwitchCurrent(AiTurnMusic, MuzikState.AiTurn);
    }
}