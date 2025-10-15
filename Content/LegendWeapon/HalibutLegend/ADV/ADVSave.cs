using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    internal class ADVSave
    {
        public bool FirstMet;
        public bool QueenBeeGift;
        public bool SkeletronGift;
        public bool EyeOfCthulhuGift;
        public bool KingSlimeGift;
        public bool CrabulonGift;
        public bool PerforatorGift;
        public bool HiveMindGift;
        public bool WallOfFleshGift;

        public virtual TagCompound SaveData() {
            TagCompound tag = new TagCompound {
                ["FirstMet"] = FirstMet,
                ["QueenBeeGift"] = QueenBeeGift,
                ["SkeletronGift"] = SkeletronGift,
                ["EyeOfCthulhuGift"] = EyeOfCthulhuGift,
                ["KingSlimeGift"] = KingSlimeGift,
                ["CrabulonGift"] = CrabulonGift,
                ["PerforatorGift"] = PerforatorGift,
                ["HiveMindGift"] = HiveMindGift,
                ["WallOfFleshGift"] = WallOfFleshGift,
            };
            return tag;
        }

        public virtual void LoadData(TagCompound tag) {
            if (tag.TryGet("FirstMet", out bool firstMet)) {
                FirstMet = firstMet;
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
        }
    }
}
