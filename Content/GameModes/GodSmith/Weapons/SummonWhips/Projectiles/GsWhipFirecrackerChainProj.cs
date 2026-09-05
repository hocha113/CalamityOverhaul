using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 火鞭连环爆单元：ai[0] 传起爆延迟帧，到点炸出 90px 火团（1.0x 鞭面板）。
    /// 三枚错位错时组成连环，逐爆推进爆竹节奏
    /// </summary>
    internal class GsWhipFirecrackerChainProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfernoFriendlyBlast;

        /// <summary>引信期的落点标记尺寸（px）</summary>
        private const float FuseMarkPx = 24f;

        private const int BoomWindow = 4;
        private const int LifeFrames = 52;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        private int Delay => (int)Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 180;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= Delay && Elapsed < Delay + BoomWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(180f)));

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 180);

        public override void AI() {
            //起爆帧：全端主音
            if (Elapsed == Delay && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = 0.15f }, Projectile.Center);
            }
        }

        /// <summary>范围提示：引信期画一枚小落点标记，起爆后原版贴图按判定框缩放画一笔（lightColor 着色）并随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale;
            float fade = 1f;
            if (elapsed < Delay) {
                scale = FuseMarkPx / tex.Width;
            }
            else {
                scale = Projectile.width / (float)tex.Width;
                fade = 1f - MathHelper.Clamp((elapsed - Delay) / (float)(LifeFrames - Delay), 0f, 1f);
                if (fade <= 0.01f) {
                    return false;
                }
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
