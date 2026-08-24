using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core
{
    /// <summary>皇后运动与演出库，状态共用</summary>
    internal static class QueenMotion
    {
        /// <summary>圣晶粉，主题主色</summary>
        internal static Color RoyalPink => new(255, 120, 220);
        /// <summary>晶蓝，副色</summary>
        internal static Color CrystalBlue => new(110, 200, 255);
        /// <summary>圣光金白，高光</summary>
        internal static Color HolyGold => new(255, 235, 180);

        /// <summary>棱彩色相环采样，t任意实数</summary>
        public static Color PrismHue(float t) {
            t = (t % 1f + 1f) % 1f;
            float seg = t * 3f;
            if (seg < 1f) {
                return Color.Lerp(RoyalPink, CrystalBlue, seg);
            }
            if (seg < 2f) {
                return Color.Lerp(CrystalBlue, HolyGold, seg - 1f);
            }
            return Color.Lerp(HolyGold, RoyalPink, seg - 2f);
        }

        #region 缓动
        /// <summary>高次幂缓出，芭蕾"发力即到位"的落点曲线</summary>
        public static float SnapOut(float t, int power = 8) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f - (float)Math.Pow(1f - t, power);
        }

        /// <summary>后仰蓄力，前段几乎不动末段猛收</summary>
        public static float LateSnap(float t, int power = 8) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return (float)Math.Pow(t, power);
        }

        /// <summary>0→1→0 拱形，抛物姿态用</summary>
        public static float Bump(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return (float)Math.Sin(t * MathHelper.Pi);
        }
        #endregion

        #region 运动
        /// <summary>阻尼弹簧悬停</summary>
        public static void SpringHover(NPC npc, Vector2 target, float stiffness = 0.014f, float damping = 0.085f, float maxSpeed = 26f) {
            npc.velocity += (target - npc.Center) * stiffness;
            npc.velocity *= 1f - damping;
            if (npc.velocity.Length() > maxSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
        }

        /// <summary>飞行倾斜，速度映射身体侧倾</summary>
        public static void FlightLean(NPC npc, float factor = 0.055f, float maxLean = 0.42f) {
            npc.rotation = MathHelper.Clamp(npc.velocity.X * factor, -maxLean, maxLean);
        }

        /// <summary>芭蕾起跳，一帧全速+方向</summary>
        public static void LaunchHop(NPC npc, float vx, float vy) {
            npc.velocity = new Vector2(vx, vy);
            npc.netUpdate = true;
        }

        /// <summary>底边锚定改缩放，防位置漂移</summary>
        public static void SetScaleAnchored(NPC npc, float scale) {
            if (Math.Abs(scale - npc.scale) < 0.001f) {
                return;
            }
            npc.position.X += npc.width / 2;
            npc.position.Y += npc.height;
            npc.scale = scale;
            npc.width = (int)(114f * scale);
            npc.height = (int)(100f * scale);
            npc.position.X -= npc.width / 2;
            npc.position.Y -= npc.height;
        }

        /// <summary>找脚下地面(世界坐标)，最多向下扫150格</summary>
        public static Vector2 FindGroundBelow(Vector2 worldPos) {
            Point tile = worldPos.ToTileCoordinates();
            for (int i = 0; i < 150 && tile.Y + i < Main.maxTilesY - 10; i++) {
                if (WorldGen.SolidOrSlopedTile(tile.X, tile.Y + i)) {
                    return new Vector2(tile.X * 16f + 8f, (tile.Y + i) * 16f);
                }
            }
            return worldPos;
        }
        #endregion

        #region 演出
        /// <summary>屏幕震动，受设置项</summary>
        public static void Shake(Vector2 pos, float strength, int frames, string uniqueId = "QueenSlime") {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, frames, 2200f, uniqueId);
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>水晶碎裂爆点：碎晶+闪星+光尘+音</summary>
        public static void CrystalShatterBurst(Vector2 pos, float scale, float hueSeed, bool playSound = true) {
            if (VaultUtils.isServer) {
                return;
            }

            int shardCount = (int)(9 * scale);
            for (int i = 0; i < shardCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 9.5f) * scale;
                var shard = PRTLoader.NewParticle<PRT_ATShard>(pos + Main.rand.NextVector2Circular(14f, 14f) * scale,
                    vel, PrismHue(hueSeed + Main.rand.NextFloat(0.25f)), Main.rand.NextFloat(0.5f, 0.95f) * scale);
                shard?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(-0.24f, 0.24f));
            }

            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(pos + Main.rand.NextVector2Circular(20f, 20f) * scale,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), Color.White, Main.rand.NextFloat(0.7f, 1.2f) * scale)?
                    .Configure(PrismHue(hueSeed), Main.rand.Next(18, 30), 0.08f, 1.4f);
            }

            Lighting.AddLight(pos, PrismHue(hueSeed).ToVector3() * 1.1f * scale);
            if (playSound) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.7f * scale, Pitch = 0.35f, MaxInstances = 4 }, pos);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f * scale, Pitch = 0.2f, MaxInstances = 4 }, pos);
            }
        }

        /// <summary>凝胶溅落：胶滴+涟漪色尘</summary>
        public static void GelSplashBurst(Vector2 pos, float scale, int dropletCount = 8) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < dropletCount; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(2f, 7f)) * scale;
                PRTLoader.NewParticle<PRT_QueenGelDrop>(pos + Main.rand.NextVector2Circular(12f, 6f) * scale,
                    vel, RoyalPink * 0.85f, Main.rand.NextFloat(0.7f, 1.25f) * scale);
            }
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(16f, 8f) * scale,
                    DustID.TintableDust, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f)) * scale,
                    120, GetQueenDustColor(), Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>原版皇后凝胶色(随机采样)</summary>
        public static Color GetQueenDustColor() {
            Color blue = new(0, 160, 255);
            Color pinkGray = Color.Lerp(new Color(200, 200, 200), new Color(255, 80, 255), Main.rand.NextFloat());
            return Color.Lerp(blue, pinkGray, Main.rand.NextFloat());
        }

        /// <summary>蓄力内聚闪星，radius边缘向心</summary>
        public static void ChargeGatherFX(Vector2 center, float progress, float radius = 110f, float hueSeed = 0f) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(radius, radius) * (1f - progress * 0.45f);
            PRTLoader.NewParticle<PRT_Sparkle>(spawnPos, (center - spawnPos) * 0.1f,
                Color.White, Main.rand.NextFloat(0.6f, 1.1f) * (0.5f + progress * 0.8f))?
                .Configure(PrismHue(hueSeed + Main.rand.NextFloat(0.3f)), 16, 0.05f, 1.2f);
        }

        /// <summary>足尖落地轻环：击点波纹+尘</summary>
        public static void LandingRingFX(Vector2 pos, float scale, float hueSeed) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, PrismHue(hueSeed) * 0.8f, 0.2f * scale)?
                .Configure(new Vector2(1.6f, 0.4f), 0f, 0.9f * scale, 14);
            GelSplashBurst(pos, scale * 0.7f, 5);
        }
        #endregion

        #region 随从生成(服务端)
        /// <summary>生成随从并写角色/槽位/属主，服务端调用</summary>
        public static NPC SpawnMinion(NPC queen, int npcType, int role, int slot, Vector2 pos, int lifeOverride = 0) {
            if (VaultUtils.isClient) {
                return null;
            }
            int idx = NPC.NewNPC(queen.GetSource_FromAI(), (int)pos.X, (int)pos.Y, npcType,
                ai0: role, ai1: slot, ai2: queen.whoAmI, ai3: 0);
            if (idx < 0 || idx >= Main.maxNPCs) {
                return null;
            }
            NPC minion = Main.npc[idx];
            if (lifeOverride > 0) {
                minion.lifeMax = lifeOverride;
                minion.life = lifeOverride;
            }
            minion.netUpdate = true;
            if (Main.dedServ) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
            }
            return minion;
        }

        /// <summary>剧本击杀：走原生同步死亡链(各端有死亡演出)，隐藏伤害数字，服务端调用</summary>
        public static void ScriptKill(NPC n) {
            NPC.HitInfo hit = new NPC.HitInfo {
                InstantKill = true,
                HideCombatText = true,
            };
            n.StrikeNPC(hit, fromNet: false, noPlayerInteraction: true);
            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendStrikeNPC(n, in hit);
            }
        }

        /// <summary>清场：击碎本皇后麾下全部随从，服务端调用</summary>
        public static void ShatterAllMinions(NPC queen) {
            if (VaultUtils.isClient) {
                return;
            }
            foreach (var n in Main.ActiveNPCs) {
                if ((n.type != NPCID.QueenSlimeMinionBlue && n.type != NPCID.QueenSlimeMinionPink && n.type != NPCID.QueenSlimeMinionPurple)
                    || (int)n.ai[2] != queen.whoAmI || (int)n.ai[0] == QueenMinionRole.None) {
                    continue;
                }
                ScriptKill(n);
            }
        }
        #endregion
    }
}
