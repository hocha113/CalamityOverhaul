using CalamityMod.NPCs.CalClone;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using System.IO;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class SupCalDefeat : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalDefeat);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }
        public static LocalizedText Line10 { get; private set; }

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "至尊灾厄");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "[Name] & 比目鱼");
            Line1 = this.GetLocalization(nameof(Line1), () => "你竟然已经到达这种地步了吗......呵，是我技不如人了");
            Line2 = this.GetLocalization(nameof(Line2), () => "但你并非最强，你或许很不错，但那个人绝对不会比你差");
            Line3 = this.GetLocalization(nameof(Line3), () => "亚利姆已经走到了那条道路的尽头，到达了泰拉人的极致，没人会比他强");
            Line4 = this.GetLocalization(nameof(Line4), () => "可惜，你们不能见面......");
            Line5 = this.GetLocalization(nameof(Line5), () => "你的层次太低，永远无法理解我现在的状态");
            Line6 = this.GetLocalization(nameof(Line6), () => "......我层次太低?");
            Line7 = this.GetLocalization(nameof(Line7), () => "(活这么多年，还是第一次被说层次太低)");
            Line8 = this.GetLocalization(nameof(Line8), () => "我是一个时代孕育出来的唯一，既然敢舍弃泰拉人的身份，自封为神，自然是无所不能");
            Line9 = this.GetLocalization(nameof(Line9), () => "你说亚利姆可以称量我?得让克洛希克来");
            Line10 = this.GetLocalization(nameof(Line10), () => "......");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            string hapName = Rolename2.Value.Replace("[Name]", Main.LocalPlayer.name);
            Add(Rolename1.Value, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            Add(Rolename1.Value, Line4.Value);
            Add(hapName, Line5.Value);
            Add(Rolename1.Value, Line6.Value);
            Add(Rolename1.Value, Line7.Value);
            Add(hapName, Line8.Value);
            Add(hapName, Line9.Value);
            Add(Rolename1.Value, Line10.Value);
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.SupCalDefeat) {
                return;
            }
            if (!halibutPlayer.HeldHalibut) {
                return;//必须持有比目鱼才能触发
            }
            if (halibutPlayer.SeaDomainLayers < 10) {
                return;//必须海域层数达到10层才能触发
            }
            if (!SupCalDefeatNPC.Spawned) {
                return;
            }
            if (--SupCalDefeatNPC.RandomTimer > 0) {
                return;
            }
            if (ScenarioManager.Start<SupCalDefeat>()) {
                save.SupCalDefeat = true;
                SupCalDefeatNPC.Spawned = false;
            }
        }
    }

    internal class SupCalDefeatNPC : GlobalNPC
    {
        public static bool Spawned = false;
        public static int RandomTimer;
        public override void OnKill(NPC npc) {
            if (npc.type == ModContent.NPCType<SupremeCalamitas>() && Main.LocalPlayer.GetItem().type == HalibutOverride.ID) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);//给一个3到5秒的缓冲时间，打完立刻触发不太合适
            }

            //仅服务器发送
            if (VaultUtils.isServer) {
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.SupCalDefeatNPC);
                packet.Send();
            }
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (!VaultUtils.isClient) {
                return;//仅客户端处理
            }
            if (type == CWRMessageType.SupCalDefeatNPC) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);//给一个3到5秒的缓冲时间，打完立刻触发不太合适
            }
        }
    }
}
