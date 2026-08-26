using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
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
    /// 唤雨符玩家侧宿主：符箧（已录入 Key 集合）与祈雨绳符位表都随玩家存档。<br/>
    /// 符位表 2026-08 自物品侧迁入（他模 SetDefaults 重造物品会清光物品级数据），
    /// 语义为一名玩家一套祈雨绳配置，不随单把伞走；owner 本机写入，
    /// 经 <see cref="KikasaTalismanNet"/> 快照广播供旁观端演出。<br/>
    /// 另承载按符 Key 索引的会话计量仓与持伞/受击挂钩派发
    /// </summary>
    internal class KikasaTalismanPlayer : ModPlayer
    {
        /// <summary>已录入符箧的 Key 集合；owner 本机数据（校验与展示只发生在本机），无需同步</summary>
        internal HashSet<string> OwnedTalismanKeys = [];

        /// <summary>
        /// 祈雨绳符位表，玩家侧唯一真相（绳上挂了哪些符、顺序）。
        /// 写入走 <see cref="KikasaTalismanRegistry"/> 的挂/摘/换入口，随本类存档
        /// </summary>
        internal KikasaTalismanStore Talismans { get; } = new();

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
            //符位表沿用 KikasaFu 前缀键（存入本 ModPlayer 的 tag，与旧物品 tag 互不相干）
            Talismans.SaveData(tag);
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
            Talismans.LoadData(tag);
        }

        public override void OnEnterWorld() {
            KikasaTalismanOwned.EnsureInit(this);
            AdoptLegacyItemTalismans();
            KikasaTalismanNet.SendRopeSnapshot(Player);
            //会话计量随会话走，进世界一律清零
            talismanSessions?.Clear();
        }

        /// <summary>联机同步入口：入场时本机把符位表推给服务器，服务器把存量转播给晚入场者</summary>
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            KikasaTalismanNet.SendRopeSnapshot(Player, toWho, fromWho);
        }

        public override void PreUpdate() {
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

        //==================== 老档折算（一次性收编） ====================

        //符位表迁玩家侧前挂在伞的物品数据上。进世界时玩家侧空表而伞上带旧表 → 收编一次；
        //多把伞都带时取修订号最新的，平手取先扫到的（背包序在前、湖藏序在后）。
        //收编后物品侧旧键不再写档（KikasaData 已停写），随下一次存档自然消失，
        //故不会重复收编：本会话由"玩家侧非空"挡住，下个会话遗产键已死
        private void AdoptLegacyItemTalismans() {
            if (Player.whoAmI != Main.myPlayer || Talismans.HungCount > 0) {
                return;
            }
            KikasaTalismanStore best = null;
            uint bestRevision = 0;
            void Consider(Item item) {
                KikasaData data = KikasaData.TryGet(item);
                if (data?.LegacyTalismans == null) {
                    return;
                }
                if (best == null || data.LegacyEditRevision > bestRevision) {
                    best = data.LegacyTalismans;
                    bestRevision = data.LegacyEditRevision;
                }
            }
            foreach (Item item in Player.inventory) {
                Consider(item);
            }
            foreach (Item item in Player.GetModPlayer<KikasaVaultPlayer>().Stored) {
                Consider(item);
            }
            if (best != null) {
                Talismans.CopyFrom(best);
                CWRMod.Instance.Logger.Info(
                    "[KikasaTalisman] legacy item-side rope adopted into player storage");
            }
        }
    }
}
