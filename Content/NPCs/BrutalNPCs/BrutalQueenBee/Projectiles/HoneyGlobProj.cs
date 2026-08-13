using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles
{
    /// <summary>
    /// 蜂蜜团：抛物线飞行，黏稠抖动，落地铺出黏滞蜜洼<br/>
    /// ai[0]=蜜洼宽度(服务端掷骰)
    /// </summary>
    internal class HoneyGlobProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.24f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //黏稠垂滴
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_HoneyDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Color.Lerp(QueenBeeMotion.HoneyGold, QueenBeeMotion.AmberDeep, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.5f, 0.85f));
            }
            Lighting.AddLight(Projectile.Center, QueenBeeMotion.HoneyGold.ToVector3() * 0.3f);
        }

        public override void OnKill(int timeLeft) {
            //落点铺蜜洼(服务端)，场上蜜洼超量则只溅不铺
            if (!VaultUtils.isClient && CountPuddles() < 8) {
                float width = Projectile.ai[0] > 0f ? Projectile.ai[0] : 220f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    Vector2.Zero, ModContent.ProjectileType<HoneyPuddleZone>(), 0, 0f, Main.myPlayer, width);
            }
            QueenBeeMotion.HoneyBurst(Projectile.Center, 1.2f, 12);
        }

        private static int CountPuddles() {
            int count = 0;
            int type = ModContent.ProjectileType<HoneyPuddleZone>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == type) {
                    count++;
                }
            }
            return count;
        }

        public override bool PreDraw(ref Color lightColor) {
            //程序化蜜团：双层扁摆体+高光芯，飞行相位驱动黏稠抖动
            Texture2D tex = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float wobblePhase = Projectile.timeLeft * 0.32f;
            float squash = 1f + (float)Math.Sin(wobblePhase) * 0.18f;
            Vector2 bodyScale = new Vector2(0.9f / squash, 0.9f * squash) * Projectile.scale;
            Vector2 origin = tex.Size() * 0.5f;

            Color body = QueenBeeMotion.AmberDeep;
            Color shine = QueenBeeMotion.HoneyGold;

            //拖影
            Main.EntitySpriteDraw(tex, pos - Projectile.velocity * 0.7f, null, body * 0.3f,
                Projectile.rotation, origin, bodyScale * 0.86f, SpriteEffects.None, 0);
            //主体
            Main.EntitySpriteDraw(tex, pos, null, body * 0.92f,
                Projectile.rotation, origin, bodyScale, SpriteEffects.None, 0);
            //亮层
            Main.EntitySpriteDraw(tex, pos - new Vector2(2f, 3f), null, shine * 0.55f,
                Projectile.rotation, origin, bodyScale * 0.62f, SpriteEffects.None, 0);
            //高光点(窄条不走圆斑)
            Main.EntitySpriteDraw(tex, pos - new Vector2(3f, 5f), null, new Color(255, 240, 190, 0) * 0.5f,
                Projectile.rotation + 0.6f, origin, bodyScale * new Vector2(0.14f, 0.4f), SpriteEffects.None, 0);
            return false;
        }
    }
}
