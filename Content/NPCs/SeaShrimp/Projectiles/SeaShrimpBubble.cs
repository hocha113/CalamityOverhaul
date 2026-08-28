using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 泡幕气泡：慢速上升的可读威胁，横向水流摆（identity 确定性哈希，不掷随机）。
    /// 触顶或寿尽即破。ai[0]=半径，ai[1]=上升速度
    /// </summary>
    internal class SeaShrimpBubble : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle")]
        private static Asset<Texture2D> RingTex = null;

        private float Radius => Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            //寿命压短：泡幕不得叠进下一招的弹幕图案（图案叠压的公平口径）
            Projectile.timeLeft = 260;
        }

        public override void AI() {
            //横向水流摆：identity 定相位，各端一致
            float phase = Projectile.identity * 0.917f;
            Projectile.velocity.X = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + phase) * 0.55f;
            Projectile.velocity.Y = -Projectile.ai[1];
            Lighting.AddLight(Projectile.Center, 0.05f, 0.12f, 0.24f);

            //触顶破裂
            if (ShrimpTerrain.SolidAt(Projectile.Center - new Vector2(0f, Radius + 6f))) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_SHPCCoralBubble>(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, Radius * 0.5f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.4f)),
                    Color.White * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= Radius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = RingTex?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (ring == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float wobble = 1f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
            float scale = Radius * 2f / ring.Width * wobble;
            Main.spriteBatch.Draw(ring, pos, null, new Color(150, 200, 255) * 0.8f, 0f,
                ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(ring, pos, null, new Color(40, 70, 130) * 0.3f, 0f,
                ring.Size() * 0.5f, scale * 0.84f, SpriteEffects.None, 0f);
            if (glow != null) {
                Main.spriteBatch.Draw(glow, pos + new Vector2(-Radius * 0.32f, -Radius * 0.36f), null,
                    new Color(255, 255, 255, 0) * 0.45f, 0f, glow.Size() * 0.5f,
                    Radius * 0.42f / glow.Width * 2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
