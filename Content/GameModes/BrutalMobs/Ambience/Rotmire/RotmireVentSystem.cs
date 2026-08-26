using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rotmire.Projectiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rotmire
{
    /// <summary>
    /// 腐化之地环境机制调度器（权威端）。两个具名机制：
    /// 「瘴气涌泉」深谷底部与洞口周期喷涌瘴柱（柱位由地形采样，预告 ≥45 帧，走位可绕）；
    /// 「邪土蠕动」腐化草地低频拱起蠕动土包（多数静默消退，少数在充分预告后爆孢微伤）。
    /// 档位只调瘴柱频率与密度，机制形状不变；Boss 在场与城镇安宁期一律停手。
    /// 决策与生成只在权威端，客户端经同步弹幕实体看到状态
    /// </summary>
    internal class RotmireVentSystem : ModSystem
    {
        //==== 瘴气涌泉 ====
        /// <summary>喷涌间隔（帧），档位只调频率</summary>
        private static readonly int[] VentIntervalByTier = [560, 450, 350];
        /// <summary>单次脉冲的瘴柱数（密度），毁灭档双柱、各带完整预告</summary>
        private static readonly int[] VentCountByTier = [1, 1, 2];
        /// <summary>瘴柱伤害 = 群系原版敌怪接触伤害 × 此系数（微量，主减益）</summary>
        private const float VentDamageFrac = 0.45f;
        /// <summary>瘴柱全局并发上限</summary>
        private const int VentCap = 3;
        /// <summary>地下变奏：深谷与洞口的瘴气更活跃，间隔缩短</summary>
        private const float UndergroundIntervalMul = 0.85f;

        //==== 邪土蠕动 ====
        /// <summary>土包脉冲间隔（帧），不随档位变化（档位只归瘴柱）</summary>
        private const int MoundInterval = 560;
        /// <summary>爆孢概率（千分比）："哪个会爆"的悬念比例，多数静默消退</summary>
        private const int MoundBurstPermille = 340;
        /// <summary>爆孢伤害 = 群系原版敌怪接触伤害 × 此系数（微伤）</summary>
        private const float MoundDamageFrac = 0.4f;
        /// <summary>土包全局并发上限</summary>
        private const int MoundCap = 4;

        //==== 通用 ====
        /// <summary>条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>城镇安宁半径（60 格）：附近有存活城镇 NPC 时伤害机制不触发</summary>
        private const float TownPeaceRange = 960f;
        /// <summary>单次脉冲的地形采样尝试次数</summary>
        private const int SampleAttempts = 6;

        private static int ventTimer;
        private static int moundTimer;
        /// <summary>轮询游标：多人时轮流选目标玩家（世界级状态，非逐玩家数据）</summary>
        private static int robin;

        public override void ClearWorld() {
            ventTimer = 150;
            moundTimer = 320;
            robin = 0;
        }

        public override void PostUpdateEverything() {
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;//决策与敌对弹幕生成只在权威端
            }
            if (!GameModeSystem.BrutalActive) {
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场暂停伤害性环境机制（计时冻结，不积压）
            }

            if (ventTimer > 0) {
                ventTimer--;
            }
            if (moundTimer > 0) {
                moundTimer--;
            }

            if (ventTimer <= 0) {
                int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
                if (TryVentPulse(tier, out bool underground)) {
                    float mul = underground ? UndergroundIntervalMul : 1f;
                    ventTimer = (int)(VentIntervalByTier[tier - 1] * mul) + Main.rand.Next(90);
                }
                else {
                    ventTimer = RetryFrames;
                }
            }
            if (moundTimer <= 0) {
                moundTimer = TryMoundPulse() ? MoundInterval + Main.rand.Next(120) : RetryFrames;
            }
        }

        //==================== 目标选取 ====================

        /// <summary>轮流挑一位符合条件的玩家（腐化之地内、非死亡、城镇安宁不成立）</summary>
        private static Player PickPlayer() {
            int eligible = 0;
            foreach (Player player in Main.ActivePlayers) {
                if (Eligible(player)) {
                    eligible++;
                }
            }
            if (eligible == 0) {
                return null;
            }
            int pick = robin++ % eligible;
            foreach (Player player in Main.ActivePlayers) {
                if (!Eligible(player)) {
                    continue;
                }
                if (pick-- == 0) {
                    return player;
                }
            }
            return null;
        }

        private static bool Eligible(Player player) {
            if (player.dead || !player.ZoneCorrupt) {
                return false;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(player.Center) < TownPeaceRange) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>统计某类弹幕的活动实例数（只在脉冲时刻调用，非每帧）</summary>
        private static int CountActive(int projType) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType) {
                    count++;
                }
            }
            return count;
        }

        private static bool AnyProjNear(int projType, Vector2 pos, float range) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && proj.Distance(pos) < range) {
                    return true;
                }
            }
            return false;
        }

        //==================== 瘴气涌泉 ====================

        private static bool TryVentPulse(int tier, out bool underground) {
            underground = false;
            Player target = PickPlayer();
            if (target == null) {
                return false;
            }
            underground = target.Center.Y > Main.worldSurface * 16.0;
            int ventType = ModContent.ProjectileType<RotmireVentProj>();
            if (CountActive(ventType) >= VentCap) {
                return false;
            }

            int want = VentCountByTier[tier - 1];
            int got = 0;
            for (int attempt = 0; attempt < SampleAttempts && got < want; attempt++) {
                if (!TrySampleVentSite(target, out Vector2 basePos, out bool gorge)) {
                    continue;
                }
                if (AnyProjNear(ventType, basePos, 100f)) {
                    continue;
                }
                float scale = (gorge ? 1.15f : 1f) * Main.rand.NextFloat(0.92f, 1.08f);
                Projectile.NewProjectile(new EntitySource_WorldEvent(), basePos, Vector2.Zero,
                    ventType, AnchorDamage(VentDamageFrac), 1f, Main.myPlayer,
                    scale, gorge ? 1f : 0f);
                got++;
            }
            return got > 0;
        }

        /// <summary>
        /// 柱位由地形采样：目标两侧 7~26 格内找腐化质地面，上方 ≥7 格净空供瘴柱生长；
        /// 两侧地表明显更高判为谷底（洞内地形天然多谷，洞口涌泉由此涌现）
        /// </summary>
        private static bool TrySampleVentSite(Player target, out Vector2 basePos, out bool gorge) {
            basePos = default;
            gorge = false;
            int px = (int)(target.Center.X / 16f);
            int py = (int)(target.Center.Y / 16f);
            int x = px + Main.rand.Next(7, 27) * (Main.rand.NextBool() ? 1 : -1);
            if (!FindFloor(x, py - 12, 34, out int floorY)) {
                return false;
            }
            if (!IsCorruptTile(x, floorY)) {
                return false;
            }
            for (int dy = 1; dy <= 7; dy++) {
                if (WorldGen.SolidTile(x, floorY - dy)) {
                    return false;
                }
            }
            basePos = new Vector2(x * 16f + 8f, floorY * 16f);
            if (FindFloor(x - 3, floorY - 9, 18, out int leftY)
                && FindFloor(x + 3, floorY - 9, 18, out int rightY)) {
                gorge = leftY <= floorY - 2 && rightY <= floorY - 2;
            }
            return true;
        }

        //==================== 邪土蠕动 ====================

        private static bool TryMoundPulse() {
            Player target = PickPlayer();
            if (target == null) {
                return false;
            }
            int moundType = ModContent.ProjectileType<RotmireWritheProj>();
            if (CountActive(moundType) >= MoundCap) {
                return false;
            }

            //一次 1~2 个土包，各自独立掷"会不会爆"
            int want = 1 + (Main.rand.NextBool() ? 1 : 0);
            int got = 0;
            for (int attempt = 0; attempt < SampleAttempts && got < want; attempt++) {
                if (!TrySampleMoundSite(target, out Vector2 basePos)) {
                    continue;
                }
                if (AnyProjNear(moundType, basePos, 80f)) {
                    continue;
                }
                bool burst = Main.rand.Next(1000) < MoundBurstPermille;
                float scale = Main.rand.NextFloat(0.85f, 1.2f);
                Projectile.NewProjectile(new EntitySource_WorldEvent(), basePos, Vector2.Zero,
                    moundType, burst ? AnchorDamage(MoundDamageFrac) : 0, 0f, Main.myPlayer,
                    burst ? 1f : 0f, scale);
                got++;
            }
            return got > 0;
        }

        /// <summary>土包只长在腐化草地上，上方 ≥4 格净空</summary>
        private static bool TrySampleMoundSite(Player target, out Vector2 basePos) {
            basePos = default;
            int px = (int)(target.Center.X / 16f);
            int py = (int)(target.Center.Y / 16f);
            int x = px + Main.rand.Next(5, 23) * (Main.rand.NextBool() ? 1 : -1);
            if (!FindFloor(x, py - 12, 30, out int floorY)) {
                return false;
            }
            Tile tile = Main.tile[x, floorY];
            if (!tile.HasTile || tile.TileType != TileID.CorruptGrass) {
                return false;
            }
            for (int dy = 1; dy <= 4; dy++) {
                if (WorldGen.SolidTile(x, floorY - dy)) {
                    return false;
                }
            }
            basePos = new Vector2(x * 16f + 8f, floorY * 16f);
            return true;
        }

        //==================== 地形与数值 ====================

        /// <summary>自上而下先越过实心找到空腔，再落到腔底实心面（兼容地表与洞穴）</summary>
        private static bool FindFloor(int x, int fromY, int span, out int floorY) {
            floorY = 0;
            bool inAir = false;
            for (int y = fromY; y < fromY + span; y++) {
                if (!WorldGen.InWorld(x, y, 10)) {
                    return false;
                }
                bool solid = WorldGen.SolidTile(x, y);
                if (!inAir) {
                    if (!solid) {
                        inAir = true;
                    }
                    continue;
                }
                if (solid) {
                    floorY = y;
                    return true;
                }
            }
            return false;
        }

        private static bool IsCorruptTile(int x, int y) {
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile) {
                return false;
            }
            return tile.TileType is TileID.CorruptGrass or TileID.Ebonstone or TileID.Ebonsand
                or TileID.CorruptIce or TileID.CorruptSandstone or TileID.CorruptHardenedSand;
        }

        /// <summary>
        /// 群系伤害锚：腐化之地原版敌怪接触伤害（困难前噬魂怪 / 困难后腐化者的未缩放模板值）× 机制系数。
        /// 敌对弹幕命中玩家时原版自带 ×2（难度再放大：经典 ×2/专家 ×4/大师 ×6），此处已预除原版
        /// 敌对弹幕 ×2 结算系数：damage = 经典档目标实收 ÷ 2 = 接触伤 × frac × 0.5，
        /// 实际实收 ≈ 接触伤 × frac，随难度自动跟走，禁止再叠任何手动难度乘数。
        /// ContentSamples 在载入期（普通难度）构建，是稳定的原版基准；读取异常时用具名常量兜底
        /// </summary>
        private static int AnchorDamage(float frac) {
            int baseDamage = Main.hardMode ? 50 : 22;
            int anchorType = Main.hardMode ? NPCID.Corruptor : NPCID.EaterofSouls;
            if (ContentSamples.NpcsByNetId.TryGetValue(anchorType, out NPC sample) && sample.damage > 0) {
                baseDamage = sample.damage;
            }
            return Math.Max(1, (int)(baseDamage * frac * 0.5f));
        }
    }
}
