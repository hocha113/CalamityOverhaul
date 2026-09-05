using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant.Projectiles
{
    /// <summary>
    /// 气象痛「风暴合唱」：满层强化召来的驻场大风暴柱。<br/>
    /// 判定 = 本体矩形（宽 110 高 140，与可见尺寸同源），idStatic 免疫 24t 兑现 0.4s 一跳；
    /// 伤害在生成时按 0.5 倍烘焙。本体一笔按命中盒缩放原版气象痛龙卷贴图。
    /// ai[0] = 水平漂移方向（±1，生成时烘焙随包同步）
    /// </summary>
    internal class GsChantStormChoirProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WeatherPainShot;

        public override string LocalizationCategory => "GodSmithMagicChant";

        /// <summary>总寿命 1.8s</summary>
        private const int LifeTicks = 108;
        /// <summary>起身帧数</summary>
        private const int RiseTicks = 14;
        /// <summary>消散帧数</summary>
        private const int FadeTicks = 16;

        private float Envelope {
            get {
                float rise = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / (float)RiseTicks, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTicks, 0f, 1f);
                return Math.Min(rise * rise, fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 140;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 24;
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.WeatherPainShot];
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Envelope > 0.5f ? null : false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffTwisterLoop with {
                        Volume = 0.8f,
                        Pitch = -0.2f,
                        MaxInstances = 2
                    }, Projectile.Center);
                }
            }
            //缓慢横漂，读作风暴在行进而非钉死的立柱
            Projectile.position.X += Projectile.ai[0] * 0.45f * Envelope;
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //原版气象痛龙卷贴图一笔按命中盒拉伸（尺寸提示与判定同源，随包络起身/消散）
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
            Vector2 scale = new(Projectile.width / (float)frame.Width * env, Projectile.height / (float)frame.Height * env);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor,
                0f, frame.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
