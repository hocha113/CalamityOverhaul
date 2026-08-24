using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 单符会话计量仓：字段语义由各符自定（节拍连击/充能/月相/祭值/冷却……），
    /// 不落盘、不联机同步；各端各自持有，owner 端为权威读数，旁观端仅供近似演出
    /// </summary>
    internal sealed class KikasaTalismanSessionState
    {
        public float MeterA;
        public float MeterB;
        public int CounterA;
        public int CounterB;
        public int TimerA;
        public int TimerB;

        public void Reset() {
            MeterA = MeterB = 0f;
            CounterA = CounterB = 0;
            TimerA = TimerB = 0;
        }
    }

    /// <summary>
    /// 唤雨符玩家侧宿主：符箧（已录入 Key 集合）随玩家存档；
    /// 会话钩子驱动 <see cref="KikasaTalismanNet"/> 的挂起清理与权威回填；
    /// 另承载按符 Key 索引的会话计量仓与持伞/受击挂钩派发
    /// </summary>
    internal class KikasaTalismanPlayer : ModPlayer
    {
        /// <summary>已录入符箧的 Key 集合；读取过 <see cref="KikasaTalismanOwned"/> 门闩</summary>
        internal HashSet<string> OwnedTalismanKeys = [];

        //会话计量：懒建（多数玩家从不挂符），进世界清零
        private Dictionary<string, KikasaTalismanSessionState> talismanSessions;

        /// <summary>取该符的会话计量仓，首次访问即建</summary>
        internal KikasaTalismanSessionState GetTalismanState(string key) {
            talismanSessions ??= [];
            if (!talismanSessions.TryGetValue(key, out KikasaTalismanSessionState state)) {
                state = new KikasaTalismanSessionState();
                talismanSessions.Add(key, state);
            }
            return state;
        }

        public override void SaveData(TagCompound tag) {
            KikasaTalismanOwned.EnsureInit(this);
            List<string> keys = OwnedTalismanKeys
                .Where(key => KikasaTalismanRegistry.TryGet(key, out _)).ToList();
            if (keys.Count > 0) {
                tag["KikasaTalismanOwned"] = keys;
            }
        }

        public override void LoadData(TagCompound tag) {
            OwnedTalismanKeys = [];
            if (tag.TryGet("KikasaTalismanOwned", out List<string> keys) && keys != null) {
                foreach (string key in keys) {
                    if (!string.IsNullOrEmpty(key)) {
                        OwnedTalismanKeys.Add(key);
                    }
                }
            }
        }

        public override void OnEnterWorld() {
            KikasaTalismanNet.ResetPlayerSession(Player);
            KikasaTalismanNet.RepairDuplicateIdentities(Player);
            KikasaTalismanOwned.EnsureInit(this);
            KikasaTalismanNet.SendOwnedSnapshot(Player);
            //会话计量随会话走，进世界一律清零
            talismanSessions?.Clear();
        }

        public override void PreUpdate() {
            KikasaTalismanNet.RepairDuplicateIdentities(Player);
            KikasaTalismanNet.UpdatePending(Player);
            KikasaTalismanNet.ReconcileAuthoritativeState(Player);
            //持伞逐帧挂钩：物品类型先挡一刀，空绳在派发器内短路。
            //PreUpdate 在死亡分支之前运行，倒地期间计时器照常走
            if (Player.HeldItem?.type == ModContent.ItemType<KikasaItem>()) {
                KikasaTalismanHooks.For(Player).UpdateWhileHeld();
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            //受击挂钩（澍「及时雨」窗口等）：仅持伞时派发
            if (Player.HeldItem?.type == ModContent.ItemType<KikasaItem>()) {
                KikasaTalismanHooks.For(Player).OnOwnerHurt(in info);
            }
        }
    }
}
