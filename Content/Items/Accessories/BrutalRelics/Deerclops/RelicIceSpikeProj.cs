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
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Deerclops
{
    /// <summary>
    /// 遗物冰刺(友方刺浪单元，复用巨鹿刺笼语汇)。ai[0]=预兆帧 ai[1]=体型；
    /// 生命周期：冰裂预兆→破土(土屑+碎晶)→驻留→缩回，判定窗只在破土与驻留
    /// </summary>
    internal class RelicIceSpikeProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeerclopsIceSpike;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<WhiteoutStormCore>();

        private const int EruptTime = 7;
        private const int HoldTime = 24;
        private const int RetractTime = 12;

        private int FissureTime => Math.Max((int)Projectile.ai[0], 4);
        private float TargetScale => Projectile.ai[1];
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
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Generic;
            //一根刺对每个目标只算一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
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
            Projectile.friendly = elapsed >= FissureTime && elapsed < FissureTime + EruptTime + HoldTime;

            //破土帧：土屑+冰晶碎裂
            if (elapsed == FissureTime && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                Vector2 axis = Projectile.rotation.ToRotationVector2();
                for (int i = 0; i < 7; i++) {
                    Dust ice = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 5f),
                        DustID.Ice, axis.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 6f) * TargetScale, 80, default, Main.rand.NextFloat(0.9f, 1.6f));
                    ice.noGravity = Main.rand.NextBool();
                }
                //破土的土屑：向两侧崩开、吃重力
                for (int i = 0; i < 6; i++) {
                    Dust dirt = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 4f),
                        DustID.Dirt, new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(1.5f, 4f)), 40, default, Main.rand.NextFloat(1f, 1.5f));
                    dirt.noGravity = false;
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_DefCrystalShard>(Projectile.Center,
                        axis.RotatedByRandom(0.6f) * Main.rand.NextFloat(2.5f, 6f),
                        DeerclopsMotion.IceBlue * 0.85f, Main.rand.NextFloat(0.3f, 0.5f) * TargetScale)
                        .Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.2f, 0.2f));
                }
            }

            //预兆期渗霜
            if (elapsed < FissureTime && !Main.dedServ && Main.rand.NextBool(3)) {
                float progress = elapsed / (float)FissureTime;
                Dust seep = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f) * TargetScale, 2f),
                    DustID.Frost, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f + progress * 1.6f)), 120, default, 0.8f + progress * 0.5f);
                seep.noGravity = true;
            }

            float bodyLight = EruptProgress * RetractFactor;
            if (bodyLight > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * 20f * TargetScale,
                    DeerclopsMotion.IceBlue.ToVector3() * 0.3f * bodyLight * TargetScale);
            }
        }

        /// <summary>沿刺轴取样命中(尖端也有效)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.friendly) {
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //刺浪带短白盲(只减速+冻伤，不混乱)
            target.AddBuff(ModContent.BuffType<WhiteblindDebuff>(), 120);
        }

        #region 绘制
        /// <summary>预兆冰裂隙(顶点quad走DeerFrostFissure，无着色器则CPU回退)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            int elapsed = Elapsed;
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

            float halfLen = 30f * TargetScale + 20f;
            float halfH = 14f;
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
            //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
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
                    Texture2D glowTex = CWRAsset.SoftGlow.Value;
                    Vector2 markPos = Projectile.Center + new Vector2(0f, 6f) - Main.screenPosition;
                    Color warn = DeerclopsMotion.IceBlue with { A = 0 } * (0.6f * p * pulse);
                    Main.EntitySpriteDraw(glowTex, markPos, null, warn, 0f, glowTex.Size() / 2f,
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
            Color underColor = DeerclopsMotion.DeepIce with { A = 0 } * (0.5f * visual * retract);
            Main.EntitySpriteDraw(under, drawPos - Projectile.rotation.ToRotationVector2() * 8f * TargetScale, null, underColor,
                Projectile.rotation + MathHelper.PiOver2, under.Size() / 2f, new Vector2(0.7f, 1.1f) * TargetScale * visual, SpriteEffects.None, 0);

            //破土白闪(4向偏移残像，随爆出衰减)
            float flash = MathHelper.Clamp(1f - (Elapsed - FissureTime) / 12f, 0f, 1f);
            if (flash > 0f && visual > 0f) {
                Color flashColor = Color.White with { A = 0 } * (0.45f * flash);
                for (int i = 0; i < 4; i++) {
                    Vector2 off = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * i) * 2f * scaleVec;
                    Main.EntitySpriteDraw(tex, drawPos + off, rect, flashColor, Projectile.rotation, origin, scaleVec, SpriteEffects.None, 0);
                }
            }

            //友方刺不走boss刺的幽淡alpha路线：实体冰白、受光照染，读作"你的冰"
            Color body = Color.Lerp(lightColor, DeerclopsMotion.ColdWhite, 0.28f) * 0.9f;
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
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + axis * Main.rand.NextFloat(0f, 55f) * TargetScale,
                    DustID.Ice, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)), 100, default, Main.rand.NextFloat(0.7f, 1.2f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>
        /// 地表搜寻：从起始行先向上钻出实心，再向下找可站表面，
        /// 让刺浪贴着地形起伏(上坡爬、下坡落)
        /// </summary>
        internal static int FindSurfaceTileY(int x, int startY) {
            int y = startY;
            for (int i = 0; i < 8 && y > 20; i++) {
                if (!WorldGen.SolidTile(x, y)) {
                    break;
                }
                y--;
            }
            for (int i = 0; i < 14 && y < Main.maxTilesY - 20; i++) {
                if (WorldGen.ActiveAndWalkableTile(x, y)) {
                    break;
                }
                y++;
            }
            return y;
        }
    }
}
