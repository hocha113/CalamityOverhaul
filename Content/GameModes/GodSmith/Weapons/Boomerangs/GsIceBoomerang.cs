using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 冰回旋镖重铸。材质：寒晶冰刃。签名行为：①命中附加霜火并迸出两片穿刺碎冰
    /// ②悬停期镖体结霜，周身凝出寒雾光环 ③命中是冰晶碎裂声与青白冰屑
    /// </summary>
    internal class GsIceBoomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.IceBoomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsIceBoomerangProj>();

        internal override float DamageMul => 1.05f;

        protected override string GsDescFallback =>
            "Hits inflict Frostburn and shatter off two piercing ice shards, each dealing 25% damage\n" +
            "While hovering it frosts over, wreathed in freezing mist\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>冰刃镖体：碎冰迸射</summary>
    internal class GsIceBoomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.IceBoomerang;

        protected override Color GlowColor => new(140, 215, 255);

        protected override Color TrailColor => new(180, 235, 255);

        protected override SoundStyle HitSound => SoundID.Item27 with { Volume = 0.5f, Pitch = 0.2f };

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 180);
            //碎冰迸射：owner 端沿命中面法向掰出两片碎冰
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.25f));
                Vector2 baseDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 2; i++) {
                    Vector2 vel = baseDir.RotatedBy((i == 0 ? 1 : -1) * 0.9f)
                        * Main.rand.NextFloat(6f, 8f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, vel,
                        ModContent.ProjectileType<GsIceBoomerangShardProj>(), dmg, 0.5f, Owner.whoAmI);
                }
            }
        }

        protected override void OnHoverTick(Player owner) {
            //寒雾光环：悬停期低频冰尘环绕
            if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                Vector2 off = Main.rand.NextVector2CircularEdge(20f, 20f);
                Dust d = Dust.NewDustPerfect(Projectile.Center + off, DustID.IceTorch,
                    off * 0.02f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.25f)?.Configure(10, 0.8f);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Ice,
                    Main.rand.NextVector2Circular(4f, 4f), 60, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>穿刺碎冰：轻坠短寿命冰片，四芒星青白自绘</summary>
    internal class GsIceBoomerangShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.12f;   //轻坠弧线
            Projectile.velocity *= 0.985f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.15f, 0.25f, 0.35f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 3 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                    -Projectile.velocity * 0.1f, 120, default, 0.8f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 90);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.5f }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                    Main.rand.NextVector2Circular(2f, 2f), 60, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //速度拉伸的冰片：沿飞行向压扁的四芒星
            Vector2 stretch = new(0.11f, 0.05f);
            Color body = new Color(190, 235, 255) * 0.8f;
            body.A = 0;
            Main.spriteBatch.Draw(star, pos, null, body, Projectile.rotation,
                star.Size() / 2f, stretch, SpriteEffects.None, 0);
            Color core = Color.White * 0.7f;
            core.A = 0;
            Main.spriteBatch.Draw(star, pos, null, core, Projectile.rotation,
                star.Size() / 2f, stretch * 0.55f, SpriteEffects.None, 0);
            return false;
        }
    }
}
