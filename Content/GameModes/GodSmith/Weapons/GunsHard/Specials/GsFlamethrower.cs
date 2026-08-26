using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard.Specials
{
    /// <summary>
    /// 火焰喷射器重铸（L3 手持接管）：持续焰流双档。<br/>
    /// [扇焰] 宽锥 26° 短程，贴地留 2 秒残焰补丁；[喷枪] 窄矛 6°，射程约 1.6 倍集中单体。<br/>
    /// 持续压喷 3 秒「气压」渐满，焰程缩至 60%，松手回压，喷吐有呼吸节奏。
    /// 凝胶按原版节拍逐发消耗（held 内 PickAmmo，1:1）
    /// </summary>
    internal class GsFlamethrower : GodSmithScheme
    {
        public override int TargetItemID => ItemID.Flamethrower;

        public override string GsFamily => "GunsSpecial";

        protected override string GsDescFallback =>
            "Reforged: sustained fire stream with two modes. Fan sweeps a wide cone and leaves burning ground; Lance focuses a long jet"
            + "\nRight click to switch modes. Sustained spraying drains pressure and shortens the flame; ease off to recover";

        /// <summary>模式名（[0]=扇焰 [1]=喷枪），held 切换漂字用</summary>
        internal static LocalizedText[] ModeNames;

        /// <summary>下次举枪沿用的档位；只在本地玩家路径读写</summary>
        internal int preferredMode;
        /// <summary>右键预切换冷却，只在本地玩家路径读写</summary>
        private int switchCd;

        public override void GsSetStaticDefaults() {
            ModeNames = [
                this.GetLocalization("Mode0", () => "Fan Blaze"),
                this.GetLocalization("Mode1", () => "Lance Jet"),
            ];
        }

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsFlamethrowerHeld>(player)) {
                return false;
            }
            if (player.altFunctionUse == 2) {
                //举枪前右键预选档位；held 在场时的切换由 held 自己处理
                if (player.whoAmI == Main.myPlayer && switchCd <= 0) {
                    switchCd = 12;
                    preferredMode = preferredMode == 0 ? 1 : 0;
                    GsGunPose.ModeSwitchFeedback(player, ModeNames[preferredMode].Value);
                }
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsFlamethrowerHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI, preferredMode);
            }
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (switchCd > 0) {
                switchCd--;
            }
        }
    }

    /// <summary>
    /// 火焰喷射器手持弹幕：焰流生成与凝胶消耗全部自管，姿态手写。ai[0]=档位
    /// </summary>
    internal class GsFlamethrowerHeld : GsFlamerHeldBase
    {
        protected override int HeldTargetItemID => ItemID.Flamethrower;

        protected override int JetPalette => 0;

        protected override Color MuzzleColor => new(255, 140, 50);

        protected override void OnModeSwitched(int newMode) {
            if (GodSmithScheme.TryGetScheme(ItemID.Flamethrower, out GodSmithScheme scheme)
                && scheme is GsFlamethrower flamer) {
                flamer.preferredMode = newMode;
            }
            GsGunPose.ModeSwitchFeedback(Owner, GsFlamethrower.ModeNames[newMode].Value);
        }
    }
}
