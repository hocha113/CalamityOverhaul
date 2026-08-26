using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes._Benchmark
{
    /// <summary>
    /// 【范例·行为型神赋】剑圣回响：覆盖近战顶级词缀（传奇/泰拉传奇），
    /// 挥砍时概率放出金色剑气。行为型神赋 = 在使用/命中钩子里做动作：
    /// 权威动作守 player.whoAmI == Main.myPlayer，弹幕随 NewProjectile 自动同步。<br/>
    /// 弹幕贴图复用原版（禁新增资源），全部数值随出生参数携带（生成包时机纪律）
    /// </summary>
    internal class GodSmithLegendaryEndow : GodSmithEndow
    {
        /// <summary>触发概率（百分比）</summary>
        internal const int WaveChance = 30;

        /// <summary>剑气伤害占武器伤害比（百分比，顶级档基准）</summary>
        internal const int WaveDamagePercent = 60;

        public override int[] CoveredPrefixes => [PrefixID.Legendary, PrefixID.Legendary2];

        protected override string EndowNameFallback => "Sword Saint's Echo";

        protected override string EndowDescFallback =>
            "Swings have a {0}% chance to loose a golden sword wave dealing {1}% weapon damage";

        public override object[] DescFormatArgs(Item item) => [WaveChance, WaveDamagePercent];

        public override void OnUseAnimation(Item item, Player player, float tierScale) {
            //权威动作只在 owner 端：随机 roll 也只在这一端做，结果随弹幕同步
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= WaveChance) {
                return;
            }
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            int damage = (int)(player.GetWeaponDamage(item) * (WaveDamagePercent / 100f) * tierScale);
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, aim * 11f,
                ModContent.ProjectileType<GodSmithSwordWave>(), damage, item.knockBack, player.whoAmI);
        }
    }

    /// <summary>金色剑气：贴图复用原版光明之刃剑气，出鞘短促加速后滑行渐隐。
    /// 自身出生源是武器（ItemUse），命中会再走一次神赋命中钩子；本神赋命中钩子为空，无级联</summary>
    internal class GodSmithSwordWave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 48;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            //出鞘 8 个更新帧微加速，避免全程匀速的呆板飞行
            if (Projectile.timeLeft > 40) {
                Projectile.velocity *= 1.04f;
            }
            //收尾渐隐
            if (Projectile.timeLeft < 12) {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 24);
            }
            Lighting.AddLight(Projectile.Center, 0.42f, 0.34f, 0.12f);
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    -Projectile.velocity * 0.15f, 100, default, 1.1f);
                dust.noGravity = true;
            }
        }

        //鎏金自发光，随渐隐收敛
        public override Color? GetAlpha(Color lightColor) => new Color(255, 226, 142, 80) * Projectile.Opacity;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
