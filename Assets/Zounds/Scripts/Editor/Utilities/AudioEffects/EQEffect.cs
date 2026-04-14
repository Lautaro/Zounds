using UnityEngine;

namespace Zounds {

    public class EQEffect : AudioEffect {

        public override string Name => "EQ";

        public override bool IsActive(Klip klip) {
            if (!klip.eqEnabled) return false;
            // Skip if all bands are near zero and filters are at default
            return Mathf.Abs(klip.subGain) > 0.1f ||
                   Mathf.Abs(klip.lowGain) > 0.1f ||
                   Mathf.Abs(klip.lowMidGain) > 0.1f ||
                   Mathf.Abs(klip.midGain) > 0.1f ||
                   Mathf.Abs(klip.highMidGain) > 0.1f ||
                   Mathf.Abs(klip.highGain) > 0.1f ||
                   Mathf.Abs(klip.airGain) > 0.1f ||
                   klip.lpFrequency < 21900f ||
                   klip.hpFrequency > 20f;
        }

        public override float[] Process(float[] samples, int channels, int sampleRate, Klip klip) {
            int sampleCount = samples.Length / channels;

            // Per-channel filter instances to avoid stereo crosstalk.
            // BiQuad filters are stateful — sharing one instance across channels
            // causes left channel history to bleed into right channel processing.
            var lp = new AudioRenderUtility.LowPassFilter[channels];
            var hp = new AudioRenderUtility.HighPassFilter[channels];
            var subBand = new AudioRenderUtility.PeakingEQFilter[channels];
            var lowBand = new AudioRenderUtility.PeakingEQFilter[channels];
            var lowMidBand = new AudioRenderUtility.PeakingEQFilter[channels];
            var midBand = new AudioRenderUtility.PeakingEQFilter[channels];
            var highMidBand = new AudioRenderUtility.PeakingEQFilter[channels];
            var highBand = new AudioRenderUtility.PeakingEQFilter[channels];
            var airBand = new AudioRenderUtility.PeakingEQFilter[channels];

            for (int c = 0; c < channels; c++) {
                lp[c] = new AudioRenderUtility.LowPassFilter(sampleRate, klip.lpFrequency, 0.707f);
                hp[c] = new AudioRenderUtility.HighPassFilter(sampleRate, klip.hpFrequency, 0.707f);
                subBand[c] = new AudioRenderUtility.PeakingEQFilter(sampleRate, 60f, klip.subGain, 0.7f);
                lowBand[c] = new AudioRenderUtility.PeakingEQFilter(sampleRate, 150f, klip.lowGain, 0.8f);
                lowMidBand[c] = new AudioRenderUtility.PeakingEQFilter(sampleRate, 400f, klip.lowMidGain, 1.0f);
                midBand[c] = new AudioRenderUtility.PeakingEQFilter(sampleRate, 1000f, klip.midGain, 1.0f);
                highMidBand[c] = new AudioRenderUtility.PeakingEQFilter(sampleRate, 2500f, klip.highMidGain, 1.0f);
                highBand[c] = new AudioRenderUtility.PeakingEQFilter(sampleRate, 6000f, klip.highGain, 0.8f);
                airBand[c] = new AudioRenderUtility.PeakingEQFilter(sampleRate, 12000f, klip.airGain, 0.7f);
            }

            bool applyLP = klip.lpFrequency < 21900f;
            bool applyHP = klip.hpFrequency > 20f;

            for (int i = 0; i < sampleCount; i++) {
                for (int c = 0; c < channels; c++) {
                    int index = i * channels + c;
                    float sample = samples[index];

                    if (applyLP) sample = lp[c].Process(sample);
                    if (applyHP) sample = hp[c].Process(sample);

                    sample = subBand[c].Process(sample);
                    sample = lowBand[c].Process(sample);
                    sample = lowMidBand[c].Process(sample);
                    sample = midBand[c].Process(sample);
                    sample = highMidBand[c].Process(sample);
                    sample = highBand[c].Process(sample);
                    sample = airBand[c].Process(sample);

                    samples[index] = sample;
                }
            }

            return samples;
        }
    }

}
