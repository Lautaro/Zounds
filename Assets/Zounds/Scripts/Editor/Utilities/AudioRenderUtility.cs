using UnityEngine;
using UnityEditor;

namespace Zounds {

    public static class AudioRenderUtility {

        public static AudioClip Trim(AudioClip clip, float startTime, float endTime) {
            if (clip == null) return null;

            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int startSample = Mathf.FloorToInt(startTime * sampleRate * channels);
            int endSample = Mathf.FloorToInt(endTime * sampleRate * channels);
            int lengthSamples = endSample - startSample;

            if (lengthSamples <= 0) return null;

            float[] outputData = new float[lengthSamples];
            float[] fullData = new float[clip.samples * channels];
            clip.GetData(fullData, 0);

            System.Array.Copy(fullData, startSample, outputData, 0, lengthSamples);

            AudioClip newClip = AudioClip.Create(clip.name + "_Trimmed", lengthSamples / channels, channels, sampleRate, false);
            newClip.SetData(outputData, 0);
            return newClip;
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
                clip.GetData(clipData, 0);

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


        public static AudioClip VolumeEnvelope(AudioClip clip, AnimationCurve envelope) {
            if (clip == null || envelope == null) return null;

            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int totalSamples = clip.samples * channels;
            float duration = clip.length;

            float[] outputData = new float[totalSamples];
            clip.GetData(outputData, 0);

            for (int i = 0; i < clip.samples; i++) {
                float t = (float)i / clip.samples;
                float volumeFactor = envelope.Evaluate(t);

                for (int c = 0; c < channels; c++) {
                    int index = i * channels + c;
                    outputData[index] *= volumeFactor;
                }
            }

            AudioClip newClip = AudioClip.Create(clip.name + "_VolumeEnveloped", clip.samples, channels, sampleRate, false);
            newClip.SetData(outputData, 0);
            return newClip;
        }


        public static AudioClip PitchEnvelope(AudioClip clip, AnimationCurve envelope) {
            if (clip == null || envelope == null) return null;

            float[] sourceSamples = new float[clip.samples * clip.channels];
            clip.GetData(sourceSamples, 0);
            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int sourceSampleCount = clip.samples;

            float totalOutputDuration = CalculateOutputDuration(clip.length, envelope);

            // new sample count. since pitch also changes the speed, then the sample count will not be the same.
            // this also means we need to interpolate the samples later down below
            int outputSampleCount = Mathf.CeilToInt(totalOutputDuration * sampleRate * clip.channels);
            outputSampleCount += outputSampleCount % sourceSampleCount;
            float[] outputData = new float[outputSampleCount];


            float accumulatedOutputTime = 0f;
            float clipDuration = clip.length;

            for (int i = 0; i < outputSampleCount; i += channels) {
                EditorUtility.DisplayProgressBar("Rendering Pitch Envelope", "Interpolating Samples " + (i + 1) + " / " + outputSampleCount, (i + 1f) / (float)outputSampleCount);
                float outputProgress = accumulatedOutputTime / totalOutputDuration;
                float currentPitch = envelope.Evaluate(outputProgress);
                currentPitch = Mathf.Clamp(currentPitch, 0.1f, 2f); // we decided that the pitch range is between 0.1 ~ 2

                float sourceTime = GetSourceTimeForOutputTime(accumulatedOutputTime, envelope, clipDuration);
                float sourcePosition = sourceTime / clipDuration;

                // interpolate sample for each channel
                for (int channel = 0; channel < channels; channel++) {
                    outputData[i + channel] = GetInterpolatedSample(sourceSamples, sourcePosition, sourceSampleCount, channels, channel);
                }

                // iterate time based on current pitch
                // higher pitch -> faster playback, meaning smaller time increment
                accumulatedOutputTime += (1f / sampleRate);
            }
            EditorUtility.ClearProgressBar();

            AudioClip newClip = AudioClip.Create(clip.name + "_PitchEnveloped", outputSampleCount / channels, channels, sampleRate, false);
            newClip.SetData(outputData, 0);
            return newClip;
        }

        public static AudioClip CutOffEnvelope(AudioClip clip, AnimationCurve envelope, bool highPass, float resonance = 1f) {
            if (clip == null || envelope == null) return null;

            float[] audioData = new float[clip.samples * clip.channels];
            clip.GetData(audioData, 0);

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
        private static float CalculateOutputDuration(float sourceDuration, AnimationCurve pitchEnvelope) {
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

        private static float GetSourceTimeForOutputTime(float outputTime, AnimationCurve pitchEnvelope, float sourceDuration) {
            int integrationSteps = 1000/*10000*/; // arbitrary. higher value, then longer iteration loop, but more detailed/high precision.
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

        private static float GetInterpolatedSample(float[] samples, float position, int sampleCount, int channelCount, int channel) {
            float exactSampleIndex = position * (sampleCount - 1) * channelCount + channel;
            int sampleIndex = Mathf.FloorToInt(exactSampleIndex);
            float t = exactSampleIndex - sampleIndex;

            sampleIndex = Mathf.Clamp(sampleIndex, 0, samples.Length - channelCount - 1);
            int nextSampleIndex = Mathf.Min(sampleIndex + channelCount, samples.Length - 1);

            float sampleA = samples[sampleIndex];
            float sampleB = samples[nextSampleIndex];

            return Mathf.Lerp(sampleA, sampleB, t);
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
                float q = Mathf.Clamp(resonance, 0.1f, 10f);
                float alpha = Mathf.Tan(w / 2f);

                float norm = 1f / (1f + alpha / q + alpha * alpha);
                a0 = alpha * alpha * norm;
                a1 = 2f * a0;
                a2 = a0;
                b1 = 2f * (alpha * alpha - 1f) * norm;
                b2 = (1f - alpha / q + alpha * alpha) * norm;
            }
        }

        public class HighPassFilter : BiQuadFilter {
            public HighPassFilter(float sampleRate, float cutoff, float resonance = 1f) {
                this.sampleRate = sampleRate;
                SetCutoffFrequency(cutoff, resonance);
            }

            public override void SetCutoffFrequency(float cutoff, float resonance = 1f) {

                // gotta get back later to fix this formula to work like this calculator:
                // https://www.earlevel.com/main/2013/10/13/biquad-calculator-v2/

                cutoff = Mathf.Clamp(cutoff, 10f, 22000f);
                float w = 2f * Mathf.PI * cutoff / sampleRate;
                float q = Mathf.Clamp(resonance, 0.1f, 10f);
                float alpha = Mathf.Tan(w / 2f);

                float norm = 1f / (1f + alpha / q + alpha * alpha);
                a0 = norm;
                a1 = -2f * a0;
                a2 = a0;
                b1 = 2f * (alpha * alpha - 1f) * norm;
                b2 = (1f - alpha / q + alpha * alpha) * norm;
            }
        }
        #endregion

    }

}
