using UnityEngine;
using UnityEditor;
using System.IO;

#if ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
#endif

namespace Zounds {

    public static class AudioRenderUtility {

        public static AudioClip Trim(AudioClip clip, float startTime, float endTime) {
            if (clip == null) return null;

            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int startSample = Mathf.FloorToInt(startTime * sampleRate * channels);
            int endSample = Mathf.Min(Mathf.FloorToInt(endTime * sampleRate * channels), clip.samples * channels);
            int lengthSamples = endSample - startSample;

            if (lengthSamples <= 0) {
                Debug.LogError($"[Zounds] Trim failed: start ({startTime}s, {startSample} samples) is >= end ({endTime}s, {endSample} samples). Source clip '{clip.name}' is {clip.length}s ({clip.samples} samples).");
                return null;
            }

            float[] outputData = new float[lengthSamples];
            float[] fullData = new float[clip.samples * channels];
            bool success = clip.GetData(fullData, 0);
            if (!success) {
                Debug.LogError($"[Zounds] Trim: GetData failed for {clip.name}");
            }

            System.Array.Copy(fullData, startSample, outputData, 0, lengthSamples);

            AudioClip newClip = AudioClip.Create(clip.name + "_Trimmed", lengthSamples / channels, channels, sampleRate, false);
            newClip.SetData(outputData, 0);
            return newClip;
        }

        private static float GetMaxAmplitude(float[] data, int start, int end) {
            float max = 0;
            for(int i = Mathf.Max(0, start); i < Mathf.Min(data.Length, end); i++) {
                max = Mathf.Max(max, Mathf.Abs(data[i]));
                if (max > 0.99f) break;
            }
            return max;
        }


        public static AudioClip Combine(AudioClip[] clips, float[] startTimes) {
            if (clips == null || clips.Length == 0 || startTimes == null || startTimes.Length != clips.Length) {
                Debug.LogError("Invalid input: Ensure clips and startTimes array have the same length.");
                return null;
            }

            int channels = clips[0].channels;
            int sampleRate = clips[0].frequency;

            float maxDuration = 0f;
            for (int i = 0; i < clips.Length; i++) {
                float clipEndTime = startTimes[i] + clips[i].length;
                if (clipEndTime > maxDuration)
                    maxDuration = clipEndTime;
            }

            int totalSamples = Mathf.CeilToInt(maxDuration * sampleRate * channels);
            float[] outputData = new float[totalSamples];

            for (int i = 0; i < clips.Length; i++) {
                AudioClip clip = clips[i];
                int startSample = Mathf.FloorToInt(startTimes[i] * sampleRate * channels);
                float[] clipData = new float[clip.samples * channels];
                bool success = clip.GetData(clipData, 0);
                if (!success) {
                    Debug.LogError($"[Zounds] Combine: GetData failed for {clip.name}");
                }

                for (int j = 0; j < clipData.Length; j++) {
                    int targetIndex = startSample + j;
                    if (targetIndex < totalSamples) {
                        // mix by adding values (simple overlay)
                        outputData[targetIndex] += clipData[j];
                    }
                }
            }

            // normalize
            float maxAmplitude = 1f;
            for (int i = 0; i < outputData.Length; i++) {
                maxAmplitude = Mathf.Max(maxAmplitude, Mathf.Abs(outputData[i]));
            }
            if (maxAmplitude > 1f) {
                for (int i = 0; i < outputData.Length; i++) {
                    outputData[i] /= maxAmplitude;
                }
            }

            AudioClip newClip = AudioClip.Create(clips[0] + "_Combined", totalSamples / channels, channels, sampleRate, false);
            newClip.SetData(outputData, 0);
            return newClip;
        }


        public static AudioClip VolumeEnvelope(AudioClip clip, Envelope envelope, float startTime = 0, float endTime = 0) {
            if (clip == null || envelope == null) return null;

            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int totalSamples = clip.samples;
            float duration = clip.length;

            float[] outputData = new float[totalSamples * channels];
            bool success = clip.GetData(outputData, 0);
            if (!success) {
                Debug.LogError($"[Zounds] VolumeEnvelope: GetData failed for {clip.name}");
            }

            // If we are NOT clamping, the envelope spans the whole clip duration
            bool useClamping = startTime != 0 || endTime != 0;

            for (int i = 0; i < totalSamples; i++) {
                float t;

                if (useClamping) {
                    float currentTime = (float)i / sampleRate;
                    // If we are clamped to trim, 't' is 0.0 at startTime and 1.0 at endTime
                    float trimDuration = endTime - startTime;
                    if (currentTime < startTime || currentTime > endTime) {
                        t = -1; // Outside envelope range
                    } else {
                        t = (currentTime - startTime) / trimDuration;
                    }
                } else {
                    // Use totalSamples - 1 to ensure t is exactly 1.0 at the last sample
                    t = (float)i / (totalSamples - 1);
                }

                if (t >= 0 && t <= 1) {
                    float volumeFactor = envelope.Evaluate(t);
                    for (int c = 0; c < channels; c++) {
                        int index = i * channels + c;
                        outputData[index] *= volumeFactor;
                    }
                }
            }

            AudioClip newClip = AudioClip.Create(clip.name + "_VolumeEnveloped", totalSamples, channels, sampleRate, false);
            newClip.SetData(outputData, 0);
            return newClip;
        }

        public static AudioClip PitchEnvelope(AudioClip clip, Envelope envelope, float startTime = 0, float endTime = 0) {
            if (clip == null || envelope == null) return null;

            float[] sourceSamples = new float[clip.samples * clip.channels];
            bool success = clip.GetData(sourceSamples, 0);
            if (!success) {
                Debug.LogError($"[Zounds] PitchEnvelope: GetData failed for {clip.name}");
            }
            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int sourceSampleCount = clip.samples;

            bool useClamping = startTime != 0 || endTime != 0;
            float totalOutputDuration;
            
            if (useClamping) {
                // If clamped, we only care about the duration of the trimmed segment
                float durationInRange = endTime - startTime;
                float outputDurationInRange = CalculateOutputDuration(durationInRange, envelope);
                totalOutputDuration = (clip.length - durationInRange) + outputDurationInRange;
            } else {
                // If NOT clamped, the envelope affects the whole clip length
                totalOutputDuration = CalculateOutputDuration(clip.length, envelope);
            }

            int outputSampleCount = Mathf.CeilToInt(totalOutputDuration * sampleRate);
            if (outputSampleCount <= 0) outputSampleCount = 1;
            
            float[] outputSamples = new float[outputSampleCount * channels];

            float currentSourceTime = 0f;
            float dt = 1f / sampleRate;

            for (int i = 0; i < outputSampleCount; i++) {
                float pitch = 1f;
                float t;

                if (useClamping) {
                    float trimDuration = endTime - startTime;
                    if (currentSourceTime >= startTime && currentSourceTime <= endTime) {
                        t = (currentSourceTime - startTime) / trimDuration;
                        pitch = envelope.Evaluate(t);
                    }
                } else {
                    t = currentSourceTime / clip.length;
                    pitch = envelope.Evaluate(t);
                }

                pitch = Mathf.Max(0.01f, pitch);

                // Sample from source (with basic linear interpolation for better quality)
                float sourceSamplePos = currentSourceTime * sampleRate;
                
                // If we are at the end of the source but have more output samples to fill
                if (sourceSamplePos >= sourceSampleCount - 1) {
                    // We've reached the end of the source data, but the loop might still have output samples.
                    // This is where the cutoff was happening. We'll fill with silence or the last sample.
                    break; 
                }

                int idxA = Mathf.FloorToInt(sourceSamplePos);
                int idxB = idxA + 1;
                float lerpT = sourceSamplePos - idxA;

                for (int c = 0; c < channels; c++) {
                    float valA = sourceSamples[idxA * channels + c];
                    float valB = sourceSamples[idxB * channels + c];
                    outputSamples[i * channels + c] = Mathf.Lerp(valA, valB, lerpT);
                }

                currentSourceTime += dt * pitch;
            }

            AudioClip newClip = AudioClip.Create(clip.name + "_PitchEnveloped", outputSampleCount, channels, sampleRate, false);
            newClip.SetData(outputSamples, 0);
            
            return newClip;
        }

        public static AudioClip CutOffEnvelope(AudioClip clip, Envelope envelope, bool highPass, float resonance = 1f) {
            if (clip == null || envelope == null) return null;

            float[] audioData = new float[clip.samples * clip.channels];
            bool success = clip.GetData(audioData, 0);
            if (!success) {
                Debug.LogError($"[Zounds] CutOffEnvelope: GetData failed for {clip.name}");
            }

            BiQuadFilter filter = null;
            if (highPass) {
                filter = new HighPassFilter(clip.frequency, 10f);
            }
            else {
                filter = new LowPassFilter(clip.frequency, 22000f);
            }

            for (int i = 0; i < audioData.Length; i++) {
                float normalizedPosition = (float)i / audioData.Length;
                float cutOffValue = envelope.Evaluate(normalizedPosition);

                if (highPass) {
                    // 10Hz = no filtering, 22000Hz = max filtering

                    if (cutOffValue > 10) {
                        filter.SetCutoffFrequency(cutOffValue, resonance);
                        audioData[i] = filter.Process(audioData[i]);
                    }
                }
                else {
                    // 10Hz = max filtering, 22000Hz = no filtering

                    if (cutOffValue < 22000f) {
                        filter.SetCutoffFrequency(cutOffValue, resonance);
                        audioData[i] = filter.Process(audioData[i]);
                    }
                }
            }

            AudioClip newClip = AudioClip.Create(clip.name + "_CutOffEnveloped", clip.samples, clip.channels, clip.frequency, false);
            newClip.SetData(audioData, 0);
            return newClip;
        }


        #region PITCH-ENVELOPE
        public static float GetOutputTimeForSourceTime(float sourceTime, Envelope pitchEnvelope, float totalSourceDuration) {
            const int integrationSteps = 1000;
            float stepSize = totalSourceDuration / integrationSteps;
            float accumulatedOutputTime = 0f;

            for (int i = 0; i < integrationSteps; i++) {
                float currentSourceTime = i * stepSize;
                if (currentSourceTime >= sourceTime) break;
                
                float t = currentSourceTime / totalSourceDuration;
                float pitch = Mathf.Max(0.01f, pitchEnvelope.Evaluate(t));
                accumulatedOutputTime += stepSize / pitch;
            }

            return accumulatedOutputTime;
        }

        private static float CalculateOutputDuration(float sourceDuration, Envelope pitchEnvelope) {
            const int integrationSteps = 1000;
            float stepSize = 1f / integrationSteps;
            float totalTime = 0f;

            for (int i = 0; i < integrationSteps; i++) {
                float t = i * stepSize;
                float pitch = pitchEnvelope.Evaluate(t);
                pitch = Mathf.Max(pitch, 0.01f);
                totalTime += stepSize * sourceDuration / pitch;
            }

            return totalTime;
        }

        private static float GetSourceTimeForOutputTime(float outputTime, Envelope pitchEnvelope, float sourceDuration) {
            int integrationSteps = GetOptimalIntegrationSteps(sourceDuration);
            //Debug.Log("Integration Steps: " + integrationSteps);
            // integration steps is necessary since we can't interpolate because the pitch is changing overtime due to pitchEnvelope
            float stepSize = sourceDuration / integrationSteps;
            float accumulatedOutputTime = 0f;
            float accumulatedSourceTime = 0f;

            while (accumulatedSourceTime < sourceDuration && accumulatedOutputTime < outputTime) {
                float progress = accumulatedSourceTime / sourceDuration;
                float pitch = pitchEnvelope.Evaluate(progress);
                pitch = Mathf.Max(pitch, 0.01f);

                float segmentSourceTime = stepSize;
                float segmentOutputTime = segmentSourceTime / pitch;

                if (accumulatedOutputTime + segmentOutputTime >= outputTime) {
                    // target output time reached
                    float remainingOutputTime = outputTime - accumulatedOutputTime;
                    float correspondingSourceTime = remainingOutputTime * pitch;
                    accumulatedSourceTime += correspondingSourceTime;
                    break;
                }

                accumulatedOutputTime += segmentOutputTime;
                accumulatedSourceTime += segmentSourceTime;
            }

            return Mathf.Min(accumulatedSourceTime, sourceDuration);
        }

        public static int GetOptimalIntegrationSteps(float sourceDuration) {
            //int integrationSteps = 1000/*10000*/; // arbitrary. higher value, then longer iteration loop, but more detailed/high precision.
            return Mathf.Clamp(Mathf.CeilToInt(sourceDuration / 0.01f), 100, 10000); // 0.01: 10ms
        }

        private static float GetInterpolatedSample(float[] samples, float position, int sampleCount, int channelCount, int channel) {
            float exactSampleIndex = position * (sampleCount - 1) * channelCount + channel;
            int i = Mathf.FloorToInt(exactSampleIndex / channelCount) * channelCount + channel;
            float t = (exactSampleIndex - i) / channelCount;

            // Cubic Hermite Interpolation (Catmull-Rom)
            // Points: p0, p1, p2, p3
            int i0 = Mathf.Max(i - channelCount, channel);
            int i1 = i;
            int i2 = Mathf.Min(i + channelCount, samples.Length - channelCount + channel);
            int i3 = Mathf.Min(i + 2 * channelCount, samples.Length - channelCount + channel);

            float p0 = samples[i0];
            float p1 = samples[i1];
            float p2 = samples[i2];
            float p3 = samples[i3];

            // Formula: 0.5 * ((2*p1) + (-p0 + p2)*t + (2*p0 - 5*p1 + 4*p2 - p3)*t^2 + (-p0 + 3*p1 - 3*p2 + p3)*t^3)
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
            );
        }
        #endregion

        #region CUTOFF-ENVELOPE
        public abstract class BiQuadFilter {
            protected float a0, a1, a2, b1, b2;
            protected float in1, in2, out1, out2;
            protected float sampleRate;

            public abstract void SetCutoffFrequency(float cutoff, float resonance);

            public float Process(float input) {
                float output = (a0 * input) + (a1 * in1) + (a2 * in2) - (b1 * out1) - (b2 * out2);

                in2 = in1;
                in1 = input;
                out2 = out1;
                out1 = output;

                return output;
            }

            public float Bypass(float input) {
                in1 = in2 = out1 = out2 = 0;
                return input;
            }
        }

        public class LowPassFilter : BiQuadFilter {
            public LowPassFilter(float sampleRate, float cutoff, float resonance = 1f) {
                this.sampleRate = sampleRate;
                SetCutoffFrequency(cutoff, resonance);
            }

            public override void SetCutoffFrequency(float cutoff, float resonance = 1f) {

                // gotta get back later to fix this formula to work like this calculator:
                // https://www.earlevel.com/main/2013/10/13/biquad-calculator-v2/

                cutoff = Mathf.Clamp(cutoff, 10f, 22000f);
                float w = 2f * Mathf.PI * cutoff / sampleRate;
                float alpha = Mathf.Sin(w) / (2f * resonance);
                float cosW = Mathf.Cos(w);

                float norm = 1f / (1f + alpha);
                a0 = ((1f - cosW) / 2f) * norm;
                a1 = (1f - cosW) * norm;
                a2 = ((1f - cosW) / 2f) * norm;
                b1 = (-2f * cosW) * norm;
                b2 = (1f - alpha) * norm;
            }
        }

        public class HighPassFilter : BiQuadFilter {
            public HighPassFilter(float sampleRate, float cutoff, float resonance = 1f) {
                this.sampleRate = sampleRate;
                SetCutoffFrequency(cutoff, resonance);
            }

            public override void SetCutoffFrequency(float cutoff, float resonance = 1f) {
                cutoff = Mathf.Clamp(cutoff, 10f, 22000f);
                float w = 2f * Mathf.PI * cutoff / sampleRate;
                float alpha = Mathf.Sin(w) / (2f * resonance);
                float cosW = Mathf.Cos(w);

                float norm = 1f / (1f + alpha);
                a0 = ((1f + cosW) / 2f) * norm;
                a1 = -(1f + cosW) * norm;
                a2 = ((1f + cosW) / 2f) * norm;
                b1 = (-2f * cosW) * norm;
                b2 = (1f - alpha) * norm;
            }
        }

        public class PeakingEQFilter : BiQuadFilter {
            public PeakingEQFilter(float sampleRate, float frequency, float gainDB, float q = 1.0f) {
                this.sampleRate = sampleRate;
                SetParameters(frequency, gainDB, q);
            }

            public override void SetCutoffFrequency(float cutoff, float resonance) {
                SetParameters(cutoff, 0f, resonance);
            }

            public void SetParameters(float frequency, float gainDB, float q) {
                frequency = Mathf.Clamp(frequency, 10f, 22000f);
                float a = Mathf.Pow(10f, gainDB / 40f);
                float w = 2f * Mathf.PI * frequency / sampleRate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float cosW = Mathf.Cos(w);

                float norm = 1f / (1f + alpha / a);
                a0 = (1f + alpha * a) * norm;
                a1 = (-2f * cosW) * norm;
                a2 = (1f - alpha * a) * norm;
                b1 = (-2f * cosW) * norm;
                b2 = (1f - alpha / a) * norm;
            }
        }

        public static AudioClip ApplyEqualizer(AudioClip clip, float subGain, float lowGain, float lowMidGain, float midGain, float highMidGain, float highGain, float airGain, float lpFreq, float hpFreq) {
            if (clip == null) return null;

            float[] samples = new float[clip.samples * clip.channels];
            bool success = clip.GetData(samples, 0);
            if (!success) {
                Debug.LogError($"[Zounds] ApplyEqualizer: GetData failed for {clip.name}");
            }

            int channels = clip.channels;
            float sampleRate = clip.frequency;

            // Filter setup
            LowPassFilter lp = new LowPassFilter(sampleRate, lpFreq, 0.707f);
            HighPassFilter hp = new HighPassFilter(sampleRate, hpFreq, 0.707f);
            
            PeakingEQFilter subBand = new PeakingEQFilter(sampleRate, 60f, subGain, 0.7f);
            PeakingEQFilter lowBand = new PeakingEQFilter(sampleRate, 150f, lowGain, 0.8f);
            PeakingEQFilter lowMidBand = new PeakingEQFilter(sampleRate, 400f, lowMidGain, 1.0f);
            PeakingEQFilter midBand = new PeakingEQFilter(sampleRate, 1000f, midGain, 1.0f);
            PeakingEQFilter highMidBand = new PeakingEQFilter(sampleRate, 2500f, highMidGain, 1.0f);
            PeakingEQFilter highBand = new PeakingEQFilter(sampleRate, 6000f, highGain, 0.8f);
            PeakingEQFilter airBand = new PeakingEQFilter(sampleRate, 12000f, airGain, 0.7f);

            for (int i = 0; i < clip.samples; i++) {
                for (int c = 0; c < channels; c++) {
                    int index = i * channels + c;
                    float sample = samples[index];
                    
                    // Apply Filters
                    if (lpFreq < 21900f) sample = lp.Process(sample);
                    if (hpFreq > 20f) sample = hp.Process(sample);
                    
                    // Apply EQ Bands
                    sample = subBand.Process(sample);
                    sample = lowBand.Process(sample);
                    sample = lowMidBand.Process(sample);
                    sample = midBand.Process(sample);
                    sample = highMidBand.Process(sample);
                    sample = highBand.Process(sample);
                    sample = airBand.Process(sample);
                    
                    samples[index] = sample;
                }
            }

            AudioClip newClip = AudioClip.Create(clip.name + "_EQ", clip.samples, channels, (int)sampleRate, false);
            newClip.SetData(samples, 0);
            return newClip;
        }
        #endregion


        public static AudioClip SaveAudio(AudioClip result, string filePath) {
            if (result == null) return null;

            float[] checkData = new float[result.samples * result.channels];
            result.GetData(checkData, 0);
            bool hasSound = false;
            float maxAmplitude = 0f;
            for(int i = 0; i < checkData.Length; i++) {
                float abs = Mathf.Abs(checkData[i]);
                if (abs > 0.0001f) {
                    hasSound = true;
                }
                if (abs > maxAmplitude) maxAmplitude = abs;
            }

            if (!hasSound) {
                Debug.LogError($"[Zounds] Render Alert: The generated buffer for {result.name} is COMPLETELY SILENT (Max Amp: {maxAmplitude}). Path: {filePath}");
            }

            SavWav.Save(GetAbsolutePath(filePath), result);
            AssetDatabase.ImportAsset(filePath);
            var reloaded = AssetDatabase.LoadAssetAtPath<AudioClip>(filePath);
            return reloaded;
        }

        private static string GetAbsolutePath(string assetPath) {
            if (string.IsNullOrEmpty(assetPath)) {
                Debug.LogWarning("GetAbsolutePath: assetPath is null or empty.");
                return null;
            }

            if (assetPath.StartsWith("Assets/")) {
                return Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring(7)));
            }
            else if (assetPath.StartsWith("Packages/")) {
                // TODO: Handle Packages/ folder
                Debug.LogError("GetAbsolutePath for Packages is not yet implemented.");
            }
            else {
                Debug.LogWarning("GetAbsolutePath: Unsupported asset path " + assetPath);
            }

            return null;
        }

#if ADDRESSABLES_INSTALLED
        public static AssetReferenceT<AudioClip> GetAudioReference(AudioClip audioClip) {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) {
                Debug.Log("Please create Addressable Asset Settings first.");
                return null;
            }

            string path = AssetDatabase.GetAssetPath(audioClip);
            var guid = AssetDatabase.GUIDFromAssetPath(path).ToString();
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);

            if (entry != null) {
                AssetReferenceT<AudioClip> reference = new AssetReferenceT<AudioClip>(guid);
                return reference;
            }

            return null;
        }
#endif


        public static AudioClip ApplyGain(AudioClip clip, float gain) {
            if (clip == null) return null;
            
            // Safety: If gain is 0 (reset state), treat as 1.0 (no boost/change)
            if (Mathf.Abs(gain) < 0.0001f) gain = 1f;
            if (Mathf.Approximately(gain, 1f)) return clip;
            
            float[] samples = new float[clip.samples * clip.channels];
            bool success = clip.GetData(samples, 0);
            if (!success) {
                Debug.LogError($"[Zounds] ApplyGain: GetData failed for {clip.name}");
            }
            
            for (int i = 0; i < samples.Length; i++) {
                samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
            }
            AudioClip newClip = AudioClip.Create(clip.name + "_Gain", clip.samples, clip.channels, clip.frequency, false);
            newClip.SetData(samples, 0);
            return newClip;
        }

    }

}
