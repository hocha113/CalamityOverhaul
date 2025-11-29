using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵营地
    /// </summary>
    internal class OldDukeCampsite
    {
        //反射加载老公爵贴图，以便在ADV场景中使用，总共七帧，一般只使用前六帧，因为第七帧是张嘴动画
        [VaultLoaden("@CalamityMod/NPCs/OldDuke/")]
        public static Texture2D OldDuke;
    }
}
