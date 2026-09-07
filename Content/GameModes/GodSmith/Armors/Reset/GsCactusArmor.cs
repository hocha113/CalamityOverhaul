using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 仙人掌套 · 针衣（通用）。单件沿用原版（无属性）。<br/>
    /// 原版旗标清点：荆棘反刺（cactusThorns）→ 原样补回；无删除项。<br/>
    /// 签名：受击时向四周迸出 5 根仙人掌针（冷却 2 秒）
    /// </summary>
    internal class GsCactusArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CactusHelmet];
        public override int BodyID => ItemID.CactusBreastplate;
        public override int LegsID => ItemID.CactusLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Enemies that touch you take damage; taking damage bursts 5 cactus needles around you";

        private const int NeedleCount = 5;
        private const int NeedleDamage = 4;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.cactusThorns = true;
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || !state.TryUseCooldown(this, 120)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
            for (int i = 0; i < NeedleCount; i++) {
                //上半圆扇形迸出，受重力落回
                float angle = MathHelper.Pi + MathHelper.Pi * (i + 0.5f) / NeedleCount + Main.rand.NextFloat(-0.12f, 0.12f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 9f);
                SpawnProc(player, "GodSmithCactusEndow", player.Center, velocity,
                    ModContent.ProjectileType<GsCactusArmorNeedleProj>(), NeedleDamage, 2f);
            }
        }
    }

    /// <summary>仙人掌针：借滚仙人掌的刺贴图，迸出后受轻微重力下坠，可穿透一次，触地即碎</summary>
    internal class GsCactusArmorNeedleProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RollingCactusSpike;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.2f, 12f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.t_Cactus,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1f));
                dust.noGravity = false;
            }
        }
    }
}
