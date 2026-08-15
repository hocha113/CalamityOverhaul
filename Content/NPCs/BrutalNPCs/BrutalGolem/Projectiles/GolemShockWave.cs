using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>落地行进冲击波：贴地爬行的石浪，跳过可越过，撞高墙湮灭</summary>
    internal class GolemShockWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 58;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 86;
        }

        public override void AI() {
            //贴地：向下寻找地表，浪头吸附
            int tileX = (int)(Projectile.Center.X / 16f);
            int tileY = (int)(Projectile.Center.Y / 16f);

            //高墙检测：前方3格内垂直阻挡则湮灭
            int aheadX = (int)((Projectile.Center.X + Projectile.velocity.X * 3f) / 16f);
            int wallHeight = 0;
            for (int y = tileY; y >= tileY - 3; y--) {
                if (WorldGen.SolidTile(aheadX, y)) {
                    wallHeight++;
                }
            }
            if (wallHeight >= 3) {
                Projectile.Kill();
                return;
            }

            //地表吸附（向下扫8格，向上让2格）
            int surfaceY = -1;
            for (int y = tileY - 2; y < tileY + 8; y++) {
                if (WorldGen.SolidTile(tileX, y)) {
                    surfaceY = y;
                    break;
                }
            }
            if (surfaceY > 0) {
                float targetBottom = surfaceY * 16f;
                Projectile.Bottom = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Bottom.Y, targetBottom, 0.4f));
            }
            else {
                //悬空：缓降
                Projectile.velocity.Y = 4f;
            }
            if (surfaceY > 0) {
                Projectile.velocity.Y = 0f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.32f, 0.1f));

            //浪头表现：尘幕 + 崩石
            if (!Main.dedServ) {
                for (int i = 0; i < 2; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.Bottom - new Vector2(Projectile.width / 2f, 34f),
                        Projectile.width, 30, DustID.Smoke, 0f, -1.2f, 90, default, 1.5f);
                    dust.velocity *= 0.5f;
                    dust.velocity.X += Projectile.velocity.X * 0.2f;
                }
                if (Main.rand.NextBool(2)) {
                    Dust rock = Dust.NewDustDirect(Projectile.Bottom - new Vector2(Projectile.width / 2f, 12f),
                        Projectile.width, 8, DustID.Stone, Projectile.velocity.X * 0.3f, -2.6f, 40, default, 1.3f);
                    rock.noGravity = false;
                }
                if (Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-20f, 20f), -8f),
                        new Vector2(Projectile.velocity.X * 0.25f, Main.rand.NextFloat(-4f, -2f)),
                        new Color(122, 104, 78), Main.rand.NextFloat(0.6f, 1f)).Configure(30);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Smoke, 0f, -1f, 80, default, 1.4f);
                dust.velocity *= 0.6f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //热浪芯：低矮的辉光楔形，主体交给尘幕
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Bottom - new Vector2(0f, 18f) - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 150, 50, 0) * (0.55f * fade),
                0f, glow.Size() / 2f, new Vector2(1.5f, 0.6f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 220, 140, 0) * (0.4f * fade),
                0f, glow.Size() / 2f, new Vector2(0.8f, 0.35f), SpriteEffects.None, 0);
            return false;
        }
    }
}
