using Sirenix.OdinInspector;
using UnityEngine;
using Zounds;

public class TestZAPI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [Button()]
    void Setup()
    {
        token1 = ZoundEngine.GetZoundToken(Zound1);
        token2 = ZoundEngine.GetZoundToken(Zound2);

    }

    [ShowInInspector] public string Zound1 = "Music Overworld 5";
    [ShowInInspector] public string Zound2 = "Music Overworld 3";
    public float duration = 2f;

    ZoundToken token1;
    ZoundToken token2;

    [Button()]
    async void Play1Then2Async()
    {
        await token1.PlayAsync(1);
        await token1.KillAsync(1);
        await token2.PlayAsync(1);
        await token2.KillAsync(1);
    }

    [Button()]
    void Play1()
    {
        token1 = ZoundEngine.PlayZound(Zound1);
        if (token2 != null && token2.state != ZoundToken.State.Killed)
        {
            token2.Pause();
        }
    }
    [Button()]
    void Play2()
    {
        token2 = ZoundEngine.PlayZound(Zound2);
        if (token1 != null && token1.state != ZoundToken.State.Killed)
        {
            token1.Pause();
        }
    }

    [Button]
    void PlayPause1()
    {
        if (token1.state == ZoundToken.State.Playing)
        {
            token1.Pause(duration);
        }
        else
        {
            token1.Unpause(duration);
        }
    }
    [Button]
    void PlayPause2()
    {
        if (token2.state == ZoundToken.State.Playing)
        {
            token2.Pause(duration);
        }
        else
        {
            token2.Unpause(duration);
        }
    }

    [Button()]
    void Mix()
    {
        if (token1 == null || token2 == null || token1.state == ZoundToken.State.Killed || token2.state == ZoundToken.State.Killed)
            Setup();

        if (token1.state == ZoundToken.State.Playing)
        {
            MixInternal(token1, token2);
        }
        else if (token2.state == ZoundToken.State.Playing)
        {
            MixInternal(token2, token1);
        }

        void MixInternal(ZoundToken playing, ZoundToken paused)
        {
            paused.Unpause(duration);
            playing.Pause(duration);
        }
    }
}
