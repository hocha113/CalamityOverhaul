using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>喷沙沙团：重力弧线，原版沙球贴图为体（漫反射，乘光照），同材质残影拖尾</summary>
    internal class BssSandGlob : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.scale = 1.85f;
        }

        public override void AI() {
            Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + BssDirector.SandGlobGravity, -30f, 16f);
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    120, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.35f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), -Main.rand.NextFloat(0.5f, 2.4f)),
                    100, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //同材质残影拖尾（横轴=本体，比值 1.0）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color ghost = lightColor.MultiplyRGB(BssVfx.SandWarm) * (0.35f * t);
                Main.EntitySpriteDraw(tex, pos, null, ghost, Projectile.rotation - i * 0.1f,
                    origin, Projectile.scale * (0.85f + 0.15f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor.MultiplyRGB(BssVfx.SandWarm), Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
