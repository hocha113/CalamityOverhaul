using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启的按键单一受理点：复用 Legend_Restart，与比目鱼/赛博/绯嫁共键
    /// 比目鱼与绯嫁各有持械门，这里同样只在手持鬼伞时受理，赛博空间激活时让位，
    /// 四家天然互斥。倒放/无敌/结算不挂在这里，那些每帧工作由
    /// <see cref="KikasaResetSystem"/> 驱动的 <see cref="KikasaReset"/> 统一做
    /// </summary>
    public class KikasaResetPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer || Player.dead
                || CWRKeySystem.Legend_Restart == null
                || !CWRKeySystem.Legend_Restart.JustPressed) {
                return;
            }
            //时停/全屏地图/演出锁输入时不受理新命令
            if (HackTime.Active || Main.mapFullscreen
                || Main.blockInput || Player.mouseInterface) {
                return;
            }
            //赛博空间激活时让位给赛博重启（与 CrimsonBrideRestart 的让位同款）
            if (Cyberspace.Active) {
                return;
            }
            //持伞门：领域在切走武器后仍保持打开，重启只归手上有伞的人
            if (!HoldingUmbrella()) {
                return;
            }
            KikasaReset.TryReset(Player);
        }

        private bool HoldingUmbrella() {
            Item item = Player.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        /// <summary>
        /// 无敌的前置顶位：Apply 落地当帧要到 PostUpdateEverything 才补 immune，
        /// 这里在玩家更新最前面先顶住，堵起始帧的空窗
        /// </summary>
        public override void PreUpdate() {
            if (KikasaReset.IsPlayerAffected(Player.whoAmI) && !Player.dead) {
                Player.immune = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 2);
            }
        }

        /// <summary>immune 被其他系统消耗或绕开时的末道免伤：演出期间任何 Hurt 一律无效</summary>
        public override bool FreeDodge(Player.HurtInfo info)
            => KikasaReset.IsPlayerAffected(Player.whoAmI);

        /// <summary>
        /// 全程无敌的语义覆盖 DoT：immune 只挡碰撞与弹幕，
        /// 中毒/灼烧走 lifeRegen：倒带途中坏再生钳零，防被烧死在照片里
        /// </summary>
        public override void UpdateBadLifeRegen() {
            if (KikasaReset.IsPlayerAffected(Player.whoAmI) && Player.lifeRegen < 0) {
                Player.lifeRegen = 0;
            }
        }
    }
}
