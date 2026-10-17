using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Applies the shared type scale to live UI Toolkit text elements.</summary>
    public static class TanTypeStyles
    {
        public static void Apply(Label label, VisualTextRole role)
        {
            if (label == null) return;
            label.style.fontSize = VisualDesignTokens.FontSize(role);
            label.style.whiteSpace = WhiteSpace.Normal;
        }
    }
}
