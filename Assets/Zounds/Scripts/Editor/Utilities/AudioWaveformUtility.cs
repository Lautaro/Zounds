using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Zounds {

    public static class AudioWaveformUtility {

        private static readonly Dictionary<AudioClip, Texture2D> textureCache = new Dictionary<AudioClip, Texture2D>();


        public static readonly Vector2 playerHeadSize = new Vector2(6f, 8f);
        public static Color playerHeadColor => new Color(0.1f, 0.1f, 0.9f, 0.75f);

        private static Texture m_playerHeadTexture;
        public static Texture playerHeadTexture {
            get {
                if (m_playerHeadTexture == null) {
                    m_playerHeadTexture = Resources.Load("AudioSpectrumIcons/TimeCursor") as Texture;
                }
                return m_playerHeadTexture;
            }
        }


        /// <summary>
        /// Get a waveform texture of the specified audio clip.
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="_color"></param>
        /// <returns></returns>
        public static Texture2D GetWaveformSpectrumTexture(AudioClip audioClip, int width, int height, Color _color) {
            Texture2D tex;
            bool createNew = false;
            if (textureCache.TryGetValue(audioClip, out tex)) {
                if (tex == null && !ReferenceEquals(tex, null)) {
                    createNew = true;
                }
                else if (tex.width != width || tex.height != height) {
                    createNew = true;
                }
            }
            else {
                createNew = true;
            }

            if (createNew) {
                tex = CreateNewTexture(audioClip, width, height, _color);
            }

            return tex;
        }

        private static Texture2D CreateNewTexture(AudioClip audioClip, int width, int height, Color color) {
            if (width < 1 || height < 1) {
                return null;
            }
            try {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                //int sampleCount = _audioClip.samples * _audioClip.channels;
                int sampleCount = (int)(audioClip.length * audioClip.frequency) * audioClip.channels;
                float[] samples = new float[sampleCount];
                float[] waveform = new float[width];
                audioClip.GetData(samples, 0);
                int packSize = (sampleCount / width) + 1;
                int s = 0;
                for (int i = 0; i < sampleCount; i += packSize) {
                    waveform[s] = Mathf.Abs(samples[i]);
                    s++;
                }

                for (int x = 0; x < width; x++) {
                    for (int y = 0; y < height; y++) {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                }

                for (int x = 0; x < waveform.Length; x++) {
                    for (int y = 0; y <= waveform[x] * ((float)height * .75f); y++) {
                        tex.SetPixel(x, (height / 2) + y, color);
                        tex.SetPixel(x, (height / 2) - y, color);
                    }
                }
                tex.Apply();
                if (textureCache.ContainsKey(audioClip)) {
                    textureCache[audioClip] = tex;
                }
                else {
                    textureCache.Add(audioClip, tex);
                }

                return tex;
            }
            catch (System.Exception e) {
                Debug.LogError(e);
                return null;
            }
        }

        /// <summary>
        /// A helper function to immediately draw the waveform texture into a rect.
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="audioClip"></param>
        /// <param name="backgroundColor"></param>
        /// <param name="waveformColor"></param>
        public static void DrawWaveformRect(Rect rect, AudioClip audioClip, Color backgroundColor, Color waveformColor) {
            var guiColor = GUI.color;
            GUI.color = backgroundColor;
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            var audioTexture = GetWaveformSpectrumTexture(audioClip, Mathf.FloorToInt(rect.width), Mathf.FloorToInt(rect.height), waveformColor);
            if (audioTexture != null) {
                GUI.DrawTexture(rect, audioTexture);
            }
        }

        public static void DrawPlayerHead(Rect rect, AudioSource audioSource) {
            DrawPlayerHead(rect, audioSource.time, audioSource.clip.length);
        }

        public static void DrawPlayerHead(Rect rect, float currentTime, float totalDuration) {
            var guiColor = GUI.color;
            GUI.color = playerHeadColor;
            float posX = (currentTime / totalDuration) * rect.width;
            GUI.DrawTexture(new Rect(rect.x + posX - playerHeadSize.x / 2f, rect.y, playerHeadSize.x, playerHeadSize.x * 1.82f), playerHeadTexture);
            GUI.color = guiColor;

            var handlesColor = Handles.color;
            Handles.color = playerHeadColor;
            Handles.BeginGUI();
            for (int i = -1; i <= 1; i++) {
                float xPos = rect.x + posX + i * 0.05f;
                Handles.DrawLine(new Vector3(xPos, rect.y + playerHeadSize.y, 0), new Vector3(xPos, rect.yMax, 0));
            }
            Handles.EndGUI();
            Handles.color = handlesColor;
        }

    }

}