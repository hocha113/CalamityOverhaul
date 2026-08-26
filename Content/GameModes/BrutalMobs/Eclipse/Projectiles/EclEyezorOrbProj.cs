using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 独眼佐尔蓄力眼弹：ai[0]=锚索引×1000+登记类型。
    /// 瞄准短标（EclStrikeOmen 短标模式）预告 36 帧后沿锁向射出，直线飞行不追踪（预告即承诺）；
    /// 全程可见全程判定，撞墙或超时消散。服务端几何采样命中与死亡回报锚怪，供挥空破绽裁决
    /// </summary>
    internal class EclEyezorOrbProj : ModProjectile
    {
        //ChaosBall 在 ProjectileID 里不存在（原版把它做成了 NPC），用小鬼火球贴图：圆形单帧、翻滚旋转匹配
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        /// <summary>飞行寿命帧（覆盖最大触发距离 560 / 最低档速 10.5 ≈ 54 帧，余量宽裕）</summary>
        private const int FlightFrames = 90;

        private static readonly Color OrbDark = new Color(64, 10, 26);

        private int AnchorIndex => (int)Projectile.ai[0] / 1000;
        private int RecordedType => (int)Projectile.ai[0] % 1000;
        private bool hitSampled;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FlightFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.62f, Pitch = -0.42f }, Projectile.Center);
                }
            }
            Projectile.rotation += 0.18f;

            //服务端几何命中采样（挥空裁决专用）
            if (!VaultUtils.isClient && !hitSampled) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (player.active && !player.dead && Projectile.Hitbox.Intersects(player.Hitbox)) {
                        hitSampled = true;
                        EclipseNPC.NotifyPayloadHit(AnchorIndex, RecordedType);
                        break;
                    }
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Dust trail = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.PinkTorch, -Projectile.velocity * 0.15f, 120, default, Main.rand.NextFloat(0.8f, 1.2f));
                trail.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.22f, 0.08f, 0.12f);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isClient) {
                EclipseNPC.NotifyPayloadGone(AnchorIndex, RecordedType);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit3 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust pop = Dust.NewDustPerfect(Projectile.Center, DustID.PinkTorch,
                    Main.rand.NextVector2Circular(3.2f, 3.2f), 100, default, Main.rand.NextFloat(0.9f, 1.5f));
                pop.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color ocular = EclEclipseSets.OcularTint with { A = 0 };
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);

            //同材质拖尾（横轴比≥0.5 契约：0.85× 本体）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, null, EclEclipseSets.OcularTint * (0.35f * t),
                    Projectile.oldRot[i], orig, Projectile.scale * 0.85f * (0.6f + 0.4f * t), SpriteEffects.None, 0);
            }

            //暗色实底外圈（压亮背景）→ 本体 → 加色芯
            Main.EntitySpriteDraw(rim, drawPos, null, OrbDark * 0.85f, 0f,
                rim.Size() / 2f, 0.3f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, Color.Lerp(lightColor, EclEclipseSets.OcularTint, 0.55f),
                Projectile.rotation, orig, Projectile.scale * 1.15f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, ocular * (0.6f * pulse), 0f,
                glow.Size() / 2f, 0.42f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 236, 236, 0) * (0.4f * pulse), 0f,
                glow.Size() / 2f, 0.2f, SpriteEffects.None, 0);
            return false;
        }
    }
}
