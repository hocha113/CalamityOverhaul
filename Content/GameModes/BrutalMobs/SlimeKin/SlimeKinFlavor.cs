using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin
{
    /// <summary>黏化场地的风味族：同一机制家族，按史莱姆变体换表现与减益</summary>
    internal enum GooFlavor
    {
        /// <summary>通用黏浆：踩入缓速</summary>
        Base = 0,
        /// <summary>熔岩：踩入灼烧</summary>
        Molten = 1,
        /// <summary>冰面：踩入打滑（纯物理，无减益）</summary>
        Slick = 2,
        /// <summary>毒泥：毒云区中毒</summary>
        Toxic = 3,
    }

    /// <summary>史莱姆族机制的风味映射与小工具，全部为确定性纯函数（联机各端一致）</summary>
    internal static class SlimeKinFlavor
    {
        /// <summary>默认凝胶蓝（无 npc.color 信息时的回退）</summary>
        public static readonly Color GelBlue = new Color(88, 148, 255);
        /// <summary>熔岩橙</summary>
        public static readonly Color MoltenOrange = new Color(255, 138, 48);
        /// <summary>冰晶青</summary>
        public static readonly Color IceCyan = new Color(148, 224, 255);
        /// <summary>毒泥绿</summary>
        public static readonly Color ToxicGreen = new Color(158, 218, 74);

        /// <summary>按类型定风味（Lava/Ice/Toxic 是独立正类型，无需 netID）</summary>
        public static GooFlavor FlavorOf(NPC npc) {
            if (npc.type == NPCID.LavaSlime) {
                return GooFlavor.Molten;
            }
            if (npc.type == NPCID.IceSlime || npc.type == NPCID.SpikedIceSlime) {
                return GooFlavor.Slick;
            }
            if (npc.type == NPCID.ToxicSludge) {
                return GooFlavor.Toxic;
            }
            return GooFlavor.Base;
        }

        /// <summary>
        /// 个体凝胶主色：风味类型用固定色；蓝史莱姆变体（绿/红/紫/黄/黑等）取 npc.color 的 RGB；
        /// 其余深色或无色个体回退凝胶蓝
        /// </summary>
        public static Color GelColor(NPC npc) {
            switch (npc.type) {
                case NPCID.LavaSlime:
                    return MoltenOrange;
                case NPCID.IceSlime:
                case NPCID.SpikedIceSlime:
                    return IceCyan;
                case NPCID.ToxicSludge:
                case NPCID.SpikedJungleSlime:
                    return ToxicGreen;
                case NPCID.CorruptSlime:
                case NPCID.Slimer:
                    return new Color(154, 108, 220);
                case NPCID.Crimslime:
                    return new Color(228, 92, 92);
                case NPCID.IlluminantSlime:
                    return new Color(255, 128, 224);
                case NPCID.SandSlime:
                    return new Color(216, 186, 118);
                case NPCID.MotherSlime:
                    return new Color(110, 120, 214);
            }
            //蓝史莱姆宿主类型：变体色在 npc.color（半透明 alpha，只取 RGB）
            if (npc.color.R + npc.color.G + npc.color.B > 36) {
                return new Color(npc.color.R, npc.color.G, npc.color.B);
            }
            return GelBlue;
        }

        /// <summary>RGB 压进一个 float（24bit 内 float 精确表示，走 ai 槽跨端传色）</summary>
        public static float PackColor(Color color) => (color.R << 16) | (color.G << 8) | color.B;

        /// <summary>从 ai 槽还原 RGB；非法值回退凝胶蓝</summary>
        public static Color UnpackColor(float packed) {
            int v = (int)packed;
            if (v <= 0 || v > 0xFFFFFF) {
                return GelBlue;
            }
            return new Color((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
        }

        /// <summary>弹跳凝胶爆裂命中的风味减益（原版 BuffID，帧数）</summary>
        public static (int buffId, int frames) BurstDebuff(GooFlavor flavor) {
            return flavor switch {
                GooFlavor.Molten => (BuffID.OnFire, 180),
                GooFlavor.Slick => (BuffID.Chilled, 120),
                GooFlavor.Toxic => (BuffID.Poisoned, 300),
                _ => (BuffID.Slimed, 240),
            };
        }

        /// <summary>从 start 向下找地表（最多 maxTiles 格），找不到返回原点</summary>
        public static Vector2 FindGroundBelow(Vector2 start, int maxTiles) {
            int tileX = (int)(start.X / 16f);
            int tileY = (int)(start.Y / 16f);
            for (int y = tileY; y < tileY + maxTiles && y < Main.maxTilesY - 10; y++) {
                if (WorldGen.SolidTile(tileX, y)) {
                    return new Vector2(start.X, y * 16f);
                }
            }
            return start;
        }
    }
}
