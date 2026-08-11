using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults
{
    /// <summary>
    /// 鬼伞·湖藏。血湖是施术者私有的异空间储物场：
    /// 沉入当帧物品即入账（演出只是幽灵视觉），提取先入"在途"、凝实拍才交付背包，
    /// 存档时在途物折返湖藏——中途退出不丢不复制。数据只活在所有者本机，同储钱罐语义。
    /// </summary>
    public class KikasaVaultPlayer : ModPlayer, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        /// <summary>湖底容量</summary>
        public const int Capacity = 40;

        /// <summary>沉在湖底的物品，序位即湖窗展示位</summary>
        public List<Item> Stored { get; private set; } = [];

        /// <summary>提取演出在途的物品；交付背包前的暂存，存档时折返湖藏</summary>
        internal readonly List<Item> inFlight = [];

        public static LocalizedText LakeNotReady { get; private set; }
        public static LocalizedText UmbrellaRefuse { get; private set; }
        public static LocalizedText VaultFull { get; private set; }

        public override void SetStaticDefaults() {
            LakeNotReady = this.GetLocalization(nameof(LakeNotReady), () => "湖还没涨到脚边");
            UmbrellaRefuse = this.GetLocalization(nameof(UmbrellaRefuse), () => "伞沉不进自己撑开的湖");
            VaultFull = this.GetLocalization(nameof(VaultFull), () => "湖底沉不下更多了");
        }

        /// <summary>血湖是否可收发物品：本人领域非收合/翻转阶段且水位已及脚</summary>
        public bool LakeReady {
            get {
                KikasaDomainPlayer domain = Player.GetModPlayer<KikasaDomainPlayer>();
                return domain.AnyActive
                    && domain.Phase != KikasaDomainPhase.Closing
                    && domain.Phase != KikasaDomainPhase.Flipping
                    && domain.RiseT >= 0.999f;
            }
        }

        //==================== 输入 ====================

        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.dead) {
                return;
            }
            if (HackTime.Active) {
                return;
            }
            if (CWRKeySystem.Kikasa_Sink.JustPressed) {
                //同一个"沉入"手势：光标指着生物就沉生物，否则沉手中物
                if (!KikasaDrown.TryDrownAtCursor(Player)) {
                    TrySink();
                }
            }
            //湖窗开阖：持鬼伞开；已开着则任意持物都能按键合上（键的单一受理点，避免双触发）
            if (CWRKeySystem.Legend_UIControl.JustPressed) {
                KikasaVaultUI ui = KikasaVaultUI.Instance;
                if (ui == null) {
                    return;
                }
                if (ui.IsOpen) {
                    ui.Close();
                }
                else if (HoldingUmbrella()) {
                    if (LakeReady) {
                        ui.Open();
                    }
                    else {
                        Refuse(LakeNotReady);
                    }
                }
            }
        }

        private bool HoldingUmbrella() {
            Item item = Player.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        //==================== 沉入 ====================

        /// <summary>持物沉湖。数据当帧入账，演出与转播交给幽灵层</summary>
        internal bool TrySink() {
            if (Player.whoAmI != Main.myPlayer) {
                return false;
            }
            Item item = Player.GetItem();
            if (item == null || !item.Alives()) {
                //空手静默，没什么可沉
                return false;
            }
            if (!LakeReady) {
                Refuse(LakeNotReady);
                return false;
            }
            if (item.type == ModContent.ItemType<KikasaItem>()) {
                //鬼伞是重开领域的钥匙，沉进去就捞不回来了
                Refuse(UmbrellaRefuse);
                return false;
            }
            if (Stored.Count >= Capacity) {
                Refuse(VaultFull);
                return false;
            }

            Item stored = item.Clone();
            Stored.Add(stored);
            //GetItem 返回的就是光标物或快捷栏槽位本体，原地清空即可
            item.TurnToAir();

            KikasaLakeFX.SpawnSink(Player, stored);
            KikasaLakeNet.SendFX(Player, KikasaLakeNet.KindSink, stored.type);
            return true;
        }

        //==================== 提取 ====================

        /// <summary>湖窗点击提取：物品当帧离账入"在途"，凝实拍交付</summary>
        internal bool BeginExtract(int index) {
            if (Player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (index < 0 || index >= Stored.Count || !LakeReady) {
                return false;
            }
            Item item = Stored[index];
            Stored.RemoveAt(index);
            if (item == null || item.IsAir) {
                return false;
            }
            inFlight.Add(item);
            KikasaLakeFX.SpawnRaise(Player, item);
            KikasaLakeNet.SendFX(Player, KikasaLakeNet.KindRaise, item.type);
            return true;
        }

        /// <summary>凝实完成拍的交付；领域中途收合时由幽灵层提前调用兜底</summary>
        internal void DeliverExtract(Item item) {
            //已被存档折返或重复回调时这里拦下
            if (item == null || !inFlight.Remove(item)) {
                return;
            }
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f }, Player.Center);
            //入包优先、溢出落地，多人语义由 GiveItem 兜住
            Player.GiveItem(Player.GetSource_Misc("KikasaLakeVault"), item);
        }

        //==================== 反馈与存档 ====================

        private void Refuse(LocalizedText text) {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, Player.Center);
            if (Main.netMode != NetmodeID.Server) {
                CombatText.NewText(Player.Hitbox, new Color(190, 84, 80), text.Value);
            }
        }

        public override void SaveData(TagCompound tag) {
            List<TagCompound> list = [];
            foreach (Item item in Stored) {
                if (item != null && !item.IsAir) {
                    list.Add(ItemIO.Save(item));
                }
            }
            //在途折返：提取演出没走完就存档，物品回湖而不是消失
            foreach (Item item in inFlight) {
                if (item != null && !item.IsAir) {
                    list.Add(ItemIO.Save(item));
                }
            }
            if (list.Count > 0) {
                tag["KikasaVault"] = list;
            }
        }

        public override void LoadData(TagCompound tag) {
            Stored.Clear();
            inFlight.Clear();
            if (!tag.TryGet("KikasaVault", out List<TagCompound> list)) {
                return;
            }
            foreach (TagCompound itemTag in list) {
                try {
                    Item item = ItemIO.Load(itemTag);
                    if (item != null && !item.IsAir) {
                        Stored.Add(item);
                    }
                } catch {
                    //单件读损跳过，别拖垮整湖
                }
            }
        }
    }
}
