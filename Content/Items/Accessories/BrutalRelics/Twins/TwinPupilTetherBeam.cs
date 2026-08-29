using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Twins
{
    /// <summary>
    /// 双眼之间的切割系绳。两端位置每帧从两只眼本地解析(owner+类型+ai0 唯一标识，跨端等价，槽位缓存)，
    /// 弹幕天然同步在所有端可见；切割伤害走整线段碰撞+本地免疫15帧节流，命中撕开创口
    /// </summary>
    internal class TwinPupilTetherBeam : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float HitWidth = 30f;
        private const int ExpandTime = 14;

        private Vector2 endA;//视界端(红)
        private Vector2 endB;//焚瞳端(绿)
        private bool linked;
        private float power;//0~1 展开度
        private int cutFlash;//近期切中的增亮计时
        //两眼槽位缓存：缓存有效时免全表扫
        private int cachedEyeA = -1;
        private int cachedEyeB = -1;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;//切割节流：同一目标每秒至多4割
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }
            TwinPupilTetherPlayer mp = owner.GetModPlayer<TwinPupilTetherPlayer>();
            if (mp.TetherEquipped && !owner.dead) {
                Projectile.timeLeft = 2;
            }

            ResolveEyes();
            if (!linked) {
                power = Math.Max(power - 0.12f, 0f);
                return;
            }

            Projectile.Center = (endA + endB) / 2f;
            power = Math.Min(power + 1f / ExpandTime, 1f);
            if (cutFlash > 0) {
                cutFlash--;
            }

            //owner 端逐帧刷新切割伤害
            if (Projectile.owner == Main.myPlayer) {
                Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Generic)
                    .ApplyTo(TwinPupilTether.TetherDamage);
            }

            //沿线双色光照与低频能量火花
            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Vector2.Lerp(endA, endB, i / 3f),
                    Color.Lerp(TwinPupilTether.LaserColor, TwinPupilTether.FlameColor, i / 3f).ToVector3() * 0.3f * power);
            }
            if (!VaultUtils.isServer && power > 0.6f && Main.rand.NextBool(5)) {
                float t = Main.rand.NextFloat();
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(Vector2.Lerp(endA, endB, t),
                    Main.rand.NextVector2Circular(2.5f, 2.5f), Color.White,
                    Main.rand.NextFloat(0.7f, 1.2f))?.Configure(12, t < 0.5f ? 0 : 1);
            }
        }

        /// <summary>按槽位缓存解析一只眼(owner+类型+ai0 唯一，跨端等价)，缓存失效才全表重扫</summary>
        private Projectile ResolveEye(ref int cache, float eyeKind) {
            int orbiterType = ModContent.ProjectileType<TwinPupilOrbiter>();
            if (cache >= 0 && cache < Main.maxProjectiles) {
                Projectile p = Main.projectile[cache];
                if (p.active && p.type == orbiterType && p.owner == Projectile.owner && p.ai[0] == eyeKind) {
                    return p;
                }
            }
            cache = -1;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == orbiterType && p.owner == Projectile.owner && p.ai[0] == eyeKind) {
                    cache = p.whoAmI;
                    return p;
                }
            }
            return null;
        }

        private void ResolveEyes() {
            linked = false;
            Projectile eyeA = ResolveEye(ref cachedEyeA, 0f);
            Projectile eyeB = ResolveEye(ref cachedEyeB, 1f);
            if (eyeA == null || eyeB == null) {
                return;
            }
            //两端收口在瞳孔处(朝向前端)，朝向从眼的旋转约定反解
            Vector2 faceA = (eyeA.rotation - MathHelper.Pi).ToRotationVector2();
            Vector2 faceB = (eyeB.rotation - MathHelper.Pi).ToRotationVector2();
            endA = eyeA.Center + faceA * 10f;
            endB = eyeB.Center + faceB * 10f;
            linked = true;
        }

        //两眼贴脸时没有刃可言；未展满不咬人
        public override bool? CanDamage()
            => linked && power >= 0.55f && Vector2.DistanceSquared(endA, endB) > 55f * 55f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!linked) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                endA, endB, HitWidth * power, ref _);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => TwinPupilRendNPC.ApplyRendBonus(target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //切割撕开创口(命中判定端本地记账)
            if (target.TryGetGlobalNPC(out TwinPupilRendNPC rend)) {
                rend.ApplyRend();
            }
            //切割拍：命中点双向迸溅+音效，靠 cutFlash 节流不连响
            if (cutFlash <= 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.35f }, target.Center);
            }
            cutFlash = 10;
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 ab = endB - endA;
            if (ab.LengthSquared() < 1f) {
                return;
            }
            float t = MathHelper.Clamp(Vector2.Dot(target.Center - endA, ab) / ab.LengthSquared(), 0f, 1f);
            Vector2 cutPos = endA + ab * t;
            Vector2 perp = ab.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 6; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(cutPos,
                    perp * side * Main.rand.NextFloat(2f, 7f)
                    + ab.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(-2f, 2f),
                    Color.White, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(15, t < 0.5f ? 0 : 1);
            }
        }

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            if (!linked || power <= 0.03f) {
                return false;
            }
            float len = Vector2.Distance(endA, endB);
            if (len < 8f) {
                return false;
            }

            if (EffectLoader.BRelicTwinTether?.Value != null) {
                DrawShaderTether(len);
            }
            else {
                DrawFallbackTether(len);
            }

            //两端瞳孔收口辉光
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 1f + 0.14f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f);
            Main.EntitySpriteDraw(glow, endA - Main.screenPosition, null,
                (TwinPupilTether.LaserColor with { A = 0 }) * (0.8f * power), 0f,
                glow.Size() / 2f, 0.3f * pulse * power, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, endB - Main.screenPosition, null,
                (TwinPupilTether.FlameColor with { A = 0 }) * (0.8f * power), 0f,
                glow.Size() / 2f, 0.3f * pulse * power, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>着色器系绳：死光语汇细化版，双端瞳孔收口+红绿对冲能流</summary>
        private void DrawShaderTether(float len) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.BRelicTwinTether.Value;
            shader.Parameters["uColorA"]?.SetValue(TwinPupilTether.LaserColor.ToVector3());
            shader.Parameters["uColorB"]?.SetValue(TwinPupilTether.FlameColor.ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.5f);
            shader.Parameters["uOpacity"]?.SetValue(power);
            shader.Parameters["uIntensity"]?.SetValue(1.05f);
            shader.Parameters["uCutFlash"]?.SetValue(cutFlash / 10f);
            shader.Parameters["uLenScale"]?.SetValue(len / 240f);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.Parameters["uImage2"]?.SetValue(CWRAsset.PerlinNoise.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            //视觉宽比判定宽厚，撕裂缘需要余量；判定(30*power)藏在可见亮体内
            float visualWidth = HitWidth * power * 2.9f * (1f + cutFlash / 10f * 0.35f);
            sb.Draw(quad, endA - Main.screenPosition, null, Color.White,
                (endB - endA).ToRotation(), new Vector2(0f, quad.Height / 2f),
                new Vector2(len / quad.Width, visualWidth / quad.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>回退系绳：分段光刃+正弦包络，两端自然归零不平切</summary>
        private void DrawFallbackTether(float len) {
            Texture2D lineTex = CWRAsset.LightShot.Value;
            Vector2 dir = (endB - endA) / len;
            int segments = Math.Max((int)(len / 26f), 3);
            for (int i = 0; i < segments; i++) {
                float t = (i + 0.5f) / segments;
                Vector2 segPos = endA + dir * len * t - Main.screenPosition;
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                Color col = Color.Lerp(TwinPupilTether.LaserColor, TwinPupilTether.FlameColor, t) with { A = 0 };
                Main.EntitySpriteDraw(lineTex, segPos, null, col * (0.75f * power * envelope),
                    dir.ToRotation(), new Vector2(0f, lineTex.Height / 2f),
                    new Vector2(0.14f, 0.2f * power * envelope + 0.04f), SpriteEffects.None, 0);
            }
        }

        #endregion
    }
}
