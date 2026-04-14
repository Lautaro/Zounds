namespace Zounds {

    public abstract class AudioEffect {

        public abstract string Name { get; }

        public abstract bool IsActive(Klip klip);

        /// <summary>
        /// Process audio samples in-place or return a new array.
        /// The pipeline passes the same float[] through each active effect in order.
        /// </summary>
        public abstract float[] Process(float[] samples, int channels, int sampleRate, Klip klip);
    }

}
