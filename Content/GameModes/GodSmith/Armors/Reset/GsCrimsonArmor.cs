using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 血腥套：三件各防御 +5，伤害合计 +10%（4/3/3）；
    /// 套装奖励为攻击时将 5% 伤害转为治疗（上限 15，每 5 秒一次），且生命越低防御越高，30% 生命时达 1.05 倍
    /// </summary>
    internal class GsCrimsonArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CrimsonHelmet];
        public override int BodyID => ItemID.CrimsonScalemail;
        public override int LegsID => ItemID.CrimsonGreaves;

        protected override string HeadLineFallback => "5 more defense and 4% increased damage";
        protected override string BodyLineFallback => "5 more defense and 3% increased damage";
        protected override string LegsLineFallback => "5 more defense and 3% increased damage";
        protected override string SetBonusLineFallback =>
            "Attacks heal you for 5% of the damage dealt (up to 15, once every 5 seconds); defense rises as your life falls, up to 1.05x at 30% life";

        /// <summary>回血冷却帧数</summary>
        private const int HealCooldown = 300;

        public override void UpdateHead(Player player, Item item) {
            player.statDefense += 5;
            player.GetDamage(DamageClass.Generic) += 0.04f;
        }

        public override void UpdateBody(Player player, Item item) {
            player.statDefense += 5;
            player.GetDamage(DamageClass.Generic) += 0.03f;
        }

        public override void UpdateLegs(Player player, Item item) {
            player.statDefense += 5;
            player.GetDamage(DamageClass.Generic) += 0.03f;
        }

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            //满血 1.0 倍，线性升到 30% 生命的 1.05 倍并封顶
            float lifeRatio = player.statLifeMax2 > 0 ? player.statLife / (float)player.statLifeMax2 : 1f;
            float t = MathHelper.Clamp((1f - lifeRatio) / 0.7f, 0f, 1f);
            player.statDefense *= 1f + 0.05f * t;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            HealOnHit(player, state, damageDone, 0.05f, 15, HealCooldown);
        }
    }
}
