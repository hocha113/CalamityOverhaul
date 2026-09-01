using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.WeaponSkills
{
    /// <summary>
    /// 拖放落点期间锁普攻/物块交互;mouseInterface 在 Draw 开头被清,须在 SetControls 再写一次
    /// </summary>
    internal sealed class WeaponSkillAimPlayer : ModPlayer
    {
        public override void SetControls() {
            if (Player.whoAmI != Main.myPlayer || !WeaponSkillHud.IsAiming) {
                return;
            }
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.mouseInterface = true;
        }
    }
}
