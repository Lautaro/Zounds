using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class ZequenceHandler : ZoundHandler<Zequence> {

        public class RuntimeZoundEntry {
            public int zoundId;
            public ZoundToken token;
        }

        private List<RuntimeZoundEntry> runtimeZoundEntries;

        public ZequenceHandler(Zequence zequence, AudioSource audioSource) : base(zequence, audioSource) {
            runtimeZoundEntries = new List<RuntimeZoundEntry>();
            foreach (var entry in zequence.zoundEntries) {
                var runtimeEntry = new RuntimeZoundEntry() {
                    zoundId = entry.zoundId
                };
                runtimeZoundEntries.Add(runtimeEntry);
            }
        }

        public override void OnStart() {
#if ADDRESSABLES_INSTALLED
            audioSource.clip = null;
#endif
            base.OnStart();
        }

        public override bool OnUpdate() {
            bool killed = base.OnUpdate();
            if (!killed) {
                foreach (var runtimeEntry in runtimeZoundEntries) {
                    if (runtimeEntry.token == null) {
                        // TODO Play zound entries
                    }
                }
            }
            return killed;
        }

    }

}
