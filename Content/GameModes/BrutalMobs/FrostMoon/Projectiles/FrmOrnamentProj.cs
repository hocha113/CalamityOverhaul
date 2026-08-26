using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 坠落饰品：饰品炮列的投放体，恒直落（无横向速度，列承诺不漂移），
    /// 触地弹跳一次后碎裂。仅在炮列预告完结后由权威端生成
    /// </summary>
    internal class FrmOrnamentProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.OrnamentHostile;

        private const float Gravity = 0.16f;
        private const float MaxFall = 11f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            //恒直落：横向速度强制归零，列承诺不漂移
            Projectile.velocity.X = 0f;
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFall) {
                Projectile.velocity.Y = MaxFall;
            }
            Projectile.rotation += 0.12f;
            Lighting.AddLight(Projectile.Center, 0.16f, 0.08f, 0.06f);
        }

        /// <summary>触地弹跳一次（仅竖向，保持列内），再触即碎</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] >= 1f) {
                return true;
            }
            Projectile.localAI[0] = 1f;
            Projectile.velocity.X = 0f;
            Projectile.velocity.Y = -Math.Abs(oldVelocity.Y) * 0.45f;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.28f, Pitch = 0.5f, MaxInstances = 6 }, Projectile.Center);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.36f, Pitch = 0.1f, MaxInstances = 6 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 1.5f) + new Vector2(0f, -1f), 90, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.OrnamentHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.OrnamentHostile].Value;
            int frames = Main.projFrames[ProjectileID.OrnamentHostile] > 0 ? Main.projFrames[ProjectileID.OrnamentHostile] : 1;
            Rectangle rect = tex.Frame(1, frames, 0, Projectile.identity % frames);
            Vector2 orig = rect.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //同材质拖尾（坠落残影）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, rect, new Color(255, 150, 120) * (0.3f * t),
                    Projectile.rotation - i * 0.12f, orig, 0.9f * t + 0.1f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, pos, rect, lightColor, Projectile.rotation, orig, 1f, SpriteEffects.None, 0);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float twinkle = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);
            Main.EntitySpriteDraw(glow, pos, null, new Color(255, 130, 110, 0) * (0.28f * twinkle), 0f,
                glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            return false;
        }
    }
}
