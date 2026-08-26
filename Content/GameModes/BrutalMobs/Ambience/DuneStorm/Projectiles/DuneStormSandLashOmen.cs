using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm.Projectiles
{
    /// <summary>
    /// 「沙鞭」预告体（无害）。ai[0]=锁定的出鞭方向 ai[1]=绑定档位。
    /// 沙暴中地面沙柱聚拢 50 帧（向心沙粒 + 鼓包沙丘 + 沿出鞭线的弹道虚影 + 两声递进的掘沙声），
    /// 提交帧由权威端沿锁定方向甩出沙鞭。方向在生成帧锁死（预告即承诺），
    /// 预告期 Boss 入场或残酷模式关闭则取消提交（虚影变暗淡出）
    /// </summary>
    internal class DuneStormSandLashOmen : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>预告帧数（公平契约 ≥45）</summary>
        private const int TelegraphFrames = 50;
        private const int FadeFrames = 12;
        /// <summary>出鞭初速（后续复合加速）</summary>
        internal const float LashLaunchSpeed = 7f;

        private float Aim => Projectile.ai[0];
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = false;//纯预告体，伤害经由沙鞭
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //预告期资格复查：Boss 入场或模式关闭则取消（各端读同步的世界旗标，结论一致）
            if (!Cancelled && elapsed < TelegraphFrames && !DuneStorm.MechanicsAllowed) {
                Cancelled = true;
            }
            if (Cancelled && elapsed >= TelegraphFrames) {
                Projectile.Kill();
                return;
            }

            if (!Main.dedServ) {
                if (elapsed == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
                }
                else if (elapsed == 32 && !Cancelled) {
                    //第二声上扬：听觉通道的临近提示
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.75f, Pitch = 0.25f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    //伤害随预告体携带（生成参数即完整状态，无生成后补写）
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center - Vector2.UnitY * 6f, Aim.ToRotationVector2() * LashLaunchSpeed,
                        ModContent.ProjectileType<DuneStormSandLashProj>(), Projectile.damage, 2f, Main.myPlayer);
                }
                if (!Main.dedServ) {
                    //鞭响落拍 + 破沙爆发
                    SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.8f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                            Aim.ToRotationVector2().RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 7f),
                            90, default, Main.rand.NextFloat(1.1f, 1.7f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }

            if (Cancelled || Main.dedServ || elapsed >= TelegraphFrames) {
                return;
            }

            //聚拢期：向心沙粒（≤2 粒/帧），沙自四面被拢进柱心
            float progress = elapsed / (float)TelegraphFrames;
            if (Main.rand.NextBool(2)) {
                Vector2 dir = Main.rand.NextVector2Unit();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(26f, 52f),
                    DustID.Sand, -dir * Main.rand.NextFloat(1.5f, 3f + 2f * progress),
                    110, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.30f, 0.24f, 0.10f) * progress);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float cancelDim = Cancelled ? 0.35f : 1f;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;

            if (elapsed >= TelegraphFrames) {
                //提交后的余闪
                float flash = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)FadeFrames, 0f, 1f);
                Texture2D burstGlow = CWRAsset.SoftGlow.Value;
                Color burst = new Color(255, 216, 140, 0) * (0.6f * flash * cancelDim);
                Main.EntitySpriteDraw(burstGlow, Projectile.Center - Main.screenPosition, null, burst, 0f,
                    burstGlow.Size() / 2f, 1.4f, SpriteEffects.None, 0);
                return false;
            }

            float progress = elapsed / (float)TelegraphFrames;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);

            //鼓包沙丘：三枚沙块随进度隆起（实体感锚点，镜像地涌预告语法）
            for (int i = 0; i < 3; i++) {
                float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 19f + Projectile.identity + i * 2.3f);
                Vector2 pos = Projectile.Center + new Vector2((i - 1) * 10f + jig * 2f, 4f - 7f * progress)
                    - Main.screenPosition;
                Color mound = Color.Lerp(lightColor, DuneStorm.SandBright, 0.5f) * (0.85f * progress * cancelDim);
                Main.EntitySpriteDraw(tex, pos, null, mound, jig * 0.4f, orig,
                    0.6f + 0.45f * progress, SpriteEffects.None, 0);
            }

            //出鞭线弹道虚影：沿锁定方向排四点渐显（画的就是甩的，读线即知走位）
            Vector2 aimDir = Aim.ToRotationVector2();
            for (int i = 1; i <= 4; i++) {
                float reach = (26f + 34f * i) * (0.4f + 0.6f * progress);
                Vector2 pos = Projectile.Center + aimDir * reach - Main.screenPosition;
                Color ghost = new Color(226, 196, 120, 150) * (0.45f * progress * pulse * cancelDim * (1f - i * 0.12f));
                Main.EntitySpriteDraw(tex, pos, null, ghost, Aim, orig, 0.55f - i * 0.06f, SpriteEffects.None, 0);
            }

            //出鞭线暖光道（加色敷料）
            Texture2D lane = CWRAsset.SoftGlow.Value;
            Vector2 lanePos = Projectile.Center + aimDir * 80f - Main.screenPosition;
            Color laneColor = new Color(DuneStorm.WarnGlow.R, DuneStorm.WarnGlow.G, DuneStorm.WarnGlow.B, 0)
                * (0.4f * progress * pulse * cancelDim);
            Main.EntitySpriteDraw(lane, lanePos, null, laneColor, Aim, lane.Size() / 2f,
                new Vector2(3.4f, 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }
}
