using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 流星套 · 陨星坠落（魔法）。单件沿用原版（三件各 +9% 魔法伤害）。<br/>
    /// 原版旗标清点：太空枪零魔耗（spaceGun）→ 原样补回；无删除项。<br/>
    /// 签名：魔法命中 15% 概率（冷却 1 秒）从目标上空召来一颗小陨石坠向目标，落点点燃 2 秒；
    /// 出生点走地形探顶 + 高度门，洞穴里照常落到目标
    /// </summary>
    internal class GsMeteorArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.MeteorHelmet];
        public override int BodyID => ItemID.MeteorSuit;
        public override int LegsID => ItemID.MeteorLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Space Gun costs 0 mana; magic hits have a 15% chance to call a small meteor down onto the target, setting it on fire";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.spaceGun = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Magic)
                || target.life <= 0 || Main.rand.Next(100) >= 15 || !state.TryUseCooldown(this, 60)) {
                return;
            }
            Vector2 spawn = GsArmorTerrainProbe.SkySpawnAbove(target.Center, Main.rand.NextFloat(-40f, 40f), 260f);
            Vector2 velocity = (target.Center - spawn).SafeNormalize(Vector2.UnitY) * 11f;
            SpawnProc(player, "GodSmithMeteorEndow", spawn, velocity,
                ModContent.ProjectileType<GsMeteorArmorFallProj>(), ProcDamage(damageDone, 0.6f, 6, 20), 3f,
                target.Center.Y, Main.rand.Next(3));
        }
    }

    /// <summary>
    /// 陨星：借原版三种陨石贴图（ai[1] 选款），出生免地形碰撞、越过标的线（ai[0]）后恢复；
    /// 坠落加速自转，拖陨石尘，命中点燃，落地小爆只用原版火尘与爆炸音
    /// </summary>
    internal class GsMeteorArmorFallProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Meteor1;

        private ref float TargetLineY => ref Projectile.ai[0];

        private int Variant => (int)MathHelper.Clamp(Projectile.ai[1], 0f, 2f);

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            GsArmorTerrainProbe.UpdateFallGate(Projectile, TargetLineY);
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.15f, 16f);
            Projectile.rotation += 0.2f * Projectile.direction;
            Lighting.AddLight(Projectile.Center, 0.7f, 0.35f, 0.1f);
            if (Main.dedServ) {
                return;
            }
            Dust trail = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Meteorite,
                -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 100, default, 1.3f);
            trail.noGravity = true;
            if (Main.rand.NextBool(2)) {
                Dust fire = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f, 100, default, 1.4f);
                fire.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 120);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 14; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    i % 3 == 0 ? DustID.Meteorite : DustID.Torch, 0f, 0f, 100, default, 1.6f);
                dust.velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int id = ProjectileID.Meteor1 + Variant;
            Main.instance.LoadProjectile(id);
            Texture2D tex = TextureAssets.Projectile[id].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, tex.Size() * 0.5f, 0.8f, SpriteEffects.None);
            return false;
        }
    }
}
