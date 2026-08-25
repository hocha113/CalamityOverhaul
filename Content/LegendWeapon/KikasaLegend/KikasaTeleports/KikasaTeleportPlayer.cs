using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTeleports
{
    /// <summary>
    /// 鬼域传送的按键单一受理点：复用 Legend_Teleport，与比目鱼/赛博共键。
    /// 比目鱼要海域、赛博要空间层，这里只在手持鬼伞时受理，持械门天然互斥；
    /// 赛博空间激活时让位给赛博瞬移（与 <see cref="KikasaResets.KikasaResetPlayer"/> 的让位同款）
    /// </summary>
    public class KikasaTeleportPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer || Player.dead
                || CWRKeySystem.Legend_Teleport == null
                || !CWRKeySystem.Legend_Teleport.JustPressed) {
                return;
            }
            //时停/全屏地图/演出锁输入时不受理新命令
            if (HackTime.Active || Main.mapFullscreen
                || Main.blockInput || Player.mouseInterface) {
                return;
            }
            if (Cyberspace.Active) {
                return;
            }
            //持伞门：领域在切走武器后仍保持打开，传送只归手上有伞的人
            if (!HoldingUmbrella()) {
                return;
            }
            KikasaTeleport.TryTeleport(Player);
        }

        private bool HoldingUmbrella() {
            Item item = Player.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }
    }
}
