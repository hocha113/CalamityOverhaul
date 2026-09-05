using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 酷鞭处决「冰锁绽放」：聚霜、冰爆（1.5x 主段 + 霜焚）、
    /// 冰晶迸裂（ai[0] 传 0.5x 二段）
    /// </summary>
    internal class GsWhipFrostBloomProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        private const int GatherFrames = 5;
        private const int BloomWindow = 4;    //主爆窗
        private const int CrackAt = 11;       //二段起点
        private const int CrackWindow = 4;
        private const int LifeFrames = 28;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 180;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? CanDamage() {
            int elapsed = Elapsed;
            if (elapsed >= GatherFrames && elapsed < GatherFrames + BloomWindow) {
                return null;
            }
            return elapsed >= CrackAt && elapsed < CrackAt + CrackWindow ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //主爆 90px、迸裂收 60px
            float r = Elapsed < CrackAt ? 90f : 60f;
            return targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(r * 2f)));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn2, 240);

        public override void AI() {
            int elapsed = Elapsed;
            //二段起点：伤害切迸裂口径
            if (elapsed == CrackAt) {
                Projectile.damage = Math.Max(1, (int)Projectile.ai[0]);
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.9f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
            }
            if (elapsed == CrackAt) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = 0.25f }, Projectile.Center);
            }
        }

        /// <summary>范围提示：原版冰矢贴图按当前判定半径缩放画一笔（lightColor 着色），爆后随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            if (elapsed < GatherFrames) {
                return false;
            }
            float fade = 1f - MathHelper.Clamp((elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames), 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float r = elapsed < CrackAt ? 90f : 60f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, r * 2f / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
