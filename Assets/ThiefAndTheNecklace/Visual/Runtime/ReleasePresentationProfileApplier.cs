using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Optional scene host that applies the selected release profile at startup.</summary>
    public sealed class ReleasePresentationProfileApplier : MonoBehaviour
    {
        [SerializeField] private ReleasePresentationProfile profile;
        public ReleasePresentationProfile Profile => profile;

        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogError("ReleasePresentationProfileApplier has no release profile; visual defaults were not applied.", this);
                return;
            }
            profile.ApplyRuntimeDefaults();
            var lighting = GetComponent<LightingController>();
            if (lighting != null) lighting.SetState(profile.openingLighting, false);
        }
    }
}
