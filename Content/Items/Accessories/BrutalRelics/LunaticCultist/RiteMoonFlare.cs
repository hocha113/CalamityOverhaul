using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.LunaticCultist
{
    /// <summary>
    /// 月焰激光：头顶凝出微型月眼（CultistPlanet TechMoon），竖瞳睁开后射出旋扫死光<br/>
    /// ai[0]=起始角 ai[1]=扫向(±1)；束角=起始角+扫描帧×恒定角速度，各端确定性推导<br/>
    /// 柱束收口：源头=月眼瞳孔+出瞳光斑；落点=地形射线截断+撞击光斑与晶尘；
    /// 打空末端=拉伸软光天然衰减包络；束宽有 展开→维持→塌缩 生命周期
    /// </summary>
    internal class RiteMoonFlare : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.localAI[0];
        private float StartAngle => Projectile.ai[0];
        private float SweepDir => Projectile.ai[1] >= 0f ? 1f : -1f;

        /// <summary>扫弧半幅(弧度)，集环派发端取起始角用</summary>
        public const float SweepHalfArc = 0.72f;

        private const int FormFrames = 34;
        private const int SweepFrames = 60;
        private const int FadeFrames = 14;
        /// <summary>恒定角速度=全弧/扫描帧</summary>
        private const float SweepRate = SweepHalfArc * 2f / SweepFrames;
        /// <summary>月眼可见半径(px)</summary>
        private const float VisRadius = 86f;
        private const float BeamMaxLength = 1500f;
        private const float BeamHalfWidth = 26f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FormFrames + SweepFrames + FadeFrames + 4;
            Projectile.netImportant = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
        }

        private bool Firing => Timer >= FormFrames && Timer < FormFrames + SweepFrames;

        /// <summary>当前束角：成形期锁死起始角，扫描期恒速旋转</summary>
        private float BeamAngle {
            get {
                float sweep = MathHelper.Clamp(Timer - FormFrames, 0f, SweepFrames);
                return StartAngle + sweep * SweepRate * SweepDir;
            }
        }

        /// <summary>月眼成形度 0~1（cubed 缓出）</summary>
        private float FormScale {
            get {
                float t = MathHelper.Clamp(Timer / FormFrames, 0f, 1f);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                float fade = Timer > FormFrames + SweepFrames
                    ? 1f - MathHelper.Clamp((Timer - FormFrames - SweepFrames) / FadeFrames, 0f, 1f) : 1f;
                return ease * fade;
            }
        }

        /// <summary>竖瞳开度：成形末段睁眼</summary>
        private float Pupil => MathHelper.Clamp((Timer - 20f) / (FormFrames - 20f), 0f, 1f);

        /// <summary>束宽包络：点火 6 帧展开→维持→收尾塌缩</summary>
        private float WidthEnvelope {
            get {
                if (Timer < FormFrames) {
                    return 0f;
                }
                float expand = MathHelper.Clamp((Timer - FormFrames) / 6f, 0f, 1f);
                float collapse = Timer > FormFrames + SweepFrames
                    ? 1f - MathHelper.Clamp((Timer - FormFrames - SweepFrames) / FadeFrames, 0f, 1f) : 1f;
                return expand * collapse;
            }
        }

        private Vector2 BeamStart => Projectile.Center + BeamAngle.ToRotationVector2() * (VisRadius * 0.55f);

        /// <summary>本帧束长缓存：AI 里射线扫描一次，判定与绘制共用</summary>
        private float cachedRayLen = BeamMaxLength;
        private readonly float[] raySamples = new float[3];

        /// <summary>地形射线截断后的束长</summary>
        private float RayLength() {
            Vector2 dir = BeamAngle.ToRotationVector2();
            Collision.LaserScan(BeamStart, dir, 16f, BeamMaxLength, raySamples);
            float total = 0f;
            for (int i = 0; i < raySamples.Length; i++) {
                total += raySamples[i];
            }
            return total / raySamples.Length;
        }

        public override void AI() {
            Timer++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //月眼软弹簧跟随头顶锚点
            Vector2 anchor = owner.Center + new Vector2(0f, -176f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.08f);
            Projectile.velocity = Vector2.Zero;

            //本帧束长：预告与开火期都要用
            if (Timer >= 16) {
                cachedRayLen = RayLength();
            }

            bool onScreen = CultistMotion.OnScreen(Projectile.Center, 400f);

            //仪式帷幕：成形期收拢，开火期维持（本地演出，屏外不扰邻）
            if (!VaultUtils.isServer && onScreen) {
                float veil = Timer < FormFrames ? 0.30f * (Timer / FormFrames) : 0.34f * FormScale;
                CultistScreenFX.SetVeil(veil, Projectile.Center, CultistMotion.MoonCore, 520f);
            }

            if ((int)Timer == 4 && !VaultUtils.isServer && onScreen) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.35f }, Projectile.Center);
            }
            //睁眼拍
            if ((int)Timer == 22 && !VaultUtils.isServer && onScreen) {
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.5f, Pitch = -0.6f }, Projectile.Center);
            }
            //点火拍
            if ((int)Timer == FormFrames && !VaultUtils.isServer && onScreen) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
                CultistScreenFX.PushFlash(0.18f);
                CultistMotion.Shake(Projectile.Center, 4.5f, 10);
            }

            if (Firing) {
                Vector2 dir = BeamAngle.ToRotationVector2();
                float len = cachedRayLen;
                Lighting.AddLight(BeamStart + dir * len * 0.4f, CultistMotion.MoonCore.ToVector3() * 0.9f);
                Lighting.AddLight(BeamStart + dir * len * 0.85f, CultistMotion.MoonCore.ToVector3() * 0.7f);

                //落点收口：撞地处晶尘迸溅（打空则末端自然衰减，无落点演出）
                if (!VaultUtils.isServer && len < BeamMaxLength - 8f && Main.rand.NextBool(2)) {
                    Vector2 end = BeamStart + dir * len;
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_CultistFrostMote>(end,
                        -dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 5f),
                        Color.Lerp(CultistMotion.MoonCore, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(14, 26));
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.MoonCore.ToVector3() * 0.6f * FormScale);
        }

        public override bool? CanDamage() => Firing;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Firing) {
                return false;
            }
            Vector2 dir = BeamAngle.ToRotationVector2();
            Vector2 start = BeamStart;
            Vector2 end = start + dir * cachedRayLen;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, BeamHalfWidth * 2f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CultistMotion.OnScreen(Projectile.Center, VisRadius * 2f + BeamMaxLength)) {
                return false;
            }
            DrawBeam();
            DrawMoonOrb();
            return false;
        }

        /// <summary>束体：预告细线→三层软光束+出瞳光斑+落点撞击光斑（全 A=0 加色）</summary>
        private void DrawBeam() {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Vector2 dir = BeamAngle.ToRotationVector2();
            float len = cachedRayLen;
            Vector2 startScreen = BeamStart - Main.screenPosition;
            float rot = BeamAngle;
            Color core = CultistMotion.MoonCore with { A = 0 };

            //成形后半段：细线预告，亮度缓升（预告即承诺，起始角已锁死）
            if (Timer >= 16 && Timer < FormFrames) {
                float warnT = (Timer - 16f) / (FormFrames - 16f);
                Main.spriteBatch.Draw(glow, startScreen, null, core * (0.16f + 0.24f * warnT), rot,
                    new Vector2(0f, glow.Height * 0.5f),
                    new Vector2(len / glow.Width, 6f / glow.Height), SpriteEffects.None, 0f);
                return;
            }

            float width = WidthEnvelope;
            if (width <= 0.02f) {
                return;
            }
            float flick = 0.92f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f);

            //宽晕+主体+白热芯
            Main.spriteBatch.Draw(glow, startScreen, null, core * (0.42f * width), rot,
                new Vector2(0f, glow.Height * 0.5f),
                new Vector2(len / glow.Width, BeamHalfWidth * 4.2f * width / glow.Height), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, startScreen, null, core * (0.85f * width * flick), rot,
                new Vector2(0f, glow.Height * 0.5f),
                new Vector2(len / glow.Width, BeamHalfWidth * 2.0f * width / glow.Height), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, startScreen, null, Color.White with { A = 0 } * (0.9f * width * flick), rot,
                new Vector2(0f, glow.Height * 0.5f),
                new Vector2(len / glow.Width, BeamHalfWidth * 0.8f * width / glow.Height), SpriteEffects.None, 0f);
            //源头收口：出瞳光斑
            Main.spriteBatch.Draw(glow, startScreen, null, core * (0.9f * width), 0f, glow.Size() * 0.5f,
                1.6f * width + 0.3f, SpriteEffects.None, 0f);
            //落点收口：撞地光斑（打空时不画）
            if (len < BeamMaxLength - 8f) {
                Vector2 endScreen = startScreen + dir * len;
                Main.spriteBatch.Draw(glow, endScreen, null, core * (0.75f * width * flick), 0f,
                    glow.Size() * 0.5f, 1.2f * width + 0.2f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>月眼本体：CultistPlanet TechMoon，uPupil 竖瞳开合（Immediate 批合同同 CultistPlanetProj）</summary>
        private void DrawMoonOrb() {
            Effect effect = EffectLoader.CultistPlanet?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            SpriteBatch sb = Main.spriteBatch;
            float formScale = FormScale;
            if (formScale <= 0.02f) {
                return;
            }
            if (effect == null || canvas == null || noise == null) {
                //着色器缺席回退：软光球月盘
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                        (CultistMotion.MoonCore with { A = 0 }) * (0.85f * formScale), 0f,
                        glow.Size() * 0.5f, VisRadius * 2f / glow.Width * formScale, SpriteEffects.None, 0f);
                }
                return;
            }

            //uniform 全参数重设（共享 shader 的设备全局残留陷阱）
            effect.CurrentTechnique = effect.Techniques["TechMoon"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.37f);
            effect.Parameters["uAlpha"]?.SetValue(formScale);
            effect.Parameters["uSpin"]?.SetValue(0.002f);
            effect.Parameters["uShear"]?.SetValue(0f);
            effect.Parameters["uTilt"]?.SetValue(0f);
            effect.Parameters["uLightDir"]?.SetValue(new Vector3(-0.45f, -0.55f, 0.70f));
            effect.Parameters["uColDeep"]?.SetValue(new Vector3(0.10f, 0.10f, 0.13f));
            effect.Parameters["uColMid"]?.SetValue(new Vector3(0.32f, 0.33f, 0.38f));
            effect.Parameters["uColBright"]?.SetValue(new Vector3(0.62f, 0.64f, 0.70f));
            effect.Parameters["uColStorm"]?.SetValue(new Vector3(0.55f, 1.0f, 0.85f));
            effect.Parameters["uSolidity"]?.SetValue(0.62f);
            effect.Parameters["uPupil"]?.SetValue(Pupil);

            //球盘=画布半径 0.42，quad 按可见半径折算（与 .fx 头部契约同步）
            float quadSize = VisRadius / 0.42f * 2f * formScale;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadSize / canvas.Width, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
