using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 银短剑重铸「月银」。<br/>
    /// 材质：吸饱月光的镜面银刃。签名行为：①夜间伤害 ×1.25 且刺速 +10%
    /// ②血月之夜命中吸提生机，每秒至多回 1 点生命
    /// </summary>
    internal class GsSilverShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.SilverShortsword;

        protected override string GsDescFallback =>
            "Reforged: mirror-polished silver that drinks moonlight, striking 25% harder and 10% faster at night;\nunder a blood moon each hit steals back a sliver of life";
        protected override int HeldProjType => ModContent.ProjectileType<GsSilverShortswordHeld>();

        /// <summary>血月吸提的上次结算刻（myPlayer 路径消费，限频每秒 1 点）</summary>
        private uint lastHealTick;

        /// <summary>血月吸提（owner 端调用，自限频 60 刻）</summary>
        internal void TryBloodMoonHeal(Player player) {
            if (!Main.bloodMoon || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.statLife >= player.statLifeMax2 || Main.GameUpdateCount - lastHealTick < 60) {
                return;
            }
            lastHealTick = Main.GameUpdateCount;
            player.Heal(1);
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= !Main.dayTime ? 1.40f : 1.12f;//底伤 1.12，夜间签名乘区 ×1.25 = 1.40（约半程 uptime）
    }

    /// <summary>
    /// 银短剑手持突刺：轻捷镜面手感。夜间刺速 +10%；
    /// 血月命中经方案限频回血
    /// </summary>
    internal class GsSilverShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.SilverShortsword;

        protected override float WindupFrames => 2f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 6f;
        protected override float PullbackDist => 9f;
        protected override float StabReach => 33f;
        protected override float BladeLength => 43f;
        protected override float ThrustEasePower => 2.7f;
        protected override int HitstopFrames => 1;
        protected override float LeanAmp => 0.028f;
        protected override float ThrustPitch => 0.18f;

        private static bool IsNight => !Main.dayTime;

        protected override void OnInit() {
            //月照加速：夜间刺速 +10%（伤害乘区在方案 GsModifyWeaponDamage 动态结算）
            if (IsNight) {
                speedMul *= 1.10f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //血月吸提：限频结算在方案侧（owner 端命中 + myPlayer 双守门）
            if (Owner.whoAmI == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.SilverShortsword, out GodSmithScheme s)
                && s is GsSilverShortsword silver) {
                silver.TryBloodMoonHeal(Owner);
            }
        }
    }
}
