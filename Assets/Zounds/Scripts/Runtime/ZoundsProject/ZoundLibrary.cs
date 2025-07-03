using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
#endif

namespace Zounds {

    [System.Serializable]
    public class ZoundLibrary {

        public List<Klip>       klips       = new List<Klip>();
        public List<Zequence>   zequences   = new List<Zequence>();
        public List<Muzic>      muzics      = new List<Muzic>();
        public List<Randomizer> randomizers = new List<Randomizer>();

        public List<Tag> tags = new List<Tag>();

        public bool TryGetTag(string name, out Tag tag) {
            tag = tags.Find(t => t.name == name);
            return tag != null;
        }

        public bool TryGetTag(int id, out Tag tag) {
            tag = tags.Find(t => t.id == id);
            return tag != null;
        }

        public Tag CreateNewTag(string name) {
            if (tags.Find(t => t.name == name) != null) {
                Debug.LogError("Tag already exists: " + name);
                return null;
            }
            Tag tag = new Tag();
            tag.name = name;
            do {
                tag.id = Random.Range(int.MinValue, int.MaxValue);
            } while (tags.Find(t => t.id == tag.id) != null);
            tags.Add(tag);
            return tag;
        }

        public int RemoveUnusedTags() {
            int removedCount = 0;
            removedCount += tags.RemoveAll(tag => {
                int tagId = tag.id;
                return FindZound(z => z.tags.Contains(tagId)) == null;
            });
            return removedCount;
        }


        public static int GetUniqueZoundId() {
            var library = ZoundsProject.Instance.zoundLibrary;
            int id;
            do {
                id = Random.Range(int.MinValue, int.MaxValue);
            } while (id != 0 && ZoundIdExists(library, null, id));
            return id;
        }

        public void Validate() {
            bool dirty = false;
            if (ValidateZounds(klips)) dirty = true;
            if (ValidateZounds(zequences)) dirty = true;
            if (ValidateZounds(muzics)) dirty = true;
            if (ValidateZounds(randomizers)) dirty = true;
#if UNITY_EDITOR
            if (dirty) {
                UnityEditor.EditorUtility.SetDirty(ZoundsProject.Instance);
            }
#endif
        }

        private bool ValidateZounds<TZound>(List<TZound> zounds) where TZound : Zound {
            bool dirty = false;
            foreach (var zound in zounds) {
                if (zound.id == 0 || ZoundIdExists(this, zound, zound.id)) {
                    dirty = true;
                    zound.id = GetUniqueZoundId();
                }
            }
            return dirty;
        }

        private static bool ZoundIdExists(ZoundLibrary library, Zound self, int id) {
            return library.FindZound(z => z.id == id && z != self) != null;
        }

        public List<Zound> GetAllZounds() {
            List<Zound> allZounds = new List<Zound>();
            allZounds.AddRange(klips);
            allZounds.AddRange(zequences);
            allZounds.AddRange(muzics);
            allZounds.AddRange(randomizers);
            return allZounds;
        }

        public Zound FindZound(System.Predicate<Zound> match) {
            Zound result = klips.Find(match);
            if (result != null) return result; 
            result = zequences.Find(match);
            if (result != null) return result;
            result = muzics.Find(match);
            if (result != null) return result;
            result = randomizers.Find(match);
            if (result != null) return result;
            return null;
        }

        public List<Zound> FindAllZounds(System.Predicate<Zound> match) {
            var result = new List<Zound>();
            result.AddRange(klips.FindAll(match));
            result.AddRange(zequences.FindAll(match));
            result.AddRange(muzics.FindAll(match));
            result.AddRange(randomizers.FindAll(match));
            return result;
        }

        public void ForEachZound(System.Action<Zound> handler) {
            foreach (var z in klips) handler(z);
            foreach (var z in zequences) handler(z);
            foreach (var z in muzics) handler(z);
            foreach (var z in randomizers) handler(z);
        }
        

        [System.Serializable]
        public class Tag {
            public int id;
            public string name;
        }
    }


    [System.Serializable]
    public class Zound {
        internal const float MinVolumeRange = 0f;
        internal const float MaxVolumeRange = 1f;
        internal const float MinPitchRange = 0.1f;
        internal const float MaxPitchRange = 2f;
        internal const float MinChanceRange = 0f;
        internal const float MaxChanceRange = 1f;

        public int id;
        public int originalId;
        public int parentId;
        public string name;
        public float minVolume = 0.25f;
        public float maxVolume = 1f;
        public float minPitch = 0.5f;
        public float maxPitch = 1.5f;
        public float chance = 1f;
        public List<int> tags = new List<int>();

        public Zound(int id) {  this.id = id; }
        public Zound(int id, Zound source) { 
            this.id = id; 
            name = ZoundDictionary.EnsureUniqueZoundName(source.name);
            minVolume = source.minVolume;
            maxVolume = source.maxVolume;
            minPitch = source.minPitch;
            maxPitch = source.maxPitch;
            chance = source.chance;
            tags.AddRange(source.tags);
        }

        public virtual List<Zound> GetDependencies() {
            return new List<Zound>();
        }

        public virtual bool HasDirectDependency(Zound otherZound) {
            return false;
        }

        public virtual bool HasNestedDependency(Zound otherZound) {
            return false;
        }

        public virtual void RemoveDependency(Zound otherZound) {

        }

#if UNITY_EDITOR
        [HideInInspector] public bool editor_needsRender;
#endif
    }


    public interface IZoundAudioClip {
#if ADDRESSABLES_INSTALLED
        AssetReference GetAudioClipReference();
#endif
    }

    internal class ClipZound : Zound {

        public AudioClip audioClip;
        public string audioPath;

        public ClipZound(AudioClip audioClip, string audioPath) : base(0) {
            this.name = audioClip.name;
            this.audioClip = audioClip;
            this.audioPath = audioPath;
            minVolume = 1f;
            maxVolume = 1f;
            minPitch = 1f;
            maxPitch = 1f;
            chance = 1f;
        }
    }

    [System.Serializable]
    public class Klip : Zound, IZoundAudioClip {

        public float trimStart;
        public float trimEnd;
        public Envelope volumeEnvelope;
        public Envelope pitchEnvelope;

        public string audioClipPath;
        public string renderedClipPath;
#if ADDRESSABLES_INSTALLED
        public AssetReference audioClipRef;
        public AssetReference renderedClipRef;

        public AssetReference GetAudioClipReference() {
            return renderedClipRef != null && renderedClipRef.RuntimeKeyIsValid()? renderedClipRef : audioClipRef;
        }
#endif
        public string GetAudioClipPath() {
            return string.IsNullOrEmpty(renderedClipPath) ? audioClipPath : renderedClipPath;
        }

        public Klip(int id) : base(id) { }
        public Klip(int id, Klip source) : base(id, source) {
            trimStart = source.trimStart;
            trimEnd = source.trimEnd;
            volumeEnvelope = source.volumeEnvelope.DeepCopy();
            pitchEnvelope = source.pitchEnvelope.DeepCopy();
#if ADDRESSABLES_INSTALLED
            audioClipRef = source.audioClipRef;
            renderedClipRef = source.renderedClipRef;
#endif
        }
    }


    [System.Serializable]
    public class Zequence : Zound {

        public Envelope masterVolumeEnvelope = new Envelope(MinVolumeRange, MaxVolumeRange);
        public List<ZoundEntry> zoundEntries = new List<ZoundEntry>();
        public List<Klip> localKlips = new List<Klip>();

        public Zequence(int id) : base(id) { }
        public Zequence(int id, Zequence source) : base(id, source) {
            foreach (var entry in source.zoundEntries) {
                var serialized = JsonUtility.ToJson(entry);
                zoundEntries.Add(JsonUtility.FromJson<ZoundEntry>(serialized));
            }
        }

        public override List<Zound> GetDependencies() {
            var result = new List<Zound>();
            foreach (var entry in zoundEntries) {
                if (ZoundDictionary.TryGetZoundById(entry.zoundId, out var zound)) {
                    result.Add(zound);
                }
            }
            foreach (var entry in zoundEntries) {
                if (ZoundDictionary.TryGetZoundById(entry.zoundId, out var zound)) {
                    result.AddRange(zound.GetDependencies());
                }
            }
            result = result.Distinct().ToList();
            return result;
        }

        public override bool HasDirectDependency(Zound otherZound) {
            return zoundEntries.Find(entry => entry.zoundId == otherZound.id) != null;
        }

        public override bool HasNestedDependency(Zound otherZound) {
            foreach (var entry in zoundEntries) {
                if (ZoundDictionary.TryGetZoundById(entry.zoundId, out var zound)) {
                    if (zound.HasDirectDependency(otherZound)) return true;
                    else if (zound.HasNestedDependency(otherZound)) return true;
                }
            }
            return false;
        }

        public override void RemoveDependency(Zound otherZound) {
            zoundEntries.RemoveAll(entry => entry.zoundId == otherZound.id);
        }

        public bool TryGetEntryZound(ZoundEntry entry, out Zound zound) {
            if (entry.local) {
                zound = localKlips.Find(k => k.id == entry.zoundId);
                return zound != null;
            }
            else {
                if (ZoundDictionary.TryGetZoundById(entry.zoundId, out zound)) {
                    return true;
                }
            }
            zound = null;
            return false;
        }

        [System.Serializable]
        public class ZoundEntry {
            public enum ZoundType {
                Klip, Zequence, Muzic, Randomizer
            }
            public int zoundId;
            public bool local;
            public float delay;
            public float volume = 1f;
            public float pitch = 1f;
            public float chance = 1f;
            public bool overrideVolume;
            public bool overridePitch;
            public bool overrideChance;
            public bool mute;
            public bool solo;
            public Envelope volumeEnvelope = new Envelope(MinVolumeRange, MaxVolumeRange);

        }

#if UNITY_EDITOR
        [HideInInspector] public float editor_maxDuration = 3f;
#endif

    }


    [System.Serializable]
    public class Muzic : Zound, IZoundAudioClip {

        public string audioClipPath;

#if ADDRESSABLES_INSTALLED
        public AssetReference audioClipRef;

        public AssetReference GetAudioClipReference() {
            return audioClipRef;
        }
#endif

        public Muzic(int id) : base(id) { }
        public Muzic(int id, Muzic source) : base(id, source) {
#if ADDRESSABLES_INSTALLED
            audioClipRef = source.audioClipRef;
#endif
        }
    }


    [System.Serializable]
    public class Randomizer : Zound {
        public Randomizer(int id) : base(id) { }
        public Randomizer(int id, Randomizer source) : base(id, source) { 
        
        }

    }

}
