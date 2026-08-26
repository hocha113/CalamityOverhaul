using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 钉头怪处刑重钉：ai[0]=锚索引×1000+登记类型。
    /// 瞄准短标预告 36 帧后沿锁向成小束射出（束内散布有限、方向锁死不追踪=逃生保证），
    /// 直线飞行，撞墙或超时消散。服务端几何采样命中与死亡回报锚怪，供挥空破绽裁决
    /// </summary>
    internal class EclNailheadNailProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NailFriendly;

        /// <summary>飞行寿命帧（覆盖最大触发距离 480 / 钉速 13.5 ≈ 36 帧，余量宽裕）</summary>
        private const int FlightFrames = 60;

        private static readonly Color NailDark = new Color(48, 26, 16);
        private static readonly Color NailHot = new Color(255, 170, 90, 0);

        private int AnchorIndex => (int)Projectile.ai[0] / 1000;
        private int RecordedType => (int)Projectile.ai[0] % 1000;
        private bool hitSampled;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
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
                    SoundEngine.PlaySound(SoundID.Item11 with { Volume = 0.5f, Pitch = -0.35f }, Projectile.Center);
                }
            }
            //钉尖朝向飞行方向（贴图竖向，转角补 π/2）
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

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
            Lighting.AddLight(Projectile.Center, 0.12f, 0.07f, 0.03f);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isClient) {
                EclipseNPC.NotifyPayloadGone(AnchorIndex, RecordedType);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.25f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Iron,
                    Main.rand.NextVector2Circular(2.2f, 2.2f), 80, default, Main.rand.NextFloat(0.7f, 1.1f));
                spark.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //同材质拖尾（横轴比≥0.5 契约：0.9× 本体）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, null, NailDark * (0.5f * t),
                    Projectile.oldRot[i], orig, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            //暗描边残像 ×2 → 本体 → 灼热钉尖
            for (int i = 0; i < 2; i++) {
                Vector2 off = (MathHelper.PiOver2 * i + MathHelper.PiOver4).ToRotationVector2() * 1.6f;
                Main.EntitySpriteDraw(tex, drawPos + off, null, NailDark * 0.7f, Projectile.rotation,
                    orig, Projectile.scale * 1.05f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, drawPos, null, Color.Lerp(lightColor, EclEclipseSets.SpikeTint, 0.35f),
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos + Projectile.velocity.SafeNormalize(Vector2.Zero) * 7f, null,
                NailHot * 0.55f, 0f, glow.Size() / 2f, 0.14f, SpriteEffects.None, 0);
            return false;
        }
    }
}
