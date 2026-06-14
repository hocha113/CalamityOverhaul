using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye
{
    /// <summary>双子魔眼持续性死亡射线：锚定眼睛，方向随NPC旋转(AI驱动扫射)；ai[0]=宿主NPC的whoAmI；ai[1]=总持续时间(帧)；ai[2]=主题0=激光眼青紫死光1=魔焰眼烈焰射线；展开/收束缓动，未完全展开无伤害；宿主死亡时快速收束</summary>
    internal class RetinazerDeathRay : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        private const float MaxBeamLength = 2800f;
        private const int ExpandTime = 18;
        private const int CollapseTime = 16;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Owner => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private int Duration => (int)Projectile.ai[1];
        private bool FlameTheme => Projectile.ai[2] == 1f;

        private float beamWidth;
        private float beamLength;

        private float MaxWidth => FlameTheme ? 52f : 44f;
        private Color ThemeColor => FlameTheme ? new Color(255, 110, 35) : new Color(120, 200, 255);
        private Color ThemeGlow => FlameTheme ? new Color(255, 220, 120) : new Color(150, 110, 255);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC owner = Owner;

            //宿主失效时立即进入收束
            if (!owner.Alives()) {
                if (Timer < Duration - CollapseTime) {
                    Timer = Duration - CollapseTime;
                }
                if (Timer >= Duration) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.8f, Pitch = FlameTheme ? -0.25f : 0.1f, MaxInstances = 3 }, Projectile.Center);
            }

            //宽度展开/收束缓动
            float collapseStart = Duration - CollapseTime;

            //锚定与朝向:跟随宿主旋转(状态机驱动npc.rotation完成扫射)
            if (owner.Alives() && Timer < collapseStart) {
                Vector2 dir = (owner.rotation + MathHelper.PiOver2).ToRotationVector2();
                Projectile.Center = owner.Center + dir * 46f;
                Projectile.rotation = dir.ToRotation();
            }

            if (Timer < ExpandTime) {
                float t = Timer / ExpandTime;
                beamWidth = MathHelper.Lerp(2f, MaxWidth, VaultUtils.EaseOutCubic(t));
                beamLength = MathHelper.Lerp(0f, MaxBeamLength, VaultUtils.EaseOutQuad(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
                beamLength = MaxBeamLength;
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxBeamLength;
            }

            //呼吸脉动
            beamWidth *= 1f + 0.07f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 34f);

            Timer++;
            if (Timer >= Duration) {
                Projectile.Kill();
                return;
            }

            //沿光束的光照与粒子
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 6f * i), ThemeColor.ToVector3() * 0.7f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //沿线能量火花
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 sparkPos = Projectile.Center + beamDir * beamLength * along
                    + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                PRTLoader.NewParticle<PRT_TwinsSpark>(sparkPos,
                    beamDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 6f),
                    Color.White, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(16, FlameTheme ? 1 : 0);
            }

            //枪口聚能粒子(向心汇聚)
            if (Main.rand.NextBool(3)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
                PRTLoader.NewParticle<PRT_TwinsSpark>(gatherPos,
                    (Projectile.Center - gatherPos) * 0.12f,
                    Color.White, Main.rand.NextFloat(1f, 1.5f))?.Configure(14, FlameTheme ? 1 : 0);
            }
        }

        //未完全展开时不造成伤害，给玩家反应窗口
        public override bool? CanDamage() => Timer > ExpandTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            //碰撞宽度略小于视觉宽度，宽容判定
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.72f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(FlameTheme ? BuffID.OnFire3 : BuffID.Electrified, 90);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (beamWidth <= 0.5f || beamLength <= 10f) {
                return false;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;

            Color outer = ThemeColor with { A = 0 };
            Color mid = ThemeGlow with { A = 0 };
            Color core = Color.White with { A = 0 };

            float flicker = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 47f);

            if (EffectLoader.TwinsDeathRayBeam?.Value != null) {
                DrawShaderBeam(rot);
            }
            else {
                DrawFallbackBeam(drawPos, rot, outer, mid, core, flicker);
            }

            //枪口辉光:多层呼吸光球+十字星闪
            float muzzleScale = beamWidth / MaxWidth;
            Main.EntitySpriteDraw(glow, drawPos, null, outer * 0.9f, 0f, glow.Size() / 2f,
                muzzleScale * 1.9f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f,
                muzzleScale * 0.95f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, mid * 0.8f, Main.GlobalTimeWrappedHourly * 3f,
                star.Size() / 2f, muzzleScale * 0.5f * flicker, SpriteEffects.None, 0);

            return false;
        }

        /// <summary>着色器死光:流动电浆/烈焰光柱，边缘噪声撕裂，主题色由ai[2]决定</summary>
        private void DrawShaderBeam(float rot) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.TwinsDeathRayBeam.Value;
            float expandProgress = MathHelper.Clamp(Timer / ExpandTime, 0f, 1f);
            shader.Parameters["uColor"]?.SetValue(ThemeColor.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(ThemeGlow.ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.6f);
            shader.Parameters["uOpacity"]?.SetValue(1f);
            shader.Parameters["uIntensity"]?.SetValue(1.05f);
            shader.Parameters["uPulseSpeed"]?.SetValue(FlameTheme ? 9f : 13f);
            shader.Parameters["uFlameMode"]?.SetValue(FlameTheme ? 1f : 0f);
            shader.Parameters["uExpandProgress"]?.SetValue(expandProgress);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.Parameters["uImage2"]?.SetValue(CWRAsset.PerlinNoise.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            //光柱面片:着色器在UV空间内完成全部造型
            Texture2D quad = CWRAsset.Placeholder_White.Value;
            //视觉宽度比碰撞宽度更厚，撕裂边缘需要余量
            float visualWidth = beamWidth * 3.4f;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White, rot,
                new Vector2(0, quad.Height / 2f),
                new Vector2(beamLength / quad.Width, visualWidth / quad.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>兜底死光:着色器缺失时退回多层贴图拉伸</summary>
        private void DrawFallbackBeam(Vector2 drawPos, float rot, Color outer, Color mid, Color core, float flicker) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 lineOrigin = new(0, line.Height / 2f);
            float lenScale = beamLength / line.Width;

            Main.EntitySpriteDraw(line, drawPos, null, outer * 0.45f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 3.1f * flicker), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, Color.Lerp(outer, mid, 0.5f) * 0.85f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 1.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, core * 0.95f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 0.75f * flicker), SpriteEffects.None, 0);
        }
    }
}
