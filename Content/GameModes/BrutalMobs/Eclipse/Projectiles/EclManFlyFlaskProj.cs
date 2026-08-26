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
    /// 蝇医蓄力毒瓶：ai[0]=落点X ai[1]=落点Y ai[2]=锚索引×1000+登记类型。
    /// 落点标记（EclStrikeOmen 落点模式）已预告 36 帧后才掷出，弹道向锁定落点解算（不重瞄）；
    /// 触地/抵达落点高度即爆裂为短存续毒雾圈，爆裂半径与标记宽度同源常量（标记区=伤害区）。
    /// 全程可见全程判定；服务端几何采样命中与死亡回报锚怪，供挥空破绽裁决
    /// </summary>
    internal class EclManFlyFlaskProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ToxicFlask;

        /// <summary>爆裂判定半径（EclStrikeOmen 落点标记按此常量画标记宽，两处同源）</summary>
        internal const float SplashRadius = 96f;
        /// <summary>爆裂窗帧数（判定窗=可见毒雾窗）</summary>
        internal const int SplashFrames = 18;
        /// <summary>最长飞行帧（弹道解算的 t 上限），超时兜底爆裂</summary>
        internal const int MaxFlightFrames = 56;
        /// <summary>自施重力：与投掷端弹道解算共用同一常数</summary>
        internal const float Gravity = 0.24f;
        private const float MaxFall = 15f;

        private Vector2 LockPoint => new Vector2(Projectile.ai[0], Projectile.ai[1]);
        private int AnchorIndex => (int)Projectile.ai[2] / 1000;
        private int RecordedType => (int)Projectile.ai[2] % 1000;
        /// <summary>爆裂相位（各端由同步的位置/速度确定性同判）</summary>
        private bool Splashing => Projectile.localAI[0] == 1f;
        /// <summary>服务端几何采样：本次载荷是否碰到过玩家（挥空裁决用，决策私产）</summary>
        private bool hitSampled;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxFlightFrames + SplashFrames + 30;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (!Splashing) {
                //飞行期：自施重力抛物，旋转翻滚
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFall);
                Projectile.rotation += 0.24f * (Projectile.velocity.X >= 0f ? 1f : -1f);

                //抵达落点高度且在下坠 / 飞行超时 → 爆裂（条件全部来自同步原语，各端同判）
                bool arrived = Projectile.velocity.Y > 0f && Projectile.Center.Y >= LockPoint.Y - 10f;
                bool overtime = Projectile.timeLeft <= SplashFrames + 4;
                if (arrived || overtime) {
                    BeginSplash();
                }
            }
            else {
                Projectile.velocity = Vector2.Zero;
            }

            //服务端几何命中采样（挥空裁决专用，与受害端实际判伤解耦）
            if (!VaultUtils.isClient && !hitSampled) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (!player.active || player.dead) {
                        continue;
                    }
                    bool touched = Splashing
                        ? player.Distance(Projectile.Center) <= SplashRadius + player.width * 0.5f
                        : Projectile.Hitbox.Intersects(player.Hitbox);
                    if (touched) {
                        hitSampled = true;
                        EclipseNPC.NotifyPayloadHit(AnchorIndex, RecordedType);
                        break;
                    }
                }
            }

            //毒雾期低频孢尘（预算：至多 2 粒/帧）
            if (Splashing && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust fog = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(SplashRadius * 0.9f, SplashRadius * 0.5f),
                    DustID.CursedTorch, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.8f)), 140, default,
                    Main.rand.NextFloat(1f, 1.6f));
                fog.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.1f, 0.2f, 0.05f);
        }

        private void BeginSplash() {
            if (Splashing) {
                return;
            }
            Projectile.localAI[0] = 1f;
            Projectile.timeLeft = SplashFrames;
            Projectile.tileCollide = false;
            Projectile.velocity = Vector2.Zero;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.7f, Pitch = 0.1f }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch,
                        Main.rand.NextVector2Circular(4f, 2.5f) - Vector2.UnitY * 1.5f, 100, default,
                        Main.rand.NextFloat(1.2f, 1.9f));
                    burst.noGravity = true;
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //砸墙即爆：不反弹不消失，原地转毒雾
            BeginSplash();
            return false;
        }

        /// <summary>爆裂期按半径圆判定（半径=标记宽的同源常量），飞行期用瓶体默认判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Splashing) {
                return null;
            }
            Vector2 nearest = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) <= SplashRadius * SplashRadius;
        }

        public override void OnKill(int timeLeft) {
            //死亡回报锚怪：载荷已了结，服务端据此裁决挥空破绽
            if (!VaultUtils.isClient) {
                EclipseNPC.NotifyPayloadGone(AnchorIndex, RecordedType);
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust rest = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(SplashRadius * 0.5f, 20f),
                    DustID.CursedTorch, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 1f), 150, default,
                    Main.rand.NextFloat(0.8f, 1.3f));
                rest.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color venom = EclEclipseSets.VenomTint with { A = 0 };

            if (!Splashing) {
                //飞行拖尾：同材质降比重画（横轴比≥0.5 契约）
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, oldDrawPos, null, lightColor * (0.38f * t),
                        Projectile.oldRot[i], orig, Projectile.scale * 0.8f, SpriteEffects.None, 0);
                }
                //暗底衬 + 瓶体 + 毒光晕
                Main.EntitySpriteDraw(rim, drawPos, null, new Color(16, 28, 10) * 0.6f, Projectile.rotation,
                    rim.Size() / 2f, 0.2f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, orig,
                    Projectile.scale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, drawPos, null, venom * 0.4f, 0f, glow.Size() / 2f,
                    0.32f, SpriteEffects.None, 0);
                return false;
            }

            //毒雾期：可见雾圈=判定圈（同一半径常量），随余时收拢
            float life = Projectile.timeLeft / (float)SplashFrames;
            float burst = 1f - MathF.Pow(life, 3f);
            float radiusScale = SplashRadius * 2f * (0.35f + 0.65f * burst) * (0.5f + 0.5f * life);
            Main.EntitySpriteDraw(rim, drawPos, null, new Color(14, 30, 8) * (0.7f * life), 0f,
                rim.Size() / 2f, new Vector2(radiusScale / rim.Width, radiusScale * 0.62f / rim.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, venom * (0.75f * life), 0f,
                glow.Size() / 2f, new Vector2(radiusScale / glow.Width, radiusScale * 0.6f / glow.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(220, 255, 160, 0) * (0.4f * life), 0f,
                glow.Size() / 2f, new Vector2(radiusScale * 0.45f / glow.Width, radiusScale * 0.3f / glow.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
