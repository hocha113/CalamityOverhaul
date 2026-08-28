using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 漂浮孢囊（真菌鱼落水尾迹）。本体无害，水面漂浮 60 帧可见膨胀后自破，
    /// 沿 <see cref="SacBurstDirs"/> 放出 4 发迷你孢弹。
    /// 破裂方向恒为固定对角四向、生成后不读任何目标信息——非追踪保证，玩家可预判走位
    /// </summary>
    internal class MushroomSporeSacProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>漂浮帧数（自破倒计时，膨胀可见）</summary>
        internal const int SacFloatFrames = 60;
        /// <summary>迷你孢弹出膛速度</summary>
        private const float SacBoltSpeed = 5f;

        /// <summary>固定四向破裂方向（对角 X 形）：恒定不瞄准，非追踪保证</summary>
        private static readonly Vector2[] SacBurstDirs = [
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        ];

        private ref float Age => ref Projectile.localAI[0];
        private float Progress => MathHelper.Clamp(Age / SacFloatFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.hostile = false;//本体无害，威胁全在破裂产物
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SacFloatFrames;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Age++;
            if (Age == 1f && !Main.dedServ) {
                //落水帧的水花演出由孢囊自带（各端在实体首帧本地播放）
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GlowingMushroom,
                        new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(1f, 2.6f)),
                        110, default, Main.rand.NextFloat(0.8f, 1.2f));
                    dust.noGravity = true;
                }
            }

            //水面浮沉（龄期+identity 播种，各端确定性一致）
            Projectile.velocity = new Vector2(0f, MathF.Sin((Age + Projectile.identity * 11f) * 0.08f) * 0.28f);

            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.GlowingMushroom, new Vector2(0f, -0.5f), 140, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, MushroomSporeBoltProj.SporeBright.ToVector3() * (0.08f + 0.14f * Progress));

            //自破帧：权威端放射固定四向迷你孢弹
            if (Projectile.timeLeft == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                int boltType = ModContent.ProjectileType<MushroomSporeBoltProj>();
                foreach (Vector2 dir in SacBurstDirs) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                        dir * SacBoltSpeed, boltType, Projectile.damage, 0.5f, Main.myPlayer, 0f, 1f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Age / 8f, 0f, 1f);
            float swell = 0.55f + 0.6f * Progress;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * (8f + 14f * Progress) + Projectile.identity);
            //孢囊本体：双层孢珠，越临近自破鼓得越大、闪得越急（可见膨胀=倒计时）
            MushroomSporeBoltProj.DrawGlobAt(Projectile.Center - Main.screenPosition,
                MathF.Sin(Age * 0.05f + Projectile.identity) * 0.3f,
                fadeIn * pulse, new Vector2(0.34f, 0.4f) * swell);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GlowingMushroom,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 100, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = true;
            }
        }
    }
}
