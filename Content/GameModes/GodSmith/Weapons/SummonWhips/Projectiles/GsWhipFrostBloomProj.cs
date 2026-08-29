using CalamityOverhaul.Common;
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
    /// 酷鞭处决「冰锁绽放」：聚霜、冰爆（1.5x 主段 + 霜焚）、
    /// 冰晶迸裂（ai[0] 传 0.5x 二段）。冰环走共享参数化冲击环（撕裂缘+厚度生命周期）
    /// </summary>
    internal class GsWhipFrostBloomProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color FrostBright = new(226, 248, 255);
        private static readonly Color FrostMain = new(120, 210, 255);
        private static readonly Color FrostDeep = new(52, 96, 168);

        private const int GatherFrames = 5;
        private const int BloomWindow = 4;    //主爆窗
        private const int CrackAt = 11;       //二段起点
        private const int CrackWindow = 4;
        private const int LifeFrames = 28;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 180;
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
            if (elapsed >= GatherFrames && elapsed < GatherFrames + BloomWindow) {
                return null;
            }
            return elapsed >= CrackAt && elapsed < CrackAt + CrackWindow ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //主爆 90px、迸裂收 60px
            float r = Elapsed < CrackAt ? 90f : 60f;
            return targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(r * 2f)));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn2, 240);

        public override void AI() {
            int elapsed = Elapsed;
            //二段起点：伤害切迸裂口径
            if (elapsed == CrackAt) {
                Projectile.damage = Math.Max(1, (int)Projectile.ai[0]);
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (elapsed < GatherFrames && Main.GameUpdateCount % 2 == 0) {
                //聚霜：冷雾绕爆心盘旋收拢（轨道型雾按其合同喂轨心与半径）
                Vector2 offset = Main.rand.NextVector2CircularEdge(50f, 50f);
                PRTLoader.NewParticle<PRT_DefCryoMist>(Projectile.Center + offset,
                    Vector2.Zero, FrostMain, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(18, Projectile.Center, offset.Length());
            }
            if (elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.9f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_DefFrostGlint>(Projectile.Center,
                        Main.rand.NextVector2Circular(6f, 6f), FrostBright,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24));
                }
            }
            if (elapsed == CrackAt) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = 0.25f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_DefFrostGlint>(Projectile.Center,
                        Main.rand.NextVector2Circular(4f, 4f), FrostMain,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 18));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D flare = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarFlare02")?.Value;
            if (flare == null) {
                return false;
            }
            int elapsed = Elapsed;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.identity * 0.57f;
            if (elapsed < GatherFrames) {
                float g = elapsed / (float)GatherFrames;
                Main.EntitySpriteDraw(flare, pos, null, FrostMain with { A = 0 } * (0.6f * g),
                    seed, flare.Size() * 0.5f, 0.12f + 0.1f * g, SpriteEffects.None, 0);
                return false;
            }
            //冰爆波前：共享参数化冲击环，环缘撕裂、扩张同时变薄（判定主爆 90px 收在环带之内）
            float t = MathHelper.Clamp((elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames), 0f, 1f);
            float fade = 1f - t;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                MathHelper.Lerp(38f, 150f, 1f - fade * fade), 12f + 22f * fade,
                FrostBright, FrostMain, FrostDeep, 0.85f * fade,
                innerGlow: 0.22f * fade, timeSeed: seed);
            //二段迸裂：一道更小更锐的内环追出来，与迸裂判定同拍
            if (elapsed >= CrackAt) {
                float ct = MathHelper.Clamp((elapsed - CrackAt) / (float)(LifeFrames - CrackAt), 0f, 1f);
                float cFade = 1f - ct;
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                    MathHelper.Lerp(16f, 84f, 1f - cFade * cFade), 7f + 9f * cFade,
                    FrostBright, FrostBright, FrostMain, 0.70f * cFade,
                    tearPx: 8f, timeSeed: seed + 1.7f);
            }
            //六向冰晶闪
            float starFade = MathF.Max(0f, fade - 0.2f);
            Main.EntitySpriteDraw(flare, pos, null, FrostBright with { A = 0 } * (0.9f * starFade),
                seed, flare.Size() * 0.5f, 0.3f * fade + 0.08f, SpriteEffects.None, 0);
            return false;
        }
    }
}
