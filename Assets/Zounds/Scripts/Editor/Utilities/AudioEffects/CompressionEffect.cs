using UnityEngine;

namespace Zounds {

    public class CompressionEffect : AudioEffect {

        public override string Name => "Compression";

        public override bool IsActive(Klip klip) {
            return klip.compressionEnabled;
        }

        public override float[] Process(float[] samples, int channels, int sampleRate, Klip klip) {
            float thresholdDB = klip.compThreshold;
            float ratio = Mathf.Max(klip.compRatio, 1f);
            float attackMs = Mathf.Max(klip.compAttack, 0.1f);
            float releaseMs = Mathf.Max(klip.compRelease, 1f);
            float makeupDB = klip.compMakeupGain;

            int sampleCount = samples.Length / channels;

            // Convert attack/release from ms to per-sample coefficients.
            // coefficient = exp(-1 / (time_in_seconds * sampleRate))
            // Higher coefficient = slower response (smoother envelope).
            float attackCoeff = Mathf.Exp(-1f / (attackMs * 0.001f * sampleRate));
            float releaseCoeff = Mathf.Exp(-1f / (releaseMs * 0.001f * sampleRate));

            float thresholdLin = Mathf.Pow(10f, thresholdDB / 20f);
            float makeupLin = Mathf.Pow(10f, makeupDB / 20f);

            // Pass 1: Compute per-sample gain reduction envelope.
            // Uses peak detection with attack/release smoothing.
            // Linked stereo: detect level from max across channels.
            float[] gainReduction = new float[sampleCount];
            float envelope = 0f;

            for (int i = 0; i < sampleCount; i++) {
                // Find peak across all channels for this sample frame
                float peak = 0f;
                for (int c = 0; c < channels; c++) {
                    float abs = Mathf.Abs(samples[i * channels + c]);
                    if (abs > peak) peak = abs;
                }

                // Envelope follower: attack when signal rises, release when it falls
                if (peak > envelope)
                    envelope = attackCoeff * envelope + (1f - attackCoeff) * peak;
                else
                    envelope = releaseCoeff * envelope + (1f - releaseCoeff) * peak;

                // Compute gain reduction in linear domain
                if (envelope > thresholdLin && thresholdLin > 0f) {
                    // How many dB above threshold
                    float envelopeDB = 20f * Mathf.Log10(envelope);
                    float overDB = envelopeDB - thresholdDB;
                    // Reduce by (1 - 1/ratio) of the overshoot
                    float reductionDB = overDB * (1f - 1f / ratio);
                    gainReduction[i] = Mathf.Pow(10f, -reductionDB / 20f);
                } else {
                    gainReduction[i] = 1f;
                }
            }

            // Pass 2: Apply gain reduction + makeup gain
            for (int i = 0; i < sampleCount; i++) {
                float gr = gainReduction[i] * makeupLin;
                for (int c = 0; c < channels; c++) {
                    int idx = i * channels + c;
                    samples[idx] = Mathf.Clamp(samples[idx] * gr, -1f, 1f);
                }
            }

            return samples;
        }
    }

}
