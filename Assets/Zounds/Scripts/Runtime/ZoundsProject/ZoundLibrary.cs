using System.Collections.Generic;
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
            return (library.klips.Find(         k => k.id == id && k != self) != null) ||
                   (library.zequences.Find(     z => z.id == id && z != self) != null) ||
                   (library.muzics.Find(        m => m.id == id && m != self) != null) ||
                   (library.randomizers.Find(   r => r.id == id && r != self) != null);
        }

        [System.Serializable]
        public class Tag {
            public int id;
            public string name;
        }
    }


    [System.Serializable]
    public class Zound {
        public int id;
        public string name;
        public float minVolume = 0.25f;
        public float maxVolume = 1f;
        public float minPitch = 0.5f;
        public float maxPitch = 1.5f;
        public float chance = 1f;
        public List<int> tags = new List<int>();

        public Zound(int id) {  this.id = id; }

        public virtual bool HasDependency(Zound otherZound) {
            return false;
        }

        public virtual void RemoveDependency(Zound otherZound) {

        }

#if UNITY_EDITOR
        public bool needsRender;
#endif
    }


    public interface IZoundAudioClip {
#if ADDRESSABLES_INSTALLED
        AssetReference GetAudioClipReference();
#endif
    }


    [System.Serializable]
    public class Klip : Zound, IZoundAudioClip {

        public float trimStart;
        public float trimEnd;
        public Envelope volumeEnvelope;
        public Envelope pitchEnvelope;

#if ADDRESSABLES_INSTALLED
        public AssetReference audioClipRef;
        public AssetReference renderedClipRef;

        public AssetReference GetAudioClipReference() {
            return renderedClipRef.RuntimeKeyIsValid()? renderedClipRef : audioClipRef;
        }
#endif
        public Klip(int id) : base(id) { }
    }


    [System.Serializable]
    public class Zequence : Zound {
        public Zequence(int id) : base(id) { }

        public List<ZoundEntry> zoundEntries = new List<ZoundEntry>();

        public override bool HasDependency(Zound otherZound) {
            return zoundEntries.Find(entry => entry.zoundId == otherZound.id) != null;
        }

        public override void RemoveDependency(Zound otherZound) {
            zoundEntries.RemoveAll(entry => entry.zoundId == otherZound.id);
        }

        [System.Serializable]
        public class ZoundEntry {
            public enum ZoundType {
                Klip, Zequence, Muzic, Randomizer
            }
            public int zoundId;
            public float delay;
            public float volume;
            public float pitch;
            public float chance;
        }
    }


    [System.Serializable]
    public class Muzic : Zound, IZoundAudioClip {
        public Muzic(int id) : base(id) { }

#if ADDRESSABLES_INSTALLED
        public AssetReference audioClipRef;

        public AssetReference GetAudioClipReference() {
            return audioClipRef;
        }
#endif
    }


    [System.Serializable]
    public class Randomizer : Zound {
        public Randomizer(int id) : base(id) { }

    }

}
