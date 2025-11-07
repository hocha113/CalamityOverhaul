using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltower
{
    internal class DeploySignaltowerRender : RenderHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D DeploySignaltowerShow;//大小宽512高768，用于ADV任务介绍场景中展示信号塔的图片
    }
}
