using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    /// <summary>
    /// 一个通用的用于制造蓄力效果的实体栈，其中Projectile.ai[0]默认作为时间计数器
    /// 而Projectile.ai[1]用于存储需要跟随的弹幕的索引，Projectile.ai[2]为0表示是一个右键生成源，否则是一个左键生成源
    /// </summary>
    internal abstract class BaseOnSpanProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public Player Owner => Main.player[Projectile.owner];
        public virtual float MaxCharge => 90f;
        public virtual float ChargeProgress => (MaxCharge - Projectile.timeLeft) / MaxCharge;
        public virtual float Spread => MathHelper.PiOver2 * (1 - (float)Math.Pow(ChargeProgress, 1.5) * 0.95f);
        protected virtual float angle => Projectile.rotation;
        /// <summary>
        /// 一个4长度的颜色数组
        /// </summary>
        protected virtual Color[] colors => new Color[] { Color.White, Color.White, Color.White, Color.White };
        protected virtual float edgeBlendLength => 0.07f;
        protected virtual float edgeBlendStrength => 8f;
        protected virtual float halfSpreadAngleRate => 0.5f;
        protected bool onFire;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 1;
            Projectile.timeLeft = (int)MaxCharge;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 0.2f;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public sealed override bool? CanDamage() => false;

        /// <summary>
        /// 需要使用弹幕的AI[1]作为跟随弹幕的索引
        /// </summary>
        /// <param name="projectile"></param>
        public static void FlowerAI(Projectile projectile) {
            Projectile owner = null;
            if (projectile.ai[1] >= 0 && projectile.ai[1] < Main.maxProjectiles) {
                owner = Main.projectile[(int)projectile.ai[1]];
            }
            if (owner == null) {
                projectile.Kill();
                return;
            }
            projectile.Center = owner.Center;
            projectile.rotation = owner.rotation;
        }

        public sealed override void AI() {
            Projectile.MaxUpdates = 2;
            FlowerAI(Projectile);
            if (++Projectile.ai[0] >= MaxCharge) {
                onFire = true;
            }
            if (Projectile.ai[2] == 0) {
                if (!Main.player[Projectile.owner].PressKey(false) || Main.player[Projectile.owner].PressKey()) {
                    Projectile.Kill();
                }
            }
            else {
                if (!Main.player[Projectile.owner].PressKey() || Main.player[Projectile.owner].PressKey(false)) {
                    Projectile.Kill();
                }
            }
        }

        public virtual void SpanSoundFunc() => SoundEngine.PlaySound(SoundID.Item96, Projectile.Center);

        public virtual void SpanProjFunc() {

        }

        public sealed override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && onFire) {
                SpanSoundFunc();
                SpanProjFunc();
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            float blinkage = 0;
            if (Projectile.timeLeft >= MaxCharge * 1.5f) {
                blinkage = (float)Math.Sin(MathHelper.Clamp((Projectile.timeLeft - MaxCharge * 1.5f) / 15f, 0, 1) * MathHelper.PiOver2 + MathHelper.PiOver2);
            }
            Effect effect = Filters.Scene["CalamityMod:SpreadTelegraph"].GetShader().Shader;
            effect.Parameters["centerOpacity"].SetValue(ChargeProgress + 0.3f);
            effect.Parameters["mainOpacity"].SetValue((float)Math.Sqrt(ChargeProgress));
            effect.Parameters["halfSpreadAngle"].SetValue(Spread * halfSpreadAngleRate);
            effect.Parameters["edgeColor"].SetValue(Color.Lerp(colors[0], colors[1], blinkage).ToVector3());
            effect.Parameters["centerColor"].SetValue(Color.Lerp(colors[2], colors[3], blinkage).ToVector3());
            effect.Parameters["edgeBlendLength"].SetValue(0.17f);
            effect.Parameters["edgeBlendStrength"].SetValue(8f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            Main.EntitySpriteDraw(texture, Owner.MountedCenter - Main.screenPosition, null, Color.White, angle, new Vector2(texture.Width / 2f, texture.Height / 2f), 700f, 0, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
