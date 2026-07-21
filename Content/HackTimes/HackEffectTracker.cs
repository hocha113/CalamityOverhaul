using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>单个骇入效果运行时实例</summary>
    internal class ActiveHackEffect
    {
        public QuickHackDef Hack;
        public IHackTarget Target;
        /// <summary>施法玩家 whoAmI</summary>
        public int CasterIndex;
        /// <summary>已持续帧数</summary>
        public int Elapsed;
        internal float ElapsedCarry;
        public bool Active = true;
        public bool Applied;
        /// <summary>Boss 效果倍率，Boss 0.5f / 普通 1f</summary>
        public float EffectMult = 1f;
        /// <summary>传播代数，0 初始，1 已传一次</summary>
        public int Generation;

        //兼容旧 API

        /// <summary>NpcScannable 时 NPC 索引，否则 -1</summary>
        public int TargetIndex => Target is NpcScannable n ? n.NpcIndex : -1;
        /// <summary>TileScannable 时物块 X，否则 -1</summary>
        public int TileX => Target is TileScannable t ? t.TileCoordX : -1;
        /// <summary>TileScannable 时物块 Y，否则 -1</summary>
        public int TileY => Target is TileScannable t ? t.TileCoordY : -1;
    }

    /// <summary>骇入效果全局追踪器，Apply→Tick→Remove</summary>
    internal class HackEffectTracker : ICWRLoader
    {
        private static readonly List<ActiveHackEffect> activeEffects = [];
        //帧内移除缓冲
        private static readonly List<ActiveHackEffect> removeBuffer = [];
        //OnRemove 传播延迟入队，避遍历中追加
        private static readonly List<ActiveHackEffect> pendingEffects = [];
        private static bool updatingNpcEffects;

        private static readonly List<ActiveHackEffect> activeTileEffects = [];
        private static readonly List<ActiveHackEffect> tileRemoveBuffer = [];

        //击杀回收 RAM 比例
        private const float KillRefundRatio = 0.5f;
        //本帧已退过的 NPC，防重复
        private static readonly HashSet<int> killRefundedThisFrame = [];

        void ICWRLoader.UnLoadData() => Reset();

        #region NPC 效果

        /// <summary>对 NPC 施加骇入效果</summary>
        public static ActiveHackEffect ApplyNpcEffect(QuickHackDef hack, int targetIndex, int casterIndex) {
            if (targetIndex < 0 || targetIndex >= Main.maxNPCs) return null;
            NPC npc = Main.npc[targetIndex];
            if (npc == null || !npc.active) return null;

            var target = new NpcScannable(targetIndex);
            if (!hack.CanApplyTo(target)) return null;

            var effect = new ActiveHackEffect {
                Hack = hack,
                Target = target,
                CasterIndex = casterIndex,
                Elapsed = 0,
                Active = true,
                Applied = false,
                //Boss 减半
                EffectMult = npc.boss ? 0.5f : 1f,
            };

            if (updatingNpcEffects)
                pendingEffects.Add(effect);
            else
                activeEffects.Add(effect);
            return effect;
        }

        /// <summary>兼容旧 API，见 <see cref="ApplyNpcEffect"/></summary>
        public static ActiveHackEffect Apply(QuickHackDef hack, int targetIndex, int casterIndex)
            => ApplyNpcEffect(hack, targetIndex, casterIndex);

        /// <summary>每帧更新 NPC 效果</summary>
        public static void Update() {
            removeBuffer.Clear();
            killRefundedThisFrame.Clear();

            updatingNpcEffects = true;
            for (int i = 0; i < activeEffects.Count; i++) {
                var eff = activeEffects[i];
                if (!eff.Active) {
                    removeBuffer.Add(eff);
                    continue;
                }

                NPC npc = eff.Target is NpcScannable n ? Main.npc[n.NpcIndex] : null;
                if (npc == null || !npc.active || npc.life <= 0) {
                    if (!HackTime.InfiniteHack && npc != null && eff.Target is NpcScannable np
                        && !killRefundedThisFrame.Contains(np.NpcIndex)) {
                        OnHackedTargetKilled(npc, np.NpcIndex);
                    }
                    eff.Active = false;
                    removeBuffer.Add(eff);
                    continue;
                }

                Player caster = eff.CasterIndex >= 0 && eff.CasterIndex < Main.maxPlayers
                    ? Main.player[eff.CasterIndex] : Main.LocalPlayer;

                //首帧 OnApply
                if (!eff.Applied) {
                    eff.Applied = true;
                    eff.Hack.OnApply(eff.Target, caster);
                }

                //时长 × EffectMult
                int effectiveDuration = (int)(eff.Hack.GetDuration() * eff.EffectMult);
                if (effectiveDuration > 0 && eff.Elapsed >= effectiveDuration) {
                    eff.Hack.OnRemove(eff.Target);
                    eff.Active = false;
                    removeBuffer.Add(eff);
                    continue;
                }

                //duration=0 即结束
                if (eff.Hack.GetDuration() == 0 && eff.Applied) {
                    eff.Active = false;
                    removeBuffer.Add(eff);
                    continue;
                }

                bool alive = eff.Hack.OnTick(eff.Target, eff.Elapsed);
                if (!alive) {
                    eff.Hack.OnRemove(eff.Target);
                    eff.Active = false;
                    removeBuffer.Add(eff);
                    continue;
                }

                eff.Elapsed += TimeGear.PullFrameAdvance(ref eff.ElapsedCarry);
            }
            updatingNpcEffects = false;

            for (int i = 0; i < removeBuffer.Count; i++) {
                activeEffects.Remove(removeBuffer[i]);
            }

            if (pendingEffects.Count > 0) {
                activeEffects.AddRange(pendingEffects);
                pendingEffects.Clear();
            }
        }

        /// <summary>NPC 是否有某类型活跃效果</summary>
        public static bool HasEffect<T>(int npcIndex) where T : QuickHackDef {
            for (int i = 0; i < activeEffects.Count; i++) {
                var e = activeEffects[i];
                if (e.Active && e.Hack is T && e.Target is NpcScannable n && n.NpcIndex == npcIndex)
                    return true;
            }
            for (int i = 0; i < pendingEffects.Count; i++) {
                var e = pendingEffects[i];
                if (e.Active && e.Hack is T && e.Target is NpcScannable n && n.NpcIndex == npcIndex)
                    return true;
            }
            return false;
        }

        /// <summary>效果进度 0~1，无则 -1</summary>
        public static float GetEffectProgress<T>(int npcIndex) where T : QuickHackDef {
            for (int i = 0; i < activeEffects.Count; i++) {
                var eff = activeEffects[i];
                if (!eff.Active || eff.Hack is not T) continue;
                if (eff.Target is not NpcScannable n || n.NpcIndex != npcIndex) continue;
                int dur = (int)(eff.Hack.GetDuration() * eff.EffectMult);
                if (dur <= 0) return 1f;
                return Math.Clamp((float)eff.Elapsed / dur, 0f, 1f);
            }
            return -1f;
        }

        /// <summary>NPC 身上全部活跃效果</summary>
        public static void GetEffects(int npcIndex, List<ActiveHackEffect> result) {
            result.Clear();
            for (int i = 0; i < activeEffects.Count; i++) {
                var e = activeEffects[i];
                if (e.Active && e.Target is NpcScannable n && n.NpcIndex == npcIndex)
                    result.Add(e);
            }
        }

        /// <summary>某类型效果实例，无则 null</summary>
        public static ActiveHackEffect GetEffect<T>(int npcIndex) where T : QuickHackDef {
            for (int i = 0; i < activeEffects.Count; i++) {
                var eff = activeEffects[i];
                if (eff.Active && eff.Hack is T && eff.Target is NpcScannable n && n.NpcIndex == npcIndex)
                    return eff;
            }
            return null;
        }

        /// <summary>NPC 活跃效果只读列表</summary>
        public static IReadOnlyList<ActiveHackEffect> AllActiveEffects => activeEffects;

        //群组扩散复用缓冲
        private static readonly List<NPC> groupBuffer = [];

        /// <summary>扩散到多实体 Boss 群组，同类型跳过防递归</summary>
        public static void PropagateNpcEffectToGroup<T>(T hack, int rootNpcIndex,
            int casterIndex, System.Action<NPC> onSpread = null) where T : QuickHackDef {
            if (hack == null || rootNpcIndex < 0 || rootNpcIndex >= Main.maxNPCs) return;
            NPC root = Main.npc[rootNpcIndex];
            if (root == null || !root.active) return;

            Common.NpcGroupHelper.CollectGroup(root, groupBuffer);
            for (int i = 0; i < groupBuffer.Count; i++) {
                NPC member = groupBuffer[i];
                if (member.whoAmI == rootNpcIndex) continue;
                if (HasEffect<T>(member.whoAmI)) continue;
                onSpread?.Invoke(member);
                ApplyNpcEffect(hack, member.whoAmI, casterIndex);
            }
            groupBuffer.Clear();
        }

        //击杀按比例退 RAM
        private static void OnHackedTargetKilled(NPC target, int npcIndex) {
            killRefundedThisFrame.Add(npcIndex);

            int totalCost = 0;
            for (int i = 0; i < activeEffects.Count; i++) {
                var e = activeEffects[i];
                if (e.Active && e.Target is NpcScannable n && n.NpcIndex == npcIndex)
                    totalCost += e.Hack.RamCost;
            }
            if (totalCost <= 0) return;

            float refund = totalCost * KillRefundRatio;
            if (refund < 1f) refund = 1f;
            float before = RamSystem.CurrentRam;
            RamSystem.Restore(refund);
            float actual = RamSystem.CurrentRam - before;

            if (actual > 0.01f && !VaultUtils.isServer) {
                string text = HackTime.RamRefund.Format(actual.ToString("F0"));
                CombatText.NewText(target.Hitbox, HackTheme.Accent, text, true);
                SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.35f, Pitch = 0.4f },
                    target.Center);
            }
        }

        #endregion

        #region 物块效果

        /// <summary>对物块施加协议效果</summary>
        public static ActiveHackEffect ApplyTileEffect(QuickHackDef hack, int tileX, int tileY, int casterIndex) {
            if (tileX < 0 || tileX >= Main.maxTilesX || tileY < 0 || tileY >= Main.maxTilesY)
                return null;
            if (!Main.tile[tileX, tileY].HasTile) return null;

            var target = new TileScannable(tileX, tileY);
            if (!hack.CanApplyTo(target)) return null;

            var effect = new ActiveHackEffect {
                Hack = hack,
                Target = target,
                CasterIndex = casterIndex,
                Elapsed = 0,
                Active = true,
                Applied = false,
            };

            activeTileEffects.Add(effect);
            return effect;
        }

        /// <summary>兼容旧 API，见 <see cref="ApplyTileEffect"/></summary>
        public static ActiveHackEffect ApplyToTile(QuickHackDef hack, int tileX, int tileY, int casterIndex)
            => ApplyTileEffect(hack, tileX, tileY, casterIndex);

        /// <summary>每帧更新物块效果</summary>
        public static void UpdateTileEffects() {
            tileRemoveBuffer.Clear();

            for (int i = 0; i < activeTileEffects.Count; i++) {
                var eff = activeTileEffects[i];
                if (!eff.Active) {
                    tileRemoveBuffer.Add(eff);
                    continue;
                }

                if (eff.Target is not TileScannable ts || !ts.IsValid) {
                    eff.Active = false;
                    tileRemoveBuffer.Add(eff);
                    continue;
                }

                Player caster = eff.CasterIndex >= 0 && eff.CasterIndex < Main.maxPlayers
                    ? Main.player[eff.CasterIndex] : Main.LocalPlayer;

                //首帧 OnApply
                if (!eff.Applied) {
                    eff.Applied = true;
                    eff.Hack.OnApply(eff.Target, caster);
                }

                int duration = eff.Hack.GetDuration();
                if (duration > 0 && eff.Elapsed >= duration) {
                    eff.Hack.OnRemove(eff.Target);
                    eff.Active = false;
                    tileRemoveBuffer.Add(eff);
                    continue;
                }

                if (duration == 0 && eff.Applied) {
                    eff.Active = false;
                    tileRemoveBuffer.Add(eff);
                    continue;
                }

                bool alive = eff.Hack.OnTick(eff.Target, eff.Elapsed);
                if (!alive) {
                    eff.Hack.OnRemove(eff.Target);
                    eff.Active = false;
                    tileRemoveBuffer.Add(eff);
                    continue;
                }

                eff.Elapsed += TimeGear.PullFrameAdvance(ref eff.ElapsedCarry);
            }

            for (int i = 0; i < tileRemoveBuffer.Count; i++) {
                activeTileEffects.Remove(tileRemoveBuffer[i]);
            }
        }

        /// <summary>物块是否有某类型活跃效果</summary>
        public static bool HasTileEffect<T>(int tileX, int tileY) where T : QuickHackDef {
            for (int i = 0; i < activeTileEffects.Count; i++) {
                var e = activeTileEffects[i];
                if (e.Active && e.Hack is T && e.Target is TileScannable t
                    && t.TileCoordX == tileX && t.TileCoordY == tileY)
                    return true;
            }
            return false;
        }

        /// <summary>物块坐标上全部活跃效果</summary>
        public static void GetTileEffects(int tileX, int tileY, List<ActiveHackEffect> result) {
            result.Clear();
            for (int i = 0; i < activeTileEffects.Count; i++) {
                var e = activeTileEffects[i];
                if (e.Active && e.Target is TileScannable t
                    && t.TileCoordX == tileX && t.TileCoordY == tileY)
                    result.Add(e);
            }
        }

        /// <summary>物块活跃效果只读列表</summary>
        public static IReadOnlyList<ActiveHackEffect> AllActiveTileEffects => activeTileEffects;

        #endregion

        public static void Reset() {
            updatingNpcEffects = false;
            activeEffects.Clear();
            removeBuffer.Clear();
            pendingEffects.Clear();
            killRefundedThisFrame.Clear();

            for (int i = 0; i < activeTileEffects.Count; i++) {
                var eff = activeTileEffects[i];
                if (!eff.Active || !eff.Applied || eff.Target == null || !eff.Target.IsValid) continue;
                eff.Hack.OnRemove(eff.Target);
            }

            activeTileEffects.Clear();
            tileRemoveBuffer.Clear();
        }
    }
}
