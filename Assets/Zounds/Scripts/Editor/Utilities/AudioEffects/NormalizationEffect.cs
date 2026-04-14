using UnityEngine;

namespace Zounds {

    public class NormalizationEffect : AudioEffect {

        public override string Name => "Normalization";

        public override bool IsActive(Klip klip) {
            return klip.normalizationEnabled;
        }

        public override float[] Process(float[] samples, int channels, int sampleRate, Klip klip) {
            float targetLinear = Mathf.Pow(10f, klip.normalizeTargetDB / 20f);

            // Find peak amplitude
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++) {
                float abs = Mathf.Abs(samples[i]);
                if (abs > peak) peak = abs;
            }

            // Scale so peak matches target — both boost and attenuate
            if (peak > 0.0001f) {
                float scale = targetLinear / peak;
                for (int i = 0; i < samples.Length; i++) {
                    samples[i] = Mathf.Clamp(samples[i] * scale, -1f, 1f);
                }
            }

            return samples;
        }
    }

}
