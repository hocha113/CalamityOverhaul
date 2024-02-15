using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 空血条，当不希望原版血条有任何行为时将其设置给 NPC.BossBar
    /// </summary>
    public class NullBossBar : ModBossBar
    {
        public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)/* tModPorter Note: life and shield current and max values are now separate to allow for hp/shield number text draw */
        {
            return false;
        }
    }
}
