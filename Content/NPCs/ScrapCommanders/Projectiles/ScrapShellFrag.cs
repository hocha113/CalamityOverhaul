using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 迫击弹空爆碎片：弹头在弧顶炸开的下坠件，翻滚拖烬，
    /// 落地小砸响；ai[0]=1 的那片落地才堆出废钢堆（堆经济守恒）
    /// </summary>
    internal class ScrapShellFrag : ScrapModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool LeavesPile => Projectile.ai[0] == 1f;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.46f, 17f);
            Projectile.rotation += Projectile.velocity.X * 0.06f + 0.1f;
            if (!Main.dedServ && Projectile.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(
                    Projectile.Center, -Projectile.velocity * 0.1f,
                    new Color(255, 150, 58), Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(new Color(120, 46, 26), Main.rand.Next(16, 26));
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            ScrapVfx.GroundSlam(Projectile.Center, 0.55f);
            if (LeavesPile && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<ScrapJunkPile>(), 0, 0f, Main.myPlayer);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Cannonball);
            Texture2D tex = TextureAssets.Item[ItemID.Cannonball]?.Value;
            if (tex == null) {
                return false;
            }
            Color tint = lightColor.MultiplyRGB(new Color(214, 158, 118));
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, tint,
                Projectile.rotation, tex.Size() * 0.5f, 0.55f, SpriteEffects.None, 0);
            //碎片余温
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 150, 58, 0) * 0.3f, 0f, glow.Size() * 0.5f,
                    new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
