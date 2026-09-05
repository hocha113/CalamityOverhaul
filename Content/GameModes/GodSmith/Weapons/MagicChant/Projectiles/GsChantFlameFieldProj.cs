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
    /// 烈火之花「盛焰花田」：火球满层强化后原地化开的贴地火田。<br/>
    /// 判定 = 本体矩形（宽 180 高 50，与可见尺寸同源），idStatic 免疫 15t 兑现 0.25s 一跳；
    /// 伤害在生成时按 0.3 倍烘焙。阶段全部是 timeLeft 的确定函数，远端从快照自算
    /// </summary>
    internal class GsChantFlameFieldProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        public override string LocalizationCategory => "GodSmithMagicChant";

        /// <summary>总寿命 2.5s</summary>
        private const int LifeTicks = 150;
        /// <summary>起势帧数</summary>
        private const int RiseTicks = 10;
        /// <summary>收尾帧数</summary>
        private const int FadeTicks = 18;

        /// <summary>起势/收尾包络（timeLeft 确定函数）</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / (float)RiseTicks, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTicks, 0f, 1f);
                return Math.Min(rise, fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 15;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Envelope > 0.55f ? null : false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.15f }, Projectile.Center);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //原版火球贴图一笔按命中盒拉伸（尺寸提示与判定同源，随包络起势/收尾）
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 scale = new(Projectile.width / (float)tex.Width * env, Projectile.height / (float)tex.Height * env);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
