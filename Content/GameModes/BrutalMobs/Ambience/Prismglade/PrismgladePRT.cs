using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Prismglade
{
    /// <summary>
    /// 虹尘微粒：彩虹色相缓慢流转的悬浮光尘（常态氛围主力，粒级颗粒承载"虹尘"介质）。
    /// 尘线模式（<see cref="stretch"/> &gt; 1）沿运动方向拉伸，供「独角尘迹」远景飞掠复用
    /// </summary>
    internal class PRT_PrismgladeMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        /// <summary>当前色相，随寿命缓慢流转</summary>
        public float hue;
        /// <summary>拉伸倍率，&gt;1 进入尘线模式（方向由出生速度锁定）</summary>
        public float stretch;
        private float lockedRot;
        private bool rotLocked;
        private float sway;
        private float swaySpeed;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = Main.rand.Next(100, 160);
            hue = Main.rand.NextFloat();
            stretch = 1f;
            sway = Main.rand.NextFloat(MathHelper.TwoPi);
            swaySpeed = Main.rand.NextFloat(0.02f, 0.05f);
            Opacity = 0f;
        }

        public override void AI() {
            if (!rotLocked) {
                //尘线模式在首个活动帧锁定朝向，此后减速也不摆头
                lockedRot = Velocity.ToRotation();
                rotLocked = true;
            }
            float t = LifetimeCompletion;
            Opacity = Math.Min(t / 0.16f, 1f) * MathHelper.Clamp((1f - t) / 0.34f, 0f, 1f);

            sway += swaySpeed;
            Velocity *= 0.977f;
            Velocity += new Vector2(MathF.Sin(sway) * 0.012f, -0.006f);
            hue += 0.0014f;//色相缓慢流转
            if (stretch > 1f) {
                stretch = MathHelper.Lerp(stretch, 1f, 0.03f);
            }

            if (Opacity > 0.1f) {
                Lighting.AddLight(Position, PrismgladeFX.Prism(hue, 0.8f, 0.6f).ToVector3() * 0.14f * Opacity);
            }
        }

        public override void Reset() {
            base.Reset();
            hue = 0f;
            stretch = 1f;
            lockedRot = 0f;
            rotLocked = false;
            sway = 0f;
            swaySpeed = 0f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.004f) {
                return false;
            }
            Texture2D glow = PRTLoader.PRT_IDToTexture[ID];
            Vector2 orig = glow.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;
            //加色批：源因子是 SourceAlpha，染色保 A（禁 A=0）
            Color body = PrismgladeFX.Prism(hue, 0.85f, 0.62f) * Opacity;
            Color core = PrismgladeFX.Prism(hue + 0.04f, 0.5f, 0.85f) * (Opacity * 0.8f);

            if (stretch > 1.05f) {
                //尘线：沿锁定方向拉伸的光痕
                Vector2 scale = new(Scale * stretch, Scale * 0.5f);
                spriteBatch.Draw(glow, drawPos, null, body, lockedRot, orig, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, drawPos, null, core, lockedRot, orig, scale * 0.5f, SpriteEffects.None, 0f);
                return false;
            }
            spriteBatch.Draw(glow, drawPos, null, body, 0f, orig, Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, core, 0f, orig, Scale * 0.42f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 妖精光球：夜间多彩光点绕圈缓飞。每只自带椭圆轨道与漂移的轨道心，
    /// 翅频用缩放脉动表达，炽芯+四芒星闪防"裸光球"
    /// </summary>
    internal class PRT_PrismgladeFairy : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        /// <summary>轨道心（世界坐标），整群共享出生点后各自缓漂</summary>
        public Vector2 orbitCenter;
        /// <summary>轨道半径</summary>
        public float orbitR;
        /// <summary>当前轨道角</summary>
        public float angle;
        /// <summary>角速度（带方向）</summary>
        public float angSpeed;
        /// <summary>本体色相（妖精五色之一）</summary>
        public float hue;
        /// <summary>轨道心漂移速度</summary>
        public Vector2 centerDrift;
        private float bob;
        private float bobSpeed;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = Main.rand.Next(380, 520);
            bob = Main.rand.NextFloat(MathHelper.TwoPi);
            bobSpeed = Main.rand.NextFloat(0.05f, 0.09f);
            Opacity = 0f;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            angle += angSpeed;
            bob += bobSpeed;
            orbitCenter += centerDrift;
            //轨道心轻微游移，群舞不走直线
            centerDrift = centerDrift.RotatedBy(MathF.Sin(bob * 0.23f) * 0.012f);

            float r = orbitR * (1f + 0.09f * MathF.Sin(bob * 0.7f));
            //椭圆轨道：纵轴压扁读作环面群舞
            Position = orbitCenter + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r * 0.6f);

            float t = LifetimeCompletion;
            Opacity = Math.Min(t / 0.1f, 1f) * MathHelper.Clamp((1f - t) / 0.18f, 0f, 1f);
            if (Opacity > 0.1f) {
                Lighting.AddLight(Position, PrismgladeFX.Prism(hue, 0.75f, 0.6f).ToVector3() * 0.34f * Opacity);
            }
        }

        public override void Reset() {
            base.Reset();
            orbitCenter = Vector2.Zero;
            orbitR = 0f;
            angle = 0f;
            angSpeed = 0f;
            hue = 0f;
            centerDrift = Vector2.Zero;
            bob = 0f;
            bobSpeed = 0f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.004f) {
                return false;
            }
            Texture2D glow = PRTLoader.PRT_IDToTexture[ID];
            Texture2D star = StarTexture.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            //翅频：8Hz 级缩放脉动
            float flutter = 1f + 0.12f * MathF.Sin(bob * 4.3f);
            Color halo = PrismgladeFX.Prism(hue, 0.8f, 0.56f) * (Opacity * 0.85f);
            Color core = PrismgladeFX.Prism(hue, 0.35f, 0.88f) * Opacity;

            spriteBatch.Draw(glow, drawPos, null, halo, 0f, glow.Size() * 0.5f,
                Scale * flutter, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, core, 0f, glow.Size() * 0.5f,
                Scale * 0.36f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, drawPos, null, core * 0.8f, bob * 0.4f, star.Size() * 0.5f,
                Scale * 0.014f * flutter, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 圣晶折射闪光：白天水晶簇上的刺目星芒。前 1/4 寿命亮度过冲（刺目拍），
    /// 主芒两侧带色散重影（棱镜签名），随后快速衰减
    /// </summary>
    internal class PRT_PrismgladeFlash : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarFlare01";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        /// <summary>色散基准色相</summary>
        public float hue;
        private float rot;
        private float rotDrift;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = 34;
            rot = Main.rand.NextFloat(MathHelper.TwoPi);
            rotDrift = Main.rand.NextFloat(-0.006f, 0.006f);
        }

        public override void AI() {
            rot += rotDrift;
            float t = LifetimeCompletion;
            //过冲包络：前 25% 冲到 1.35，之后衰减归零
            Opacity = t < 0.25f
                ? t / 0.25f * 1.35f
                : 1.35f * MathHelper.Clamp((1f - t) / 0.75f, 0f, 1f);
            if (Opacity > 0.4f) {
                Lighting.AddLight(Position, new Vector3(0.9f, 0.88f, 1f) * (Opacity * 0.8f));
            }
        }

        public override void Reset() {
            base.Reset();
            hue = 0f;
            rot = 0f;
            rotDrift = 0f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.01f) {
                return false;
            }
            Texture2D flare = PRTLoader.PRT_IDToTexture[ID];
            Texture2D glow = SoftGlow.Value;
            Texture2D star = StarTexture.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            float t = LifetimeCompletion;
            float env = MathHelper.Clamp(Opacity, 0f, 1.35f);
            float size = Scale * (0.5f + 0.55f * Math.Min(t / 0.3f, 1f));

            //色散重影：主芒两侧红移/蓝移各一份（棱镜签名）
            Color shiftA = PrismgladeFX.Prism(hue - 0.07f, 0.9f, 0.6f) * (env * 0.34f);
            Color shiftB = PrismgladeFX.Prism(hue + 0.07f, 0.9f, 0.6f) * (env * 0.34f);
            Vector2 off = rot.ToRotationVector2() * 3f;
            spriteBatch.Draw(flare, drawPos - off, null, shiftA, rot, flare.Size() * 0.5f,
                size * 0.058f, SpriteEffects.None, 0f);
            spriteBatch.Draw(flare, drawPos + off, null, shiftB, rot, flare.Size() * 0.5f,
                size * 0.058f, SpriteEffects.None, 0f);

            //主芒：近白，微带色相
            Color white = PrismgladeFX.Prism(hue, 0.2f, 0.86f) * env;
            spriteBatch.Draw(flare, drawPos, null, white, rot, flare.Size() * 0.5f,
                size * 0.066f, SpriteEffects.None, 0f);
            //交叉细芒 + 中心热点
            spriteBatch.Draw(star, drawPos, null, white * 0.75f, rot + MathHelper.PiOver4,
                star.Size() * 0.5f, size * 0.02f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, white * 0.9f, 0f, glow.Size() * 0.5f,
                size * 0.24f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
