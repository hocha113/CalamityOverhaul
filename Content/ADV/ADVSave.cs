using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    internal class ADVSave
    {
        public bool FirstMet;
        public bool FirstResurrectionWarning;
        public bool QueenBeeGift;
        public bool SkeletronGift;
        public bool EyeOfCthulhuGift;
        public bool KingSlimeGift;
        public bool CrabulonGift;
        public bool PerforatorGift;
        public bool HiveMindGift;
        public bool WallOfFleshGift;
        public bool SlimeGodGift;
        public bool CryogenGift;
        public bool BrimstoneElementalGift;
        public bool AquaticScourgeGift;
        public bool CalamitasCloneGift;
        public bool PlanteraGift;
        public bool GolemGift;
        public bool MoonLordGift;
        public bool LeviathanGift;
        public bool PlaguebringerGift;
        public bool ProvidenceGift;
        public bool DevourerOfGodsGift;
        public bool YharonGift;
        public bool SupremeCalamitasGift;
        public bool FirstMetSupCal;
        public bool SupCalChoseToFight;
        public bool SupCalMoonLordReward;
        public bool SupCalDefeat;
        //任务相关字段
        public bool SupCalQuestAccepted;//玩家是否接受了任务
        public bool SupCalQuestDeclined;//玩家是否拒绝了任务
        public bool SupCalQuestReward;//玩家是否完成了任务（击杀了Providence）
        public bool SupCalQuestRewardSceneComplete;//任务完成后的奖励场景是否已播放

        public virtual TagCompound SaveData() {
            TagCompound tag = new TagCompound {
                ["FirstMet"] = FirstMet,
                ["FirstResurrectionWarning"] = FirstResurrectionWarning,
                ["QueenBeeGift"] = QueenBeeGift,
                ["SkeletronGift"] = SkeletronGift,
                ["EyeOfCthulhuGift"] = EyeOfCthulhuGift,
                ["KingSlimeGift"] = KingSlimeGift,
                ["CrabulonGift"] = CrabulonGift,
                ["PerforatorGift"] = PerforatorGift,
                ["HiveMindGift"] = HiveMindGift,
                ["WallOfFleshGift"] = WallOfFleshGift,
                ["SlimeGodGift"] = SlimeGodGift,
                ["CryogenGift"] = CryogenGift,
                ["BrimstoneElementalGift"] = BrimstoneElementalGift,
                ["AquaticScourgeGift"] = AquaticScourgeGift,
                ["CalamitasCloneGift"] = CalamitasCloneGift,
                ["PlanteraGift"] = PlanteraGift,
                ["GolemGift"] = GolemGift,
                ["MoonLordGift"] = MoonLordGift,
                ["LeviathanGift"] = LeviathanGift,
                ["PlaguebringerGift"] = PlaguebringerGift,
                ["ProvidenceGift"] = ProvidenceGift,
                ["DevourerOfGodsGift"] = DevourerOfGodsGift,
                ["YharonGift"] = YharonGift,
                ["SupremeCalamitasGift"] = SupremeCalamitasGift,
                ["FirstMetSupCal"] = FirstMetSupCal,
                ["SupCalChoseToFight"] = SupCalChoseToFight,
                ["SupCalMoonLordReward"] = SupCalMoonLordReward,
                ["SupCalDefeat"] = SupCalDefeat,
                ["SupCalQuestAccepted"] = SupCalQuestAccepted,
                ["SupCalQuestDeclined"] = SupCalQuestDeclined,
                ["SupCalQuestReward"] = SupCalQuestReward,
                ["SupCalQuestRewardSceneComplete"] = SupCalQuestRewardSceneComplete,
            };
            return tag;
        }

        public virtual void LoadData(TagCompound tag) {
            if (tag.TryGet("FirstMet", out bool firstMet)) {
                FirstMet = firstMet;
            }
            if (tag.TryGet("FirstResurrectionWarning", out bool firstResurrectionWarning)) {
                FirstResurrectionWarning = firstResurrectionWarning;
            }
            if (tag.TryGet("QueenBeeGift", out bool q)) {
                QueenBeeGift = q;
            }
            if (tag.TryGet("SkeletronGift", out bool s)) {
                SkeletronGift = s;
            }
            if (tag.TryGet("EyeOfCthulhuGift", out bool e)) {
                EyeOfCthulhuGift = e;
            }
            if (tag.TryGet("KingSlimeGift", out bool ks)) {
                KingSlimeGift = ks;
            }
            if (tag.TryGet("CrabulonGift", out bool c)) {
                CrabulonGift = c;
            }
            if (tag.TryGet("PerforatorGift", out bool p)) {
                PerforatorGift = p;
            }
            if (tag.TryGet("HiveMindGift", out bool h)) {
                HiveMindGift = h;
            }
            if (tag.TryGet("WallOfFleshGift", out bool w)) {
                WallOfFleshGift = w;
            }
            if (tag.TryGet("SlimeGodGift", out bool sg)) {
                SlimeGodGift = sg;
            }
            if (tag.TryGet("CryogenGift", out bool cr)) {
                CryogenGift = cr;
            }
            if (tag.TryGet("BrimstoneElementalGift", out bool be)) {
                BrimstoneElementalGift = be;
            }
            if (tag.TryGet("AquaticScourgeGift", out bool as_)) {
                AquaticScourgeGift = as_;
            }
            if (tag.TryGet("CalamitasCloneGift", out bool cc)) {
                CalamitasCloneGift = cc;
            }
            if (tag.TryGet("PlanteraGift", out bool pl)) {
                PlanteraGift = pl;
            }
            if (tag.TryGet("GolemGift", out bool go)) {
                GolemGift = go;
            }
            if (tag.TryGet("MoonLordGift", out bool ml)) {
                MoonLordGift = ml;
            }
            if (tag.TryGet("LeviathanGift", out bool lv)) {
                LeviathanGift = lv;
            }
            if (tag.TryGet("PlaguebringerGift", out bool pb)) {
                PlaguebringerGift = pb;
            }
            if (tag.TryGet("ProvidenceGift", out bool pr)) {
                ProvidenceGift = pr;
            }
            if (tag.TryGet("DevourerOfGodsGift", out bool dog)) {
                DevourerOfGodsGift = dog;
            }
            if (tag.TryGet("YharonGift", out bool yh)) {
                YharonGift = yh;
            }
            if (tag.TryGet("SupremeCalamitasGift", out bool sc)) {
                SupremeCalamitasGift = sc;
            }
            if (tag.TryGet("FirstMetSupCal", out bool fc)) {
                FirstMetSupCal = fc;
            }
            if (tag.TryGet("SupCalChoseToFight", out bool scf)) {
                SupCalChoseToFight = scf;
            }
            if (tag.TryGet("SupCalMoonLordReward", out bool scmr)) {
                SupCalMoonLordReward = scmr;
            }
            if (tag.TryGet("SupCalDefeat", out bool scd)) {
                SupCalDefeat = scd;
            }
            if (tag.TryGet("SupCalQuestAccepted", out bool scqa)) {
                SupCalQuestAccepted = scqa;
            }
            if (tag.TryGet("SupCalQuestDeclined", out bool scqd)) {
                SupCalQuestDeclined = scqd;
            }
            if (tag.TryGet("SupCalQuestReward", out bool scqr)) {
                SupCalQuestReward = scqr;
            }
            if (tag.TryGet("SupCalQuestRewardSceneComplete", out bool scqrc)) {
                SupCalQuestRewardSceneComplete = scqrc;
            }
        }
    }
}
