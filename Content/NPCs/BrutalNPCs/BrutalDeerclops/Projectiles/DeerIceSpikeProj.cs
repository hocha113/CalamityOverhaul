using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles
{
    /// <summary>
    /// 冰刺(地形雕刻)。ai[0]=预兆帧 ai[1]=体型 ai[2]=0标准/1长驻牢笼壁；
    /// 生命周期：冰裂预兆→破土→驻留→退场，判定窗只在破土与驻留
    /// </summary>
    internal class DeerIceSpikeProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeerclopsIceSpike;

        private const int EruptTime = 8;
        private const int RetractTime = 14;

        private int FissureTime => Math.Max((int)Projectile.ai[0], 4);
        private float TargetScale => Projectile.ai[1];
        private bool LongHold => Projectile.ai[2] == 1f;
        private int HoldTime => LongHold ? 66 : 26;
        private int TotalLife => FissureTime + EruptTime + HoldTime + RetractTime;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>破土程度 0~1(poly(6)爆出)</summary>
        private float EruptProgress {
            get {
                int t = Elapsed - FissureTime;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= EruptTime) {
                    return 1f;
                }
                float x = t / (float)EruptTime;
                return 1f - (float)Math.Pow(1f - x, 6);
            }
        }

        /// <summary>退场收缩 1→0</summary>
        private float RetractFactor {
            get {
                int t = Elapsed - FissureTime - EruptTime - HoldTime;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)RetractTime, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
            Projectile.coldDamage = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                Projectile.rotation = Projectile.velocity.ToRotation();
                //帧样式按标识确定，各端一致
                Projectile.frame = Projectile.identity % 5;
            }

            int elapsed = Elapsed;

            //判定窗与可见破土精确对齐
            Projectile.hostile = elapsed >= FissureTime && elapsed < FissureTime + EruptTime + HoldTime;

            //破土帧：爆发表现
            if (elapsed == FissureTime && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.85f, MaxInstances = 4 }, Projectile.Center);
                Vector2 axis = Projectile.rotation.ToRotationVector2();
                for (int i = 0; i < 9; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 6f),
                        DustID.Ice, axis.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 7f) * TargetScale, 80, default, Main.rand.NextFloat(1f, 1.8f));
                    dust.noGravity = Main.rand.NextBool();
                }
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_ATShard>(Projectile.Center, axis.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 8f),
                        DeerclopsMotion.IceBlue * 0.85f, Main.rand.NextFloat(0.3f, 0.55f) * TargetScale)
                        .Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.2f, 0.2f));
                }
            }

            //预兆期渗霜
            if (elapsed < FissureTime && !Main.dedServ && Main.rand.NextBool(3)) {
                float progress = elapsed / (float)FissureTime;
                Dust seep = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-18f, 18f) * TargetScale, 2f),
                    DustID.Frost, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.6f + progress * 2f)), 120, default, 0.9f + progress * 0.6f);
                seep.noGravity = true;
            }

            float bodyLight = EruptProgress * RetractFactor;
            if (bodyLight > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * 20f * TargetScale,
                    DeerclopsMotion.IceBlue.ToVector3() * 0.35f * bodyLight * TargetScale);
            }
        }

        /// <summary>沿刺轴取样命中(尖端也有效)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float visual = EruptProgress * RetractFactor;
            if (visual < 0.35f) {
                return false;
            }
            Vector2 axis = Projectile.rotation.ToRotationVector2();
            float reach = 128f * TargetScale * visual;
            float radius = 6f + 15f * TargetScale;
            for (int i = 0; i < 3; i++) {
                Vector2 point = Projectile.Center + axis * reach * (0.12f + 0.38f * i);
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(radius * 2f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        #region 绘制
        /// <summary>预兆冰裂隙(顶点quad走DeerFrostFissure，无着色器则CPU回退)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            int elapsed = Elapsed;
            //预兆全程+破土后渐隐
            float fissureFade;
            if (elapsed < FissureTime) {
                fissureFade = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            }
            else {
                fissureFade = MathHelper.Clamp(1f - (elapsed - FissureTime) / 20f, 0f, 1f);
            }
            if (fissureFade <= 0.01f) {
                return;
            }

            float progress = MathHelper.Clamp(elapsed / (float)FissureTime, 0f, 1f);
            Effect effect = EffectLoader.DeerFrostFissure?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float halfLen = 30f * TargetScale + 22f;
            float halfH = 15f;
            Vector2 basePos = Projectile.Center + new Vector2(0f, 6f);
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(basePos.X - halfLen, basePos.Y - halfH, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(basePos.X - halfLen, basePos.Y + halfH, 0f), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture(new Vector3(basePos.X + halfLen, basePos.Y - halfH, 0f), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture(new Vector3(basePos.X + halfLen, basePos.Y + halfH, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uFade"]?.SetValue(fissureFade);
            effect.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 1f);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        public override bool PreDraw(ref Color lightColor) {
            float visual = EruptProgress;
            if (visual <= 0.01f) {
                //着色器缺席时的预兆CPU回退：拉宽冷光斑，公平性不依赖可选资源
                if (EffectLoader.DeerFrostFissure?.Value == null && Elapsed > 2) {
                    float p = MathHelper.Clamp(Elapsed / (float)FissureTime, 0f, 1f);
                    float pulse = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity);
                    Texture2D glow = CWRAsset.SoftGlow.Value;
                    Vector2 markPos = Projectile.Center + new Vector2(0f, 6f) - Main.screenPosition;
                    Color warn = DeerclopsMotion.IceBlue with { A = 0 } * (0.65f * p * pulse);
                    Main.EntitySpriteDraw(glow, markPos, null, warn, 0f, glow.Size() / 2f,
                        new Vector2(1.1f * TargetScale + 0.5f, 0.35f), SpriteEffects.None, 0);
                }
                return false;
            }

            Main.instance.LoadProjectile(ProjectileID.DeerclopsIceSpike);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.DeerclopsIceSpike].Value;
            Rectangle rect = tex.Frame(1, 5, 0, Projectile.frame);
            Vector2 origin = new Vector2(16f, rect.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float retract = RetractFactor;
            //沿轴爆出、退场沿轴缩回
            Vector2 scaleVec = new Vector2(visual * retract, MathHelper.Lerp(0.55f, 1f, visual) * MathHelper.Lerp(0.4f, 1f, retract)) * TargetScale;

            //底部冷光衬
            Texture2D under = CWRAsset.Extra_98.Value;
            Color underColor = DeerclopsMotion.DeepIce with { A = 0 } * (0.55f * visual * retract);
            Main.EntitySpriteDraw(under, drawPos - Projectile.rotation.ToRotationVector2() * 8f * TargetScale, null, underColor,
                Projectile.rotation + MathHelper.PiOver2, under.Size() / 2f, new Vector2(0.7f, 1.1f) * TargetScale * visual, SpriteEffects.None, 0);

            //破土白闪(4向偏移残像，随爆出衰减)
            float flash = MathHelper.Clamp(1f - (Elapsed - FissureTime) / 14f, 0f, 1f);
            if (flash > 0f && visual > 0f) {
                Color flashColor = Color.White with { A = 0 } * (0.5f * flash);
                for (int i = 0; i < 4; i++) {
                    Vector2 off = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * i) * 2f * scaleVec;
                    Main.EntitySpriteDraw(tex, drawPos + off, rect, flashColor, Projectile.rotation, origin, scaleVec, SpriteEffects.None, 0);
                }
            }

            Color body = Projectile.GetAlpha(lightColor);
            body = Color.Lerp(body, DeerclopsMotion.ColdWhite, 0.18f);
            Main.EntitySpriteDraw(tex, drawPos, rect, body * retract, Projectile.rotation, origin, scaleVec, SpriteEffects.None, 0);
            return false;
        }
        #endregion

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //碎冰退场
            Vector2 axis = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + axis * Main.rand.NextFloat(0f, 60f) * TargetScale,
                    DustID.Ice, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)), 100, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>
        /// 服务端便捷生成：在(x基准)地表长一根冰刺，返回是否成功。
        /// lean为倾角(弧度，带向)，telegraph为预兆帧
        /// </summary>
        internal static bool TrySpawn(NPC npc, int tileX, int baseTileY, float lean, float scale, int telegraph, int damage, bool longHold = false) {
            if (VaultUtils.isClient) {
                return false;
            }
            Point source = new Point(tileX, baseTileY);
            int bestY = DeerclopsMotion.FindSpikeY(npc, source, tileX);
            if (!WorldGen.ActiveAndWalkableTile(tileX, bestY)) {
                return false;
            }
            Vector2 pos = new Vector2(tileX * 16f + 8f, bestY * 16f - 8f);
            Vector2 dir = (-Vector2.UnitY).RotatedBy(lean);
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, dir,
                ModContent.ProjectileType<DeerIceSpikeProj>(), damage, 0f, Main.myPlayer,
                telegraph, scale, longHold ? 1f : 0f);
            return true;
        }
    }
}
