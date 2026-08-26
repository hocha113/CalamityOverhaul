using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart.Projectiles
{
    /// <summary>
    /// 「蜂域警戒」蜂云：蜂巢内久待激起的一团蜂群，不生成真蜜蜂 NPC。ai[1]=触发玩家。
    /// 聚拢 64 帧（蜂影自巢壁骚动汇聚 + 嗡鸣渐急）→ 缓速掠过玩家路径 150 帧（仅此窗口有判定，
    /// 触碰微量伤害）→ 散去 34 帧。方向与速度在生成帧锁死（预告即承诺，随生成包同步，此后不改）。
    /// 触发玩家离开蜂巢立即平息：各端从同步的 Zone 旗标得出同一结论，就地消散。
    /// Boss 在场判定即停
    /// </summary>
    internal class MireheartBeeCloudProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>聚拢帧数（公平契约 ≥45：判定开启前的双通道预告窗）</summary>
        private const int GatherFrames = 64;
        private const int SweepFrames = 150;
        private const int DisperseFrames = 34;
        private const int TotalFrames = GatherFrames + SweepFrames + DisperseFrames;
        /// <summary>聚拢起始的松散半径 → 收紧后的行进半径</summary>
        private const float LooseRadius = 100f;
        private const float SwarmRadius = 66f;
        /// <summary>判定半径 = 行进半径 × 此系数（判定略窄，偏袒玩家）</summary>
        private const float CollideRadiusFrac = 0.8f;
        /// <summary>伤害 = 原版蜜蜂接触伤害 × 此值（镜像 DamageFrac 写法）</summary>
        private const float DamageFrac = 0.5f;
        /// <summary>敌对弹幕对玩家结算自带 ×2（专家 ×4），此处回折一半取回接触口径</summary>
        private const float HostileProjHalf = 0.5f;
        /// <summary>提前平息的消散帧数</summary>
        private const int DissolveFrames = 22;
        /// <summary>蜂点绘制数量</summary>
        private const int SpeckCount = 16;

        private int TriggerIndex => (int)Projectile.ai[1];
        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private float GatherProgress => MathHelper.Clamp(Elapsed / (float)GatherFrames, 0f, 1f);
        private bool InSweep => Elapsed >= GatherFrames && Elapsed < GatherFrames + SweepFrames;
        private float DisperseProgress => MathHelper.Clamp(
            (DisperseFrames - Projectile.timeLeft) / (float)DisperseFrames, 0f, 1f);

        /// <summary>提前平息进度 0~1（localAI 计数一旦启动就闩死推进）</summary>
        private float DissolveProgress => MathHelper.Clamp(Projectile.localAI[0] / DissolveFrames, 0f, 1f);

        /// <summary>伤害基准：原版蜜蜂接触伤害折算，微量口径</summary>
        internal static int CloudDamage() {
            int baseContact = ContentSamples.NpcsByNetId.TryGetValue(NPCID.Bee, out NPC bee)
                ? bee.damage : 13;
            return Math.Max(3, (int)(baseContact * DamageFrac * HostileProjHalf));
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = false;//仅掠过窗口置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //触发玩家离开蜂巢/离场/死亡 → 立即平息（Zone 旗标各端同步，结论一致）
            bool anchored = TriggerIndex >= 0 && TriggerIndex < Main.maxPlayers
                && Main.player[TriggerIndex].active && !Main.player[TriggerIndex].dead
                && Main.player[TriggerIndex].ZoneHive;
            if (!anchored || Projectile.localAI[0] > 0f) {
                Projectile.localAI[0]++;
                //真正的移除只由权威端裁定（客户端只做视觉淡出，等服务端的 Kill 同步）
                if (Projectile.localAI[0] >= DissolveFrames
                    && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.Kill();
                    return;
                }
            }

            bool dissolving = Projectile.localAI[0] > 0f;
            Projectile.hostile = InSweep && !dissolving && !CWRWorld.HasBoss;

            //只在掠过窗口沿锁定方向缓速推进（velocity 只读不改，生成包即全部真相）
            if (InSweep && !dissolving) {
                Projectile.position += Projectile.velocity;
            }

            if (Main.dedServ) {
                return;
            }

            float gather = GatherProgress;
            //嗡鸣：聚拢期音调渐升（听觉预告），掠过期恒定低鸣
            if (!dissolving) {
                if (Elapsed < GatherFrames && Elapsed % 10 == 0) {
                    SoundEngine.PlaySound(SoundID.Item97 with {
                        Volume = 0.2f + 0.1f * gather,
                        Pitch = -0.4f + 0.85f * gather,
                        MaxInstances = 3
                    }, Projectile.Center);
                }
                else if (InSweep && Elapsed % 14 == 0) {
                    SoundEngine.PlaySound(SoundID.Item97 with {
                        Volume = 0.16f, Pitch = 0.3f, MaxInstances = 3
                    }, Projectile.Center);
                }
            }

            //聚拢期巢壁蜂影骚动：附近蜂巢墙上抖出蜂点（≤1 粒/帧预算）
            if (Elapsed < GatherFrames && Main.rand.NextBool(2)) {
                Vector2 probe = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f)
                    * Main.rand.NextFloat(90f, 200f);
                Point cell = probe.ToTileCoordinates();
                if (WorldGen.InWorld(cell.X, cell.Y, 10)) {
                    int wall = Main.tile[cell.X, cell.Y].WallType;
                    if (wall == WallID.HiveUnsafe || wall == WallID.Hive) {
                        Dust speck = Dust.NewDustPerfect(probe,
                            DustID.Bee, Main.rand.NextVector2Circular(1.2f, 1.2f),
                            60, default, Main.rand.NextFloat(0.8f, 1.1f));
                        speck.noGravity = true;
                    }
                }
            }
            //云内蜂尘（≤1 粒/2 帧预算）
            if (!dissolving && Main.rand.NextBool(2)) {
                Dust inner = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(1f, 1f) * CurrentRadius() * 0.8f,
                    DustID.Bee, Main.rand.NextVector2Circular(0.8f, 0.8f),
                    80, default, 0.9f);
                inner.noGravity = true;
            }
        }

        /// <summary>当前云半径：聚拢期由松散收紧，散去期回胀</summary>
        private float CurrentRadius() {
            float radius = MathHelper.Lerp(LooseRadius, SwarmRadius, GatherProgress);
            float loosen = Math.Max(DisperseProgress, DissolveProgress);
            return radius * (1f + 0.5f * loosen);
        }

        /// <summary>圆盘判定（判定窗已由 hostile 门控，半径略窄于可见云）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = SwarmRadius * CollideRadiusFrac;
            Vector2 center = Projectile.Center;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, center) <= radius * radius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D speckTex = CWRAsset.Extra_98.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float gather = GatherProgress;
            float fade = (0.25f + 0.75f * gather)
                * (1f - Math.Max(DisperseProgress, DissolveProgress));
            if (fade <= 0.01f) {
                return false;
            }
            float radius = CurrentRadius();

            //琥珀云体（真 alpha 暗层）+ 蜜色微光（加色敷料）
            Color deep = new(74, 56, 20);
            Color honey = new(226, 172, 58);
            Main.EntitySpriteDraw(fog, center, null, deep * (0.4f * fade),
                Projectile.identity * 0.8f, fog.Size() * 0.5f,
                radius * 1.3f / fog.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, honey with { A = 0 } * (0.18f * fade),
                0f, glow.Size() * 0.5f, radius * 2.2f / glow.Width, SpriteEffects.None, 0);

            //蜂点：确定性散列在云内绕旋抖动，深色小点承担「一团蜂」的读法
            Color speckColor = new Color(38, 28, 10) * (0.85f * fade);
            Vector2 speckOrigin = speckTex.Size() * 0.5f;
            float time = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < SpeckCount; i++) {
                float hA = Hash(i, 1);
                float hR = Hash(i, 2);
                float hS = Hash(i, 3);
                float orbit = hA * MathHelper.TwoPi + time * (1.5f + hS * 2f) * (hR > 0.5f ? 1f : -1f);
                float jitter = MathF.Sin(time * (9f + hS * 8f) + i * 2.3f) * 5f;
                Vector2 pos = center + orbit.ToRotationVector2()
                    * (radius * (0.15f + 0.75f * MathF.Sqrt(hR)) + jitter);
                float rot = orbit + MathHelper.PiOver2;
                Main.EntitySpriteDraw(speckTex, pos, null, speckColor, rot, speckOrigin,
                    new Vector2(0.05f, 0.03f + 0.02f * hS), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散场：蜂点四散
            for (int i = 0; i < 8; i++) {
                Dust speck = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 24f),
                    DustID.Bee, Main.rand.NextVector2Circular(2.2f, 1.8f),
                    70, default, Main.rand.NextFloat(0.8f, 1.1f));
                speck.noGravity = true;
            }
        }

        /// <summary>确定性散列（各端一致，不触碰 Main.rand）</summary>
        private float Hash(int i, int salt) => (Projectile.identity * 127 + i * 59 + salt * 31) % 83 / 83f;
    }
}
