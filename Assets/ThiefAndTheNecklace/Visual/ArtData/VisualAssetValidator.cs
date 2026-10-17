using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Runtime-safe sprite validation shared by visual binders and editor checks.</summary>
    public static class VisualAssetValidator
    {
        public static bool ValidateSprite(Sprite sprite, string assetName)
        {
            if (sprite == null)
            {
                Debug.LogError($"Missing sprite: {assetName}");
                return false;
            }

            if (sprite.texture == null)
            {
                Debug.LogError($"Sprite has no texture: {assetName}");
                return false;
            }

            return true;
        }
    }
}
