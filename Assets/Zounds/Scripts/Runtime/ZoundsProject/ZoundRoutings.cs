using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    [System.Serializable]
    public class ZoundRoutings {

        public List<FolderRouting> folderRoutings = new List<FolderRouting>();
        public List<TagRouting> tagRoutings = new List<TagRouting>();

        [System.Serializable]
        public class FolderRouting {
            public string relativePath;
            public AudioMixerGroup mixerGroup;
        }

        [System.Serializable]
        public class TagRouting {
            public int tagId;
            public AudioMixerGroup mixerGroup;
        }

    }

}
