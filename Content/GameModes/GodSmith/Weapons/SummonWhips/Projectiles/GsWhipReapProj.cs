using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 暗黑收割处决「大镰收魂」：暗幕收缩后紫魂自目标涌出（主段 1.6x），
    /// 随即魂爆二段（ai[0] 传 0.6x）。暗层用真 alpha 贴图压暗，魂焰上涌
    /// </summary>
    internal class GsWhipReapSoulProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal static readonly Color SoulBright = new(224, 180, 255);
        internal static readonly Color SoulMain = new(150, 80, 220);
        internal static readonly Color SoulDeep = new(66, 30, 110);

        private const int GatherFrames = 6;
        private const int SoulWindow = 4;
        private const int BurstAt = 12;
        private const int BurstWindow = 4;
        private const int LifeFrames = 30;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? CanDamage() {
            int elapsed = Elapsed;
            if (elapsed >= GatherFrames && elapsed < GatherFrames + SoulWindow) {
                return null;
            }
            return elapsed >= BurstAt && elapsed < BurstAt + BurstWindow ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = Elapsed < BurstAt ? 100f : 70f;
            return targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(r * 2f)));
        }

        public override void AI() {
            int elapsed = Elapsed;
            if (elapsed == BurstAt) {
                Projectile.damage = Math.Max(1, (int)Projectile.ai[0]);
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.9f, Pitch = -0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.55f, Pitch = 0.1f }, Projectile.Center);
            }
            //魂焰上涌：主窗与爆窗各自补一波
            if (elapsed >= GatherFrames && elapsed < BurstAt + BurstWindow && Main.GameUpdateCount % 3 == 0) {
                PRTLoader.NewParticle<PRT_SoulFire>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), Main.rand.NextFloat(-10f, 30f)),
                    -Vector2.UnitY * Main.rand.NextFloat(2f, 4.5f),
                    SoulMain, Main.rand.NextFloat(0.5f, 0.9f));
            }
            if (elapsed == BurstAt) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center,
                        Main.rand.NextVector2Circular(4f, 4f) - Vector2.UnitY * 1.6f,
                        SoulBright, Main.rand.NextFloat(0.1f, 0.16f))?.Configure(14, 0.8f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D veil = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            Texture2D flare = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarFlare02")?.Value;
            if (veil == null || flare == null) {
                return false;
            }
            int elapsed = Elapsed;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.identity * 0.67f;
            if (elapsed < GatherFrames) {
                //暗幕收缩：真 alpha 贴图才能压暗（加色物理上无法变黑）
                float g = elapsed / (float)GatherFrames;
                Main.EntitySpriteDraw(veil, pos, null, Color.Black * (0.5f * g), seed,
                    veil.Size() * 0.5f, 2.4f - 1.2f * g, SpriteEffects.None, 0);
                return false;
            }
            float t = MathHelper.Clamp((elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames), 0f, 1f);
            float fade = 1f - t;
            //残暗底 + 紫魂柱状上冒 + 魂星闪
            Main.EntitySpriteDraw(veil, pos, null, Color.Black * (0.4f * fade), seed,
                veil.Size() * 0.5f, 1.2f + 0.6f * t, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flare, pos - new Vector2(0f, 30f * t), null,
                SoulMain with { A = 0 } * (0.8f * fade), seed + t * 0.8f,
                flare.Size() * 0.5f, 0.5f * fade + 0.12f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flare, pos - new Vector2(0f, 52f * t), null,
                SoulBright with { A = 0 } * (0.55f * fade), -seed,
                flare.Size() * 0.5f, 0.3f * fade + 0.08f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 暗黑收割「闪劈」：瞬至目标位置的一道短镰光（处决全场补劈与
    /// 跳劈传染共用，伤害由生成方折算）。ai[0] = 目标 npc.whoAmI，全程贴身
    /// </summary>
    internal class GsWhipReapFlashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 16;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= 3 && Elapsed < 8 ? null : false;

        public override void AI() {
            int idx = (int)Projectile.ai[0];
            if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                Projectile.Center = Main.npc[idx].Center;
            }
            if (Elapsed == 3 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = 0.3f }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_SoulFire>(Projectile.Center,
                        Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY,
                        GsWhipReapSoulProj.SoulMain, Main.rand.NextFloat(0.35f, 0.6f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D edge = CWRUtils.GetT2DAsset(CWRConstant.Masking + "CrescentEdge01")?.Value;
            if (edge == null) {
                return false;
            }
            //短镰光快扫：identity 定起始摆角，双层紫弧交错渐隐
            float t = Elapsed / (float)LifeFrames;
            float fade = 1f - t;
            float baseRot = Projectile.identity * 0.91f;
            float sweep = baseRot + MathHelper.Lerp(-0.9f, 0.9f, MathF.Min(1f, t * 2f));
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(edge, pos, null,
                GsWhipReapSoulProj.SoulBright with { A = 0 } * (0.8f * fade),
                sweep, edge.Size() * 0.5f, new Vector2(0.95f, 0.6f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(edge, pos, null,
                GsWhipReapSoulProj.SoulMain with { A = 0 } * (0.5f * fade),
                sweep - 0.3f, edge.Size() * 0.5f, new Vector2(0.75f, 0.48f), SpriteEffects.None, 0);
            return false;
        }
    }
}
