using UnityEngine;

namespace Zounds {

    public class GainEffect : AudioEffect {

        public override string Name => "Gain";

        public override bool IsActive(Klip klip) {
            // gain of 0.0 is the serialization default meaning "no boost" — treat as 1.0
            if (Mathf.Abs(klip.gain) < 0.0001f) return false;
            return !Mathf.Approximately(klip.gain, 1f);
        }

        public override float[] Process(float[] samples, int channels, int sampleRate, Klip klip) {
            float gain = klip.gain;
            if (Mathf.Abs(gain) < 0.0001f) gain = 1f;

            for (int i = 0; i < samples.Length; i++) {
                samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
            }
            return samples;
        }
    }

}
