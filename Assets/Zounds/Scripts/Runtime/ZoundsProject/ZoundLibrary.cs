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


        [System.Serializable]
        public class Tag {
            public int id;
            public string name;
        }
    }

    [System.Serializable]
    public class Zound {
        public string name;
#if ADDRESSABLES_INSTALLED
        public AssetReference audioClipRef;
#endif
        public float minVolume = 0f;
        public float maxVolume = 1f;
        public float minPitch = 0.1f;
        public float maxPitch = 2f;
        public float chance = 1f;
        public List<int> tags = new List<int>();
    }

    [System.Serializable]
    public class Klip : Zound {

    }

    [System.Serializable]
    public class Zequence : Zound {

        public List<ZoundEntry> zoundEntries = new List<ZoundEntry>();

        [System.Serializable]
        public class ZoundEntry {
            public enum ZoundType {
                Klip, Zequence, Muzic, Randomizer
            }
            public float zoundName;
            public ZoundType zoundType;
            public float delay;
            public float volume;
            public float pitch;
            public float chance;
        }
    }

    [System.Serializable]
    public class Muzic : Zound {

    }

    [System.Serializable]
    public class Randomizer : Zound {

    }

}
