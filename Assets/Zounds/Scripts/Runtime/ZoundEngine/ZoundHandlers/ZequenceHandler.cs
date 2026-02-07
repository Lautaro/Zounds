using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    internal class ZequenceHandler : ZoundHandler<Zequence> {

        public class RuntimeZoundEntry {
            public ZoundToken token;
            public Zequence.ZoundEntry entryData;
        }

        private List<RuntimeZoundEntry> runtimeZoundEntries;

        private int entryIndexToPlay;
        private bool m_isRealtime;

        public override int playedEntryIndex => entryIndexToPlay;

        public override bool isRealtime => m_isRealtime;

        internal ZoundToken GetEntryToken(CompositeZound.ZoundEntry entry) {
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.entryData == entry) {
                    if (runtimeEntry.token != null) {
                        return runtimeEntry.token;
                    }
                    break;
                }
            }
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token == null) continue;
                if (runtimeEntry.token.TryGetEntryToken(entry, out var childToken)) {
                    return childToken;
                }
            }
            return null;
        }

        public bool IsEntryMuted(CompositeZound.ZoundEntry entry) {
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.entryData == entry) {
                    if (runtimeEntry.token != null && runtimeEntry.token.audioSource != null) {
                        return runtimeEntry.token.audioSource.mute;
                    }
                    break;
                }
            }
            return true;
        }

        public override List<AudioSource> GetAudioSources() {
            var result = base.GetAudioSources();
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token != null) {
                    result.AddRange(runtimeEntry.token.audioSources);
                }
            }
            return result;
        }

        public ZequenceHandler(Zequence zequence, AudioSource audioSource, ZoundArgs zoundArgs) : base(zequence, audioSource, zoundArgs) {
            var renderedClip = zequence.renderedClipRef == null || !zequence.renderedClipRef.RuntimeKeyIsValid() ? null : ZoundDictionary.GetOrLoadClip(zequence.renderedClipRef);
            m_isRealtime = ReferenceEquals(renderedClip, null);
            audioSource.clip = renderedClip;

            if (!zoundArgs.overrideMixerGroup) {
                var zoundRoutings = ZoundsProject.Instance.zoundRoutings;
                var mixerGroup = zoundRoutings.GetRouting(zound
#if ZOUNDS_CONSIDER_FOLDERS
                , null, null
#endif
                    );
                audioSource.outputAudioMixerGroup = mixerGroup;
            }

            if (m_isRealtime) {
                InitRuntimeZoundEntries(zequence);
            }
        }

        private void InitRuntimeZoundEntries(Zequence zequence) {
            runtimeZoundEntries = new List<RuntimeZoundEntry>();
            foreach (var entry in zequence.zoundEntries) {
                var runtimeEntry = new RuntimeZoundEntry() {
                    entryData = entry
                };
                runtimeZoundEntries.Add(runtimeEntry);
            }

            if (runtimeZoundEntries.Count > 0) {
                if (zequence.mode != CompositeZound.Mode.Parallel && args.soloOverride != null) {
                    entryIndexToPlay = zequence.zoundEntries.FindIndex(e => e == args.soloOverride);
                }
                else if (zequence.mode == CompositeZound.Mode.Randomizer) {
                    int totalWeight = zequence.noPlayWeight;
                    for (int i = 0; i < zequence.zoundEntries.Count; i++) {
                        var entry = zequence.zoundEntries[i];
                        totalWeight += entry.chanceWeight;
                    }
                    int accumulativeWeight = zequence.noPlayWeight;
                    int rand = Random.Range(0, totalWeight);
                    if (rand < zequence.noPlayWeight) {
                        entryIndexToPlay = -1;
                        Debug.Log(string.Format("No Play ({0} / {1})", rand, totalWeight));
                    }
                    else {
                        for (int i = 0; i < zequence.zoundEntries.Count; i++) {
                            var entry = zequence.zoundEntries[i];
                            accumulativeWeight += entry.chanceWeight;
                            if (rand < accumulativeWeight) {
                                entryIndexToPlay = i;
                                Debug.Log(string.Format("Playing Index {0} ({1} / {2})", entryIndexToPlay, rand, totalWeight));
                                break;
                            }
                        }
                    }
                }
                else if (zequence.mode == CompositeZound.Mode.RoundRobin) {
                    if (zequence.playedEntries.Count == runtimeZoundEntries.Count) {
                        zequence.playedEntries.Clear();
                    }
                    do {
                        entryIndexToPlay = Random.Range(0, runtimeZoundEntries.Count);
                    } while (zequence.playedEntries.Contains(entryIndexToPlay));
                    zequence.playedEntries.Add(entryIndexToPlay);
                }
                else if (zequence.mode == CompositeZound.Mode.Playlist) {
                    if (zequence.currentEntryIndexToPlay >= runtimeZoundEntries.Count) {
                        zequence.currentEntryIndexToPlay = 0;
                    }
                    entryIndexToPlay = zequence.currentEntryIndexToPlay;
                    zequence.currentEntryIndexToPlay++;
                }
            }
        }

        public override void OnPause() {
            base.OnPause();
            if (!m_isRealtime) return;
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                    runtimeEntry.token.Pause();
                }
            }
        }

        public override void OnResume(float fadeDuration, System.Action onFadeComplete) {
            base.OnResume(fadeDuration, onFadeComplete);
            if (!m_isRealtime) return;
            if (zound.mode == CompositeZound.Mode.Parallel) {
                foreach (var runtimeEntry in runtimeZoundEntries) {
                    if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                        runtimeEntry.token.Unpause(fadeDuration);
                    }
                }
            }
            else {
                if (entryIndexToPlay >= 0 && entryIndexToPlay < runtimeZoundEntries.Count) {
                    var runtimeEntry = runtimeZoundEntries[entryIndexToPlay];
                    if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                        runtimeEntry.token.Unpause(fadeDuration);
                    }
                }
            }
        }

        public override void OnFadeAndPause(float fadeDuration, System.Action onFadeComplete) {
            if (m_isRealtime) {
                foreach (var runtimeEntry in runtimeZoundEntries) {
                    if (runtimeEntry.token != null) {
                        runtimeEntry.token.Pause(fadeDuration);
                    }
                }
            }
            base.OnFadeAndPause(fadeDuration, onFadeComplete);
        }

        public override void OnKill() {
            if (m_isRealtime) {
                foreach (var runtimeEntry in runtimeZoundEntries) {
                    if (runtimeEntry.token != null) {
                        runtimeEntry.token.Kill();
                    }
                }
            }
            base.OnKill();
        }

        public override void OnFadeAndKill(float fadeDuration, System.Action onFadeComplete) {
            if (m_isRealtime) {
                foreach (var runtimeEntry in runtimeZoundEntries) {
                    if (runtimeEntry.token != null) {
                        runtimeEntry.token.Kill(fadeDuration);
                    }
                }
            }
            base.OnFadeAndKill(fadeDuration, onFadeComplete);
        }

        protected override float PrepareAndCalculateDuration() {
            if (!m_isRealtime) {
                return base.PrepareAndCalculateDuration();
            }

            float duration = 0f;
            int i = -1;
            foreach (var runtimeEntry in runtimeZoundEntries) {
                i++;
                if (zound.mode != CompositeZound.Mode.Parallel) {
                    if (i != entryIndexToPlay) continue;
                }

                if (!zound.TryGetEntryZound(runtimeEntry.entryData, out var childZound)) {
                    continue;
                }

                if (childZound is Zequence zeq && CheckRecursiveness(zeq, zound)) {
                    Debug.LogError(zound.name + " is contained recursively in " + zeq.name);
                    continue;
                }
                var data = runtimeEntry.entryData;

                float parentVolumeOverride = args.volumeOverride >= 0f ? args.volumeOverride : audioSource.volume;
                float parentPitchOverride = args.pitchOverride >= 0f ? args.pitchOverride : audioSource.pitch;
                float parentChanceOverride = args.chanceOverride >= 0f ? args.chanceOverride : zound.chance;

                float volumeOverride;
                if (data.overrideVolume) {
                    volumeOverride = parentVolumeOverride * data.volume;
                }
                else {
                    //if (useFixedAverageVolumeAndPitch) {
                    //    volumeOverride = parentVolumeOverride * data.volume * ((childZound.minVolume + childZound.maxVolume) / 2f);
                    //}
                    //else {
                    //    volumeOverride = parentVolumeOverride * data.volume * Random.Range(childZound.minVolume, childZound.maxVolume);
                    //}
                    // no more middle values
                    volumeOverride = parentVolumeOverride * data.volume * Random.Range(childZound.minVolume, childZound.maxVolume);
                }

                Debug.Log(zound.name + " Set Volume Override: " + volumeOverride);

                float pitchOverride;
                if (data.overridePitch) {
                    pitchOverride = parentPitchOverride * data.pitch;
                }
                else {
                    //if (useFixedAverageVolumeAndPitch) {
                    //    pitchOverride = parentPitchOverride * data.pitch * ((childZound.minPitch + childZound.maxPitch) / 2f);
                    //}
                    //else {
                    //    pitchOverride = parentPitchOverride * data.pitch * Random.Range(childZound.minPitch, childZound.maxPitch);
                    //}
                    // no more middle values
                    pitchOverride = parentPitchOverride * data.pitch * Random.Range(childZound.minPitch, childZound.maxPitch);
                }

                //Debug.Log(zound.name + "." + childZound.name + ": " + pitchOverride);

                CompositeZound.ZoundEntry soloOverride = null;
                if (args.soloOverride != null && childZound is Zequence childZeq && childZeq.zoundEntries.Find(e => e == args.soloOverride) != null) {
                    soloOverride = args.soloOverride;
                }

                var entryArgs = new ZoundArgs() {
                    startImmediately = false,
                    delay = data.delay / parentPitchOverride,
                    volumeOverride = volumeOverride,
                    pitchOverride = pitchOverride,
                    chanceOverride = data.overrideChance ? parentChanceOverride * data.chance : parentChanceOverride * data.chance * childZound.chance,
                    useFixedAverageValues = useFixedAverageVolumeAndPitch,
                    isChild = true,
                    soloOverride = soloOverride,
                    bypassGlobalSolo = true,
                    ignoreCooldown = args.ignoreCooldown
                };

                runtimeEntry.token = ZoundEngine.PlayZound(childZound, entryArgs);
                //float effectiveDuration = GetEntryDuration(runtimeEntry, entryArgs.pitchOverride) + entryArgs.delay;
                float effectiveDuration;
                if (runtimeEntry.token == null) {
                    effectiveDuration = 0f;
                }
                else {
                    effectiveDuration = runtimeEntry.token.duration + entryArgs.delay;
                }
                if (effectiveDuration > duration) {
                    duration = effectiveDuration;
                }

            }

            //Debug.Log(zound.name + " duration: " + duration);

            return duration;
        }

        public override void ApplyMixerGroupToChildren(AudioMixerGroup mixerGroup) {
            base.ApplyMixerGroupToChildren(mixerGroup);
            if (!m_isRealtime) return;
            foreach (var runtimeEntry in runtimeZoundEntries) {
                if (runtimeEntry.token == null) continue;
                runtimeEntry.token.ApplyMixerGroupToChildren(mixerGroup);
            }
        }

        protected override void OnPlayReady(float timeStartOffset, float childFadeDuration) {
            base.OnPlayReady(timeStartOffset, childFadeDuration);
            if (!m_isRealtime) return;
            if (zound.mode == CompositeZound.Mode.Parallel) {
                foreach (var runtimeEntry in runtimeZoundEntries) {
                    if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                        runtimeEntry.token.Start(timeStartOffset, childFadeDuration);
                    }
                }
            }
            else {
                if (entryIndexToPlay >= 0 && entryIndexToPlay < runtimeZoundEntries.Count) {
                    var runtimeEntry = runtimeZoundEntries[entryIndexToPlay];
                    if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                        runtimeEntry.token.Start(timeStartOffset, childFadeDuration);
                    }
                }
            }
        }

        public override int OnUpdate(float deltaDspTime) {
            if (!m_isRealtime) {
                return base.OnUpdate(deltaDspTime);
            }

            UpdateChildrenEnvelopeVolumes();

            int nextTreatment = base.OnUpdate(deltaDspTime);
            bool killed = nextTreatment == 1;
            if (!killed) {
                if (args.soloOverride != null) {
                    foreach (var runtimeEntry in runtimeZoundEntries) {
                        if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                            bool shouldMute = runtimeEntry.entryData != args.soloOverride;
                            if (shouldMute) {
                                if (zound.TryGetEntryZound(runtimeEntry.entryData, out var childZound) && childZound is Zequence childZeq) {
                                    if (childZeq.zoundEntries.Find(e => e == args.soloOverride) != null) {
                                        shouldMute = false;
                                    }
                                }
                            }
                            runtimeEntry.token.audioSource.mute = shouldMute;
                        }
                    }
                }
                else {
                    bool hasAnySolo = false;
                    foreach (var runtimeEntry in runtimeZoundEntries) {
                        if (runtimeEntry.entryData.solo) {
                            hasAnySolo = true;
                            break;
                        }
                    }

                    if (hasAnySolo) {
                        foreach (var runtimeEntry in runtimeZoundEntries) {
                            if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                                runtimeEntry.token.audioSource.mute = audioSource.mute || !runtimeEntry.entryData.solo;
                            }
                        }
                    }
                    else {
                        foreach (var runtimeEntry in runtimeZoundEntries) {
                            if (runtimeEntry.token != null && runtimeEntry.token.state != ZoundToken.State.Killed) {
                                runtimeEntry.token.audioSource.mute = audioSource.mute || runtimeEntry.entryData.mute;
                            }
                        }
                    }
                }
            }
            return nextTreatment;
        }

        private void UpdateChildrenEnvelopeVolumes() {
            var masterVolumeEnvelope = zound.masterVolumeEnvelope;
            float masterVolume;
            if (masterVolumeEnvelope != null && masterVolumeEnvelope.enabled) {
                masterVolume = parentVolume * masterVolumeEnvelope.Evaluate(currentTime / totalDuration);
            }
            else {
                masterVolume = parentVolume;
            }

            bool isMutedOrExcluded = IsMutedOrExcluded();

            foreach (var runtimeEntry in runtimeZoundEntries) {
                var runtimeToken = runtimeEntry.token;
                if (runtimeToken != null && runtimeToken.state != ZoundToken.State.Killed) {
                    if (isMutedOrExcluded) {
                        runtimeToken.parentVolume = 0f;
                    }
                    else {
                        var volumeEnvelope = runtimeEntry.entryData.volumeEnvelope;
                        float multiplier;
                        if (volumeEnvelope != null && volumeEnvelope.enabled) {
                            multiplier = masterVolume * volumeEnvelope.Evaluate(runtimeToken.time / runtimeToken.duration);
                        }
                        else {
                            multiplier = masterVolume;
                        }
                        //Debug.Log(zound.name + ": " + masterVolume + "  -->  " + runtimeToken.zound.name + ": " + runtimeToken.audioSource.volume + "  -->  " + (runtimeToken.audioSource.volume * multiplier), runtimeToken.audioSource);
                        runtimeToken.parentVolume = multiplier;
                    }
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

        public static bool CheckRecursiveness(CompositeZound parentTarget, CompositeZound childToSearch) {
            foreach (var entry in parentTarget.zoundEntries) {
                if (!ZoundDictionary.TryGetZoundById(entry.zoundId, out var z)) continue;
                if (z is CompositeZound cz) {
                    if (cz.id == childToSearch.id) {
                        return true;
                    }
                    else {
                        if (cz.id != childToSearch.id) {
                            CheckRecursiveness(cz, childToSearch);
                        }
                    }
                }
            }
            return false;
        }

    }

}
