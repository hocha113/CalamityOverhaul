using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.Blessings
{
    /// <summary>
    /// 祝福玩家面：点燃集（启用取舍）与会话态。
    /// 解锁在世界档案（<see cref="BlessingWorld"/>），点燃是纯本地取舍——
    /// 效果全部在本端钩子里成立，不落 static、不需要联网同步
    /// </summary>
    internal class BlessingPlayer : ModPlayer
    {
        /// <summary>已点燃的祝福 ID（玩家档持久，跨世界保留取舍）</summary>
        internal HashSet<string> Kindled = [];

        /// <summary>已在往生轮上见过的祝福（新焰苗提示消隐用，玩家档持久）</summary>
        internal HashSet<string> Witnessed = [];

        /// <summary>会话态槽存储，按祝福 ID 惰性分配，不进档</summary>
        private readonly Dictionary<string, float[]> states = [];

        /// <summary>取该祝福在本玩家身上的会话态槽数组</summary>
        internal float[] StateOf(Blessing blessing) {
            if (!states.TryGetValue(blessing.ID, out float[] slots) || slots.Length < blessing.StateSlots) {
                slots = new float[blessing.StateSlots];
                states[blessing.ID] = slots;
            }
            return slots;
        }

        /// <summary>祝福系统是否在本世界生效（修罗/死神永生在开）</summary>
        internal static bool SystemActive => GameModeSystem.AsuraActive;

        /// <summary>该祝福是否燃焰（生效）：模式开 + 世界已解锁 + 本人已点燃</summary>
        internal bool IsBurning(Blessing blessing)
            => SystemActive && BlessingWorld.IsUnlocked(blessing) && Kindled.Contains(blessing.ID);

        /// <summary>当前燃焰数（只数已解锁的）</summary>
        internal int BurningCount {
            get {
                int count = 0;
                foreach (Blessing blessing in BlessingRegistry.All) {
                    if (BlessingWorld.IsUnlocked(blessing) && Kindled.Contains(blessing.ID)) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>燃焰槽上限：基础数随讨伐数成长，封顶</summary>
        internal static int SlotCap
            => Math.Min(BlessingTuning.SlotBase + BlessingWorld.UnlockedCount / BlessingTuning.SlotGrowthStep,
                BlessingTuning.SlotMax);

        /// <summary>是否还有解锁后未在往生轮上看过的祝福（HUD 新焰苗）</summary>
        internal bool HasUnwitnessed {
            get {
                foreach (Blessing blessing in BlessingRegistry.All) {
                    if (BlessingWorld.IsUnlocked(blessing) && !Witnessed.Contains(blessing.ID)) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>点燃；未解锁或槽满时拒绝并返回 false</summary>
        internal bool TryKindle(Blessing blessing) {
            if (!BlessingWorld.IsUnlocked(blessing)) {
                return false;
            }
            if (Kindled.Contains(blessing.ID)) {
                return true;
            }
            if (BurningCount >= SlotCap) {
                return false;
            }
            Kindled.Add(blessing.ID);
            return true;
        }

        /// <summary>熄灭</summary>
        internal void Snuff(Blessing blessing) => Kindled.Remove(blessing.ID);

        /// <summary>在往生轮上看过该祝福</summary>
        internal void MarkWitnessed(Blessing blessing) => Witnessed.Add(blessing.ID);

        //——存档——

        public override void SaveData(TagCompound tag) {
            if (Kindled.Count > 0) {
                tag["BlessingKindled"] = new List<string>(Kindled);
            }
            if (Witnessed.Count > 0) {
                tag["BlessingWitnessed"] = new List<string>(Witnessed);
            }
        }

        public override void LoadData(TagCompound tag) {
            Kindled.Clear();
            Witnessed.Clear();
            if (tag.TryGet("BlessingKindled", out List<string> kindled) && kindled != null) {
                foreach (string id in kindled) {
                    Kindled.Add(id);
                }
            }
            if (tag.TryGet("BlessingWitnessed", out List<string> witnessed) && witnessed != null) {
                foreach (string id in witnessed) {
                    Witnessed.Add(id);
                }
            }
        }

        /// <summary>
        /// 溢出归位：本世界解锁数可能少于上个世界，燃焰数会超过槽上限；
        /// 按进度序保留前上限盏，其余熄灭
        /// </summary>
        private void NormalizeOverflow() {
            int cap = SlotCap;
            int burning = 0;
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (!BlessingWorld.IsUnlocked(blessing) || !Kindled.Contains(blessing.ID)) {
                    continue;
                }
                burning++;
                if (burning > cap) {
                    Kindled.Remove(blessing.ID);
                }
            }
        }

        public override void OnEnterWorld() => NormalizeOverflow();

        //——效果分发：统一以燃焰为门——

        public override void PostUpdateMiscEffects() {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.PostUpdate(this);
                }
            }
        }

        public override void UpdateEquips() {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.UpdateEquips(this);
                }
            }
        }

        public override void UpdateLifeRegen() {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.UpdateLifeRegen(this);
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.ModifyHitNPC(this, target, ref modifiers);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.OnHitNPC(this, target, in hit, damageDone);
                }
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.ModifyHitByNPC(this, npc, ref modifiers);
                }
            }
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.ModifyHurt(this, ref modifiers);
                }
            }
        }

        public override void PostHurt(Player.HurtInfo info) {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.PostHurt(this, in info);
                }
            }
        }

        public override bool FreeDodge(Player.HurtInfo info) {
            if (!SystemActive) {
                return false;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing) && blessing.FreeDodge(this, in info)) {
                    return true;
                }
            }
            return false;
        }

        public override bool CanConsumeAmmo(Item weapon, Item ammo) {
            if (!SystemActive) {
                return true;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing) && !blessing.CanConsumeAmmo(this, weapon, ammo)) {
                    return false;
                }
            }
            return true;
        }

        public override float UseSpeedMultiplier(Item item) {
            if (!SystemActive) {
                return 1f;
            }
            float mult = 1f;
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    mult *= blessing.UseSpeedMultiplier(this, item);
                }
            }
            return mult;
        }

        public override void GetHealLife(Item item, bool quickHeal, ref int healValue) {
            if (!SystemActive) {
                return;
            }
            foreach (Blessing blessing in BlessingRegistry.All) {
                if (IsBurning(blessing)) {
                    blessing.GetHealLife(this, item, quickHeal, ref healValue);
                }
            }
        }
    }
}
