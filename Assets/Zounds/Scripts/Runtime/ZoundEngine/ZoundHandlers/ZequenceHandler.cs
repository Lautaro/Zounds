using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class ZequenceHandler : ZoundHandler<Zequence> {

        public class RuntimeZoundEntry {
            public int zoundId;
            public ZoundToken token;
            public Zequence.ZoundEntry entryData;
        }

        private List<RuntimeZoundEntry> runtimeZoundEntries;


        public ZequenceHandler(Zequence zequence, AudioSource audioSource, ZoundArgs zoundArgs) : base(zequence, audioSource, zoundArgs) {
            audioSource.clip = null;
            runtimeZoundEntries = new List<RuntimeZoundEntry>();
            foreach (var entry in zequence.zoundEntries) {
                var runtimeEntry = new RuntimeZoundEntry() {
                    zoundId = entry.zoundId,
                    entryData = entry
                };
                runtimeZoundEntries.Add(runtimeEntry);
            }
        }

        public override void OnPause() {
            base.OnPause();
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                    runtimeEntry.token.Pause();
                }
            }
        }

        public override void OnResume() {
            base.OnResume();
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                    runtimeEntry.token.Resume();
                }
            }
        }

        public override void OnKill() {
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token != null) {
                    runtimeEntry.token.Kill();
                }
            }
            base.OnKill();
        }

        public override void OnFadeAndKill(float fadeDuration) {
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token != null) {
                    runtimeEntry.token.FadeAndKill(fadeDuration);
                }
            }
            base.OnFadeAndKill(fadeDuration);
        }

        protected override float PrepareAndCalculateDuration() {
            float duration = 0f;
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (!ZoundDictionary.TryGetZoundById(runtimeEntry.zoundId, out var childZound)) {
                    continue;
                }

                if (childZound is Zequence zeq && CheckRecursiveness(zeq, zound)) {
                    Debug.LogError(zound.name + " is contained recursively in " + zeq.name);
                    continue;
                }
                var data = runtimeEntry.entryData;

                float parentVolumeOverride = args.volumeOverride >= 0f ? args.volumeOverride : 1f;
                float parentPitchOverride = args.pitchOverride >= 0f ? args.pitchOverride : 1f;
                float parentChanceOverride = args.chanceOverride >= 0f ? args.chanceOverride : 1f;

                float volumeOverride;
                if (data.overrideVolume) {
                    volumeOverride = parentVolumeOverride * data.volume;
                }
                else {
                    if (useFixedAverageVolumeAndPitch) {
                        volumeOverride = parentVolumeOverride * data.volume * ((childZound.minVolume + childZound.maxVolume) / 2f);
                    }
                    else {
                        volumeOverride = parentVolumeOverride * data.volume * Random.Range(childZound.minVolume, childZound.maxVolume);
                    }
                }

                float pitchOverride;
                if (data.overridePitch) {
                    pitchOverride = parentPitchOverride * data.pitch;
                }
                else {
                    if (useFixedAverageVolumeAndPitch) {
                        pitchOverride = parentPitchOverride * data.pitch * ((childZound.minPitch + childZound.maxPitch) / 2f);
                    }
                    else {
                        pitchOverride = parentPitchOverride * data.pitch * Random.Range(childZound.minPitch, childZound.maxPitch);
                    }
                }

                //Debug.Log(zound.name + "." + childZound.name + ": " + pitchOverride);

                var entryArgs = new ZoundArgs() {
                    startImmediately = false,
                    delay = data.delay,
                    volumeOverride = volumeOverride,
                    pitchOverride = pitchOverride,
                    chanceOverride = data.overrideChance ? parentChanceOverride * data.chance : parentChanceOverride * data.chance * childZound.chance,
                    useFixedAverageValues = useFixedAverageVolumeAndPitch,
                    isChild = true
                };

                runtimeEntry.token = ZoundEngine.PlayZound(childZound, entryArgs);
                //float effectiveDuration = GetEntryDuration(runtimeEntry, entryArgs.pitchOverride) + entryArgs.delay;
                float effectiveDuration = runtimeEntry.token.duration + entryArgs.delay;
                if (effectiveDuration > duration) {
                    duration = effectiveDuration;
                }

            }

            //Debug.Log(zound.name + " duration: " + duration);

            return duration;
        }

        protected override void OnPlayReady(float timeStartOffset) {
            base.OnPlayReady(timeStartOffset);
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                    runtimeEntry.token.Start(timeStartOffset);
                }
            }
        }

        //private static float GetEntryDuration(RuntimeZoundEntry runtimeEntry, float runtimePitch) {
        //    if (!ZoundDictionary.TryGetZoundById(runtimeEntry.entryData.zoundId, out var z)) return 0f;

        //    float zoundDuration;
        //    if (z is Klip klip) {
        //        var audioClip = ZoundDictionary.GetOrLoadClip(klip.GetAudioClipReference());
        //        if (audioClip != null) {
        //            zoundDuration = audioClip.length / runtimePitch;
        //        }
        //        else {
        //            zoundDuration = 0f;
        //        }
        //    }
        //    else if (z is Zequence zequence) {
        //        zoundDuration = runtimeEntry.token.duration /* / runtimePitch*/;
        //    }
        //    else if (z is Muzic muzic) {
        //        zoundDuration = 0f;
        //        Debug.LogError("Duration calculator for Muzic is not yet implemented.");
        //    }
        //    else if (z is Randomizer randomizer) {
        //        zoundDuration = 0f;
        //        Debug.LogError("Duration calculator for Randomizer is not yet implemented.");
        //    }
        //    else {
        //        zoundDuration = 0f;
        //    }

        //    return zoundDuration;
        //}

        public static bool CheckRecursiveness(Zequence parentTarget, Zequence childToSearch) {
            foreach (var entry in parentTarget.zoundEntries) {
                if (!ZoundDictionary.TryGetZoundById(entry.zoundId, out var z)) continue;
                if (z is Zequence zeq) {
                    if (zeq.id == childToSearch.id) {
                        return true;
                    }
                    else {
                        if (zeq.id != childToSearch.id) {
                            CheckRecursiveness(zeq, childToSearch);
                        }
                    }
                }
            }
            return false;
        }

    }

}
