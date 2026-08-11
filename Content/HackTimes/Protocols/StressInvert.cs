using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 应力反转：以命中格为心二十格半径内物块十秒不可破坏（对施术者自己同样生效），
    /// 高速撞上区内地形的敌怪按体质掉血。<br/>
    /// 「不可破坏标记」是纯账本登记，不写任何世界数据，还原＝移除登记；
    /// 三条清账路径：OnRemove（到期）、<see cref="SweepZones"/> 每帧与追踪器对账
    /// （锚点失效等异常路径的兜底）、世界卸载与模组卸载清全表。
    /// 账本在各端都登记（本机挖掘的 CanKillTile 拦截跑在客户端），
    /// 即使移除包丢失，对账扫描也会在效果消失的下一帧拆掉登记，不会永久锁区
    /// </summary>
    internal class StressInvert : QuickHackDef
    {
        //反转持续（帧，10 秒）
        internal const int InvertDuration = 60 * 10;
        //保护半径（格）
        internal const int ZoneRadius = 20;
        //每只敌怪的撞墙伤害冷却（帧）
        private const int HitCooldownFrames = 20;
        //触发撞墙伤害的最低撞击速度，轻轻蹭墙不算
        private const float MinImpactSpeed = 4f;

        internal sealed class StressZone
        {
            //撞墙冷却：NPC 槽位 → 上次结算帧。冷却只有 20f，槽位短期复用无碍
            public readonly Dictionary<int, ulong> HitStamps = [];
        }

        //锚点格 → 应力区
        private static readonly Dictionary<(int X, int Y), StressZone> zones = [];

        public override void SetDefaults() {
            UploadTime = 130;
            RamCost = 5;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => InvertDuration;

        public override void Unload() {
            base.Unload();
            zones.Clear();
        }

        internal static void ClearAllZones() => zones.Clear();

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not TileScannable s) return false;
            //同一锚点已在生效时不重复登记
            return !zones.ContainsKey((s.TileCoordX, s.TileCoordY));
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not TileScannable s) return false;
            zones[(s.TileCoordX, s.TileCoordY)] = new StressZone();
            if (Main.netMode != NetmodeID.Server) {
                EmitSealCue(s.TileCoordX, s.TileCoordY);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is not TileScannable s) return;
            if (!HackTargets.InWorld(s.TileCoordX, s.TileCoordY)) return;
            zones[(s.TileCoordX, s.TileCoordY)] = new StressZone();
            EmitSealCue(s.TileCoordX, s.TileCoordY);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server && target is TileScannable s) {
                TickZoneVisual(s.TileCoordX, s.TileCoordY, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (target is TileScannable s) {
                TickZoneVisual(s.TileCoordX, s.TileCoordY, elapsed);
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not TileScannable s) return;
            zones.Remove((s.TileCoordX, s.TileCoordY));
            if (Main.netMode != NetmodeID.Server) {
                EmitUnsealCue(s.TileCoordX, s.TileCoordY);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (target is not TileScannable s) return;
            zones.Remove((s.TileCoordX, s.TileCoordY));
            EmitUnsealCue(s.TileCoordX, s.TileCoordY);
        }

        #region 区域账本与不可破坏判定

        /// <summary>该格是否处于任一应力区内；镐、爆破、锤斜面共用这一道闸</summary>
        internal static bool IsProtected(int i, int j) {
            if (zones.Count == 0) return false;
            foreach ((int X, int Y) key in zones.Keys) {
                int dx = i - key.X;
                int dy = j - key.Y;
                if (dx * dx + dy * dy <= ZoneRadius * ZoneRadius) return true;
            }
            return false;
        }

        private static readonly List<(int X, int Y)> staleKeyBuffer = [];

        /// <summary>
        /// 每帧与追踪器对账：效果不在了登记就得拆，
        /// 否则锚点异常失效（比如被绕过 CanKillTile 的直接 KillTile 拆掉）会留下永久锁区
        /// </summary>
        internal static void SweepZones() {
            if (zones.Count == 0) return;
            staleKeyBuffer.Clear();
            foreach ((int X, int Y) key in zones.Keys) {
                if (!HackEffectTracker.HasTileEffect<StressInvert>(key.X, key.Y)) {
                    staleKeyBuffer.Add(key);
                }
            }
            for (int i = 0; i < staleKeyBuffer.Count; i++) {
                zones.Remove(staleKeyBuffer[i]);
            }
            staleKeyBuffer.Clear();
        }

        #endregion

        #region 撞墙伤害（权威端）

        /// <summary>
        /// 敌怪高速撞上区内地形按体质掉血。判定用「中心在区内 + 本帧发生物块碰撞 +
        /// 撞击速度过阈」的近似，不逐格反查它撞的是哪一块——半径二十格，边缘误差可忽略
        /// </summary>
        internal static void TickCollisionDamage() {
            if (Main.netMode == NetmodeID.MultiplayerClient || zones.Count == 0) return;
            ulong frame = Main.GameUpdateCount;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.dontTakeDamage
                    || npc.immortal || npc.CountsAsACritter) {
                    continue;
                }
                if (!npc.collideX && !npc.collideY) continue;
                //oldVelocity 是碰撞吃掉之前的速度，撞击强度看被吃掉的那个轴
                float impact = Math.Max(
                    npc.collideX ? Math.Abs(npc.oldVelocity.X) : 0f,
                    npc.collideY ? Math.Abs(npc.oldVelocity.Y) : 0f);
                if (impact < MinImpactSpeed) continue;

                int tileX = (int)(npc.Center.X / 16f);
                int tileY = (int)(npc.Center.Y / 16f);
                StressZone zone = FindZoneContaining(tileX, tileY);
                if (zone == null) continue;
                if (zone.HitStamps.TryGetValue(i, out ulong last)
                    && frame - last < (ulong)HitCooldownFrames) {
                    continue;
                }
                zone.HitStamps[i] = frame;

                //Boss 减半对齐追踪器的 EffectMult 惯例
                float ratio = npc.boss ? 0.02f : 0.04f;
                int dmg = Math.Max(12, (int)(npc.lifeMax * ratio));
                npc.SimpleStrikeNPC(dmg, 0, false, 0f, null, false, 0f, true);
                if (Main.netMode != NetmodeID.Server) {
                    EmitImpact(npc.Center, impact);
                }
            }
        }

        private static StressZone FindZoneContaining(int tileX, int tileY) {
            foreach (KeyValuePair<(int X, int Y), StressZone> kvp in zones) {
                int dx = tileX - kvp.Key.X;
                int dy = tileY - kvp.Key.Y;
                if (dx * dx + dy * dy <= ZoneRadius * ZoneRadius) return kvp.Value;
            }
            return null;
        }

        #endregion

        #region 表现（各端各自演）

        private static void EmitSealCue(int anchorX, int anchorY) {
            Vector2 center = HackTargets.TileWorldCenter(anchorX, anchorY);
            //一圈向内收拢的封边脉冲
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 pos = center + angle.ToRotationVector2() * ZoneRadius * 16f;
                Vector2 vel = (center - pos).SafeNormalize(Vector2.Zero) * 3f;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, HackTheme.Accent, 0.9f)
                    ?.Configure(false, 30);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.55f, Pitch = -0.2f },
                    center);
            }
        }

        private static void TickZoneVisual(int anchorX, int anchorY, int elapsed) {
            Vector2 center = HackTargets.TileWorldCenter(anchorX, anchorY);
            //边界巡走的方格微粒，读作「这一圈被封了」
            if (elapsed % 4 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = center + angle.ToRotationVector2() * ZoneRadius * 16f;
                var square = PRTLoader.NewParticle<PRT_CyberSquare>(pos,
                    (center - pos).SafeNormalize(Vector2.Zero) * 0.4f,
                    HackTheme.Accent, 0.7f);
                square?.Configure(HackTheme.AccentAlt, 18);
            }
            //整圈慢脉冲
            if (elapsed % 45 == 0) {
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f + elapsed * 0.01f;
                    Vector2 pos = center + angle.ToRotationVector2() * ZoneRadius * 16f;
                    PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero,
                        HackTheme.Accent, 0.5f)?.Configure(false, 16);
                }
            }
        }

        private static void EmitUnsealCue(int anchorX, int anchorY) {
            Vector2 center = HackTargets.TileWorldCenter(anchorX, anchorY);
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 pos = center + angle.ToRotationVector2() * ZoneRadius * 8f;
                Vector2 vel = (pos - center).SafeNormalize(Vector2.Zero) * 2.5f;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    new Color(120, 180, 220), 0.7f)?.Configure(false, 20);
            }
        }

        private static void EmitImpact(Vector2 pos, float impact) {
            int count = (int)MathHelper.Clamp(impact, 4f, 10f);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, HackTheme.Danger, 0.9f)
                    ?.Configure(true, 20);
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.5f }, pos);
        }

        #endregion
    }

    /// <summary>应力区的对账扫描与撞墙结算；世界卸载清账</summary>
    internal class StressInvertSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            //先对账再结算，拆掉的区不再打人
            StressInvert.SweepZones();
            StressInvert.TickCollisionDamage();
        }

        public override void OnWorldUnload() => StressInvert.ClearAllZones();
    }

    /// <summary>
    /// 不可破坏闸：镐（CanKillTile）、爆破（CanExplode）、锤斜面（Slope）统一拒绝，
    /// 区内对所有人生效，施术者自己也挖不开。
    /// 已知缝隙：绕过 CanKillTile 的直接 WorldGen.KillTile 调用（部分事件/模组行为）拦不住，
    /// 由 SweepZones 保证即使锚点被这样拆掉也不会留下永久锁区
    /// </summary>
    internal class StressInvertTileHook : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
            => !StressInvert.IsProtected(i, j);

        public override bool CanExplode(int i, int j, int type)
            => !StressInvert.IsProtected(i, j);

        public override bool Slope(int i, int j, int type)
            => !StressInvert.IsProtected(i, j);
    }
}
