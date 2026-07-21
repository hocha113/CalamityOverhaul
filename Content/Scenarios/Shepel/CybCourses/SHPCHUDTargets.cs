using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //SHPC HUD教程目标，对齐SHPCTheme
    internal class SHPCHUDTargets : ILoadable
    {
        private const int SectorCount = 4;

        public void Load(Mod mod) {
            CybTutorialRegistry.Register(new CoreTarget());
            for (int i = 0; i < SectorCount; i++)
                CybTutorialRegistry.Register(new SectorTarget(i));
        }

        public void Unload() => CybTutorialRegistry.Clear();

        //对齐SHPCUI.CorePos
        internal static Vector2 CorePos => new(96f, Main.screenHeight - 96f);

        //复现SHPCUI.GetSectorAngles(私有)
        internal static void GetSectorAngles(int idx, out float aStart, out float aEnd) {
            float total = SHPCTheme.FanEnd - SHPCTheme.FanStart;
            float gap = SHPCTheme.ButtonGap;
            float perAngle = (total - gap * (SectorCount - 1)) / SectorCount;
            aStart = SHPCTheme.FanStart + idx * (perAngle + gap);
            aEnd = aStart + perAngle;
        }

        private sealed class CoreTarget : ICybTutorialTarget
        {
            public string Key => "SHPC.Core";
            public bool IsAvailable => SHPCUI.Instance?.Active == true;
            public Rectangle GetScreenRect() {
                Vector2 c = CorePos;
                int r = (int)(SHPCTheme.CoreRingR + 10f);
                return new Rectangle((int)(c.X - r), (int)(c.Y - r), r * 2, r * 2);
            }
        }

        private sealed class SectorTarget : ICybTutorialTarget
        {
            private readonly int _idx;
            public SectorTarget(int idx) => _idx = idx;
            public string Key => $"SHPC.Sector.{_idx}";
            public bool IsAvailable => SHPCUI.Instance?.Active == true;
            public Rectangle GetScreenRect() {
                Vector2 c = CorePos;
                GetSectorAngles(_idx, out float a0, out float a1);
                float midA = (a0 + a1) * 0.5f;
                float midR = (SHPCTheme.ButtonInnerR + SHPCTheme.ButtonOuterR) * 0.5f;
                Vector2 center = c + new Vector2(MathF.Cos(midA), MathF.Sin(midA)) * midR;
                float hs = (SHPCTheme.ButtonOuterR - SHPCTheme.ButtonInnerR) * 0.5f + 8f;
                return new Rectangle(
                    (int)(center.X - hs), (int)(center.Y - hs),
                    (int)(hs * 2), (int)(hs * 2));
            }
        }
    }
}
