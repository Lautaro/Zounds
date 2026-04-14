using UnityEngine;

namespace Zounds {

    public class FadeEffect : AudioEffect {

        public override string Name => "Fade";

        public override bool IsActive(Klip klip) {
            return klip.fadeEnabled && (klip.fadeInDuration > 0.001f || klip.fadeOutDuration > 0.001f);
        }

        public override float[] Process(float[] samples, int channels, int sampleRate, Klip klip) {
            int sampleCount = samples.Length / channels;
            bool sCurve = klip.fadeUseSCurve;

            // Fade in
            int fadeInSamples = Mathf.Min(Mathf.FloorToInt(klip.fadeInDuration * sampleRate), sampleCount);
            if (fadeInSamples > 0) {
                for (int i = 0; i < fadeInSamples; i++) {
                    float t = (float)i / fadeInSamples;
                    float factor = sCurve ? t * t * (3f - 2f * t) : t;
                    for (int c = 0; c < channels; c++) {
                        samples[i * channels + c] *= factor;
                    }
                }
            }

            // Fade out
            int fadeOutSamples = Mathf.Min(Mathf.FloorToInt(klip.fadeOutDuration * sampleRate), sampleCount);
            if (fadeOutSamples > 0) {
                int fadeOutStart = sampleCount - fadeOutSamples;
                for (int i = 0; i < fadeOutSamples; i++) {
                    float t = 1f - (float)i / fadeOutSamples;
                    float factor = sCurve ? t * t * (3f - 2f * t) : t;
                    for (int c = 0; c < channels; c++) {
                        samples[(fadeOutStart + i) * channels + c] *= factor;
                    }
                }
            }

            return samples;
        }
    }

}
