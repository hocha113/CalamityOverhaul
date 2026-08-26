using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 雨云魔棒「雨幕走廊」：owner 的两朵雨云之间架起 60px 宽的雨幕连线带。<br/>
    /// 端点各端独立从在场云弹幕（原版同步实体）读取，几何按 X 排序保证稳定；
    /// 带内敌人受 tick 伤害并被温和减速（NPC 位移只在服务端权威写入）；
    /// 雷雨形态下由 owner 端周期在带内落雷（GsBurstProj 柱形，全端可见）。
    /// 任一云消失即由 owner 端收束
    /// </summary>
    internal class GsNimbusCorridorProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private Vector2 endA;
        private Vector2 endB;
        private bool anchored;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.timeLeft = 3600;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>取 owner 在场的雨云端点（云下沿），成功=找到两朵</summary>
        private bool ResolveClouds() {
            Vector2 a = Vector2.Zero, b = Vector2.Zero;
            int found = 0;
            for (int i = 0; i < Main.maxProjectiles && found < 2; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner
                    && (p.type == ProjectileID.RainCloudMoving || p.type == ProjectileID.RainCloudRaining)) {
                    if (found == 0) {
                        a = p.Center + new Vector2(0f, 22f);
                    }
                    else {
                        b = p.Center + new Vector2(0f, 22f);
                    }
                    found++;
                }
            }
            if (found < 2) {
                return false;
            }
            //按 X 排序，端点身份与弹幕槽位无关，各端一致
            if (a.X > b.X) {
                (a, b) = (b, a);
            }
            endA = a;
            endB = b;
            anchored = true;
            Projectile.Center = (a + b) * 0.5f;
            return true;
        }

        public override void AI() {
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            if (!ResolveClouds()) {
                //云不足两朵：owner 端收束（Kill 广播，远端等包期间沿用缓存端点绘制）
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                }
                return;
            }
            //带内温和减速：NPC 位移是服务端权威量
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                float _ = 0f;
                foreach (NPC npc in Main.npc) {
                    if (npc.active && !npc.boss && !npc.dontTakeDamage && npc.knockBackResist > 0f
                        && Collision.CheckAABBvLineCollision(npc.position, npc.Size, endA, endB, 60f, ref _)) {
                        npc.velocity.X *= 0.96f;
                    }
                }
            }
            //雷雨形态落雷：owner 端读本地形态偏好，产物为真弹幕
            if (Projectile.IsOwnedByLocalPlayer()
                && Main.player[Projectile.owner].GetModPlayer<GsMorphPlayer>().NimbusStorm
                && Projectile.timeLeft % 72 == 0) {
                Vector2 strike = Vector2.Lerp(endA, endB, Main.rand.NextFloat(0.15f, 0.85f));
                GsBurstProj.Spawn(Projectile, strike, (int)(Projectile.damage * 4.3f), 40f, 2);
                if (!VaultUtils.isServer) {
                    Vector2 cloud = strike.X - endA.X < endB.X - strike.X ? endA : endB;
                    PRTLoader.NewParticle<PRT_SkyBolt>(strike, Vector2.Zero, new Color(150, 200, 255), 1f)
                        ?.Configure(cloud - new Vector2(0f, 16f), strike + new Vector2(0f, 60f));
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.35f, Pitch = 0.3f }, strike);
                }
            }
            //雨丝（各端客户端，≤2/帧）
            if (!VaultUtils.isServer && Projectile.timeLeft % 2 == 0) {
                Vector2 pos = Vector2.Lerp(endA, endB, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos + new Vector2(0f, Main.rand.NextFloat(-6f, 20f)),
                    new Vector2(0f, Main.rand.NextFloat(3f, 5f)), new Color(140, 165, 200), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(14, 22), 0f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!anchored) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), endA, endB, 60f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!anchored) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            //雨幕带：蓝灰半透光带沿两云连线（A=0 加色安全），上缘略亮示意云檐
            Vector2 mid = (endA + endB) * 0.5f;
            float len = endA.Distance(endB);
            float rot = (endB - endA).ToRotation();
            Color band = new Color(96, 128, 178) * 0.3f;
            band.A = 0;
            Main.EntitySpriteDraw(glow, mid - Main.screenPosition, null, band, rot,
                glow.Size() / 2f, new Vector2(len / glow.Width * 1.05f, 1f),
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Color rim = new Color(170, 200, 240) * 0.24f;
            rim.A = 0;
            Main.EntitySpriteDraw(glow, mid - Main.screenPosition - new Vector2(0f, 18f), null, rim, rot,
                glow.Size() / 2f, new Vector2(len / glow.Width, 0.3f),
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }
    }
}
