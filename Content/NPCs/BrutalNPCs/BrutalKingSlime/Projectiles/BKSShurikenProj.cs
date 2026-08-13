using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>影手里剑：直线飞行，尾段微坠，碰壁叮当销毁；服务端生成</summary>
    internal class BKSShurikenProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            //40帧后开始微坠
            if (Projectile.localAI[0] > 40f) {
                Projectile.velocity.Y += 0.12f;
            }
            Projectile.rotation += 0.42f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            if (Projectile.localAI[0] == 1f) {
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.35f, Volume = 0.6f, MaxInstances = 5 }, Projectile.Center);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.5f, Volume = 0.5f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Iron, 0, 0, 120, default, 0.9f);
                d.velocity = -oldVelocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.9) * Main.rand.NextFloat(1f, 3f);
            }
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            //借原版手里剑贴图，暗影色调
            Main.instance.LoadProjectile(ProjectileID.Shuriken);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.Shuriken].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            //旋转拖影
            for (int i = 1; i <= 3; i++) {
                Vector2 ghost = pos - Projectile.velocity * (i * 0.8f);
                Main.EntitySpriteDraw(tex, ghost, null, new Color(30, 34, 56, 0) * (0.35f - i * 0.09f),
                    Projectile.rotation - i * 0.4f, origin, 1f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, pos, null, new Color(58, 64, 96) * 0.95f,
                Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            //冷青缘光
            Main.EntitySpriteDraw(tex, pos, null, new Color(150, 200, 255, 0) * 0.3f,
                Projectile.rotation, origin, 1.06f, SpriteEffects.None, 0);
            return false;
        }
    }
}
