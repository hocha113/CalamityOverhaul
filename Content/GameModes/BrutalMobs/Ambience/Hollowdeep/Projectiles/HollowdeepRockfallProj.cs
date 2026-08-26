using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Hollowdeep.Projectiles
{
    /// <summary>
    /// 「惊岩」落石。ai[0]=体型。
    /// 生成位置即锁定落点铅垂线（预告即承诺）：洞顶碎石尘簌簌下落 + 咔啦裂响 52 帧
    /// → 岩块脱顶坠下（仅坠落窗口有判定，微量伤害，可走位躲开）→ 触地碎裂余韵。
    /// 全程无后置字段写入，各端相位由 timeLeft 推导（镜像 WastesSandGeyserProj 的同步口径）
    /// </summary>
    internal class HollowdeepRockfallProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Boulder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 52;
        /// <summary>坠落窗口上限（落进深坑也到点自灭）</summary>
        private const int FallMaxFrames = 260;
        /// <summary>坠落重力与终速</summary>
        private const float Gravity = 0.34f;
        private const float MaxFallSpeed = 10.5f;

        private float RockScale => Projectile.ai[0];
        private int TotalLife => TelegraphFrames + FallMaxFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private bool Falling => Elapsed >= TelegraphFrames;
        private float SpinDir => Projectile.identity % 2 == 0 ? 1f : -1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//坠落窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;//坠落窗口内才置真
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FallMaxFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => Falling;

        public override void AI() {
            int elapsed = Elapsed;
            bool falling = elapsed >= TelegraphFrames;

            //判定窗=坠落窗；两个旗标都是各端由相位确定性推导，不走同步
            Projectile.hostile = falling;
            Projectile.tileCollide = falling;

            if (!falling) {
                //预告期：双通道预告（咔啦裂响 + 顶缝簌簌落尘）
                if (!Main.dedServ) {
                    if (elapsed == 0) {
                        SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 5 }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 5 }, Projectile.Center);
                    }
                    else if (elapsed == 26) {
                        SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.65f, Pitch = -0.15f, MaxInstances = 5 }, Projectile.Center);
                    }
                    if (elapsed % 2 == 0) {
                        float progress = elapsed / (float)TelegraphFrames;
                        Dust dust = Dust.NewDustPerfect(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f) * RockScale, -6f),
                            DustID.Stone, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(1.2f, 2.6f)),
                            80, default, 0.8f + progress * 0.5f);
                        dust.noGravity = false;
                    }
                    Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.12f, 0.05f));
                }
                return;
            }

            if (elapsed == TelegraphFrames && !Main.dedServ) {
                //脱顶帧：崩落声 + 一撮迸石
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Stone,
                        new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-0.5f, 2f)),
                        70, default, Main.rand.NextFloat(0.9f, 1.4f));
                    dust.noGravity = Main.rand.NextBool();
                }
            }

            //坠落：重力加速 + 随速自旋（确定性，各端一致）
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFallSpeed);
            Projectile.rotation += SpinDir * (0.03f + 0.05f * Projectile.velocity.Y / MaxFallSpeed);

            if (!Main.dedServ && Elapsed % 3 == 0) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), -8f),
                    DustID.Stone, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)), 100, default, 0.7f);
                trail.noGravity = true;
            }
        }

        //命中玩家同样当场碎裂（不穿身）
        public override void OnHitPlayer(Player target, Player.HurtInfo info) => Projectile.Kill();

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //碎裂拍
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.85f, Pitch = -0.3f, MaxInstances = 5 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.55f, MaxInstances = 5 }, Projectile.Center);
            //迸裂石屑
            for (int i = 0; i < 12; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Stone,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f) - new Vector2(0f, 1.5f),
                    60, default, Main.rand.NextFloat(0.9f, 1.5f));
                dust.noGravity = false;
            }
            //碎石余韵：几粒弹跳小砾 + 尘团缓浮，活得比岩块久
            for (int i = 0; i < 4; i++) {
                Dust pebble = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 4f),
                    DustID.Stone, new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(1.5f, 3.2f)),
                    30, default, Main.rand.NextFloat(0.6f, 0.9f));
                pebble.noGravity = false;
            }
            for (int i = 0; i < 3; i++) {
                Dust haze = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f),
                    DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.2f, 0.7f)),
                    140, new Color(150, 142, 132), Main.rand.NextFloat(0.8f, 1.2f));
                haze.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            int elapsed = Elapsed;

            if (!Falling) {
                //预告期：顶缝警示光斑 + 挤出的岩楔颤动（实体锚点，越临近越低垂）
                float progress = elapsed / (float)TelegraphFrames;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color warn = new Color(255, 190, 110, 0) * (0.5f * progress * pulse);
                Main.EntitySpriteDraw(glow, Projectile.Center - new Vector2(0f, 6f) - Main.screenPosition,
                    null, warn, 0f, glow.Size() / 2f, new Vector2(1.3f * RockScale, 0.45f), SpriteEffects.None, 0);

                for (int i = 0; i < 3; i++) {
                    float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 23f + Projectile.identity + i * 2.3f);
                    Vector2 pos = Projectile.Center
                        + new Vector2((i - 1) * 9f * RockScale + jig * 1.6f, -9f + 5f * progress)
                        - Main.screenPosition;
                    Color chip = Color.Lerp(lightColor, new Color(176, 168, 158), 0.4f) * (0.4f + 0.6f * progress);
                    Main.EntitySpriteDraw(tex, pos, null, chip, jig * 0.5f + i * 1.7f, orig,
                        (0.24f + 0.1f * progress) * RockScale, SpriteEffects.None, 0);
                }
                return false;
            }

            //坠落期：速度拉伸残影 + 本体（洞穴暗处也保底可读）
            Color body = Color.Lerp(lightColor, new Color(176, 168, 158), 0.35f);
            float scale = 0.8f * RockScale;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 smear = Projectile.velocity;
            Main.EntitySpriteDraw(tex, drawPos - smear * 1.1f, null, body * 0.16f,
                Projectile.rotation - SpinDir * 0.16f, orig, scale * 0.94f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos - smear * 0.55f, null, body * 0.32f,
                Projectile.rotation - SpinDir * 0.08f, orig, scale * 0.97f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, body,
                Projectile.rotation, orig, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
