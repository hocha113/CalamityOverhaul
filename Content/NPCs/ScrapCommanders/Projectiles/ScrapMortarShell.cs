using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 废钢迫击弹：翻滚的铸铁弹头拖着余烬烟迹走迫击弧线，
    /// 落地炸开尘火并砸出一座废钢堆（P2 磁暴的场上物资）
    /// </summary>
    internal class ScrapMortarShell : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + ScrapDirector.MortarGravity, 18f);
            //铸铁弹头的翻滚
            Projectile.rotation += Projectile.velocity.X * 0.045f + 0.06f;

            //弧顶空爆：过顶即炸成三片下坠碎片（各端同拍，生成只在权威端）
            if (Projectile.velocity.Y > 0.5f && Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.Kill();
                return;
            }

            if (Main.dedServ) {
                return;
            }
            //曳光烟带：短曳光帧连成弧线 + 滚烟 + 偶发热烬，坠落段更密
            bool falling = Projectile.velocity.Y > 0f;
            if (Projectile.timeLeft % 2 == 0) {
                Vector2 back = Projectile.Center - Projectile.velocity * 2.4f;
                PRTLoader.NewParticle<PRT_PallbearerTracer>(Projectile.Center, Vector2.Zero,
                    new Color(255, 150, 58) * (falling ? 0.75f : 0.5f), 1f)
                    ?.Configure(back, Projectile.Center, falling ? 5f : 3.5f, 9);
            }
            if (Projectile.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(
                    Projectile.Center - Projectile.velocity * 0.6f,
                    -Projectile.velocity * 0.05f,
                    new Color(52, 48, 44), Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(30, 46), 0.5f, Main.rand.NextFloat(-0.02f, 0.02f));
            }
            if (falling && Projectile.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    -Projectile.velocity * 0.1f,
                    new Color(255, 150, 58), Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(new Color(120, 46, 26), Main.rand.Next(20, 32));
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            Vector2 hit = Projectile.Center;
            bool airburst = Projectile.localAI[0] == 1f;
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f, Pitch = airburst ? -0.1f : -0.3f, MaxInstances = 3 }, hit);
            if (!Main.dedServ) {
                //机械爆炸：分配表配方；空爆不带尘土
                ScrapVfx.MetalExplosion(hit, airburst ? 0.8f : 0.9f);
                if (!airburst) {
                    for (int i = 0; i < 10; i++) {
                        Dust dust = Dust.NewDustPerfect(hit + Main.rand.NextVector2Circular(14f, 10f),
                            DustID.Dirt, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1.5f, 5f)),
                            60, default, Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (airburst) {
                //三片下坠碎片扇形铺开，随机一片落地成堆（堆经济与旧版守恒）
                int pileIndex = Main.rand.Next(3);
                int damage = (int)(Projectile.damage * 0.72f);
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = new(Projectile.velocity.X * 0.5f + (i - 1) * 2.8f, 2f + i * 0.6f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), hit, vel,
                        ModContent.ProjectileType<ScrapShellFrag>(), damage, 3f,
                        Main.myPlayer, i == pileIndex ? 1f : 0f);
                }
            }
            else {
                //提前被截击/砸地：原地一座废钢堆
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    hit, Vector2.Zero,
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
                Projectile.rotation, tex.Size() * 0.5f, 1f, SpriteEffects.None, 0);
            //弹头余温微光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 150, 58, 0) * 0.25f, 0f, glow.Size() * 0.5f,
                    new Vector2(20f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
