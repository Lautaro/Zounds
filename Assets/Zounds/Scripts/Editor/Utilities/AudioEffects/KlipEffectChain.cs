namespace Zounds {

    public static class KlipEffectChain {

        private static readonly AudioEffect[] effects = new AudioEffect[] {
            new EQEffect(),
            new GainEffect(),
            new CompressionEffect(),
            new NormalizationEffect(),
            new FadeEffect(),
        };

        public static bool HasActiveEffects(Klip klip) {
            for (int i = 0; i < effects.Length; i++) {
                if (effects[i].IsActive(klip)) return true;
            }
            return false;
        }

        public static float[] ProcessChain(float[] samples, int channels, int sampleRate, Klip klip) {
            for (int i = 0; i < effects.Length; i++) {
                if (effects[i].IsActive(klip)) {
                    samples = effects[i].Process(samples, channels, sampleRate, klip);
                }
            }
            return samples;
        }
    }

}
