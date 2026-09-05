using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 八套木甲的公共层：单件属性沿用原版（本就没有），套装奖励统一为砍树多掉木材
    /// （落地逻辑见 <see cref="GsWoodLumberDrop"/>）
    /// </summary>
    internal abstract class GsWoodArmorScheme : GsResetArmorScheme
    {
        public sealed override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback => "Chopping trees has a chance to drop extra lumber";
    }

    /// <summary>木套</summary>
    internal class GsWoodArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.WoodHelmet];
        public override int BodyID => ItemID.WoodBreastplate;
        public override int LegsID => ItemID.WoodGreaves;
    }

    /// <summary>针叶木套</summary>
    internal class GsBorealWoodArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.BorealWoodHelmet];
        public override int BodyID => ItemID.BorealWoodBreastplate;
        public override int LegsID => ItemID.BorealWoodGreaves;
    }

    /// <summary>红木套</summary>
    internal class GsRichMahoganyArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.RichMahoganyHelmet];
        public override int BodyID => ItemID.RichMahoganyBreastplate;
        public override int LegsID => ItemID.RichMahoganyGreaves;
    }

    /// <summary>棕榈木套</summary>
    internal class GsPalmWoodArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PalmWoodHelmet];
        public override int BodyID => ItemID.PalmWoodBreastplate;
        public override int LegsID => ItemID.PalmWoodGreaves;
    }

    /// <summary>乌木套</summary>
    internal class GsEbonwoodArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.EbonwoodHelmet];
        public override int BodyID => ItemID.EbonwoodBreastplate;
        public override int LegsID => ItemID.EbonwoodGreaves;
    }

    /// <summary>暗影木套</summary>
    internal class GsShadewoodArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShadewoodHelmet];
        public override int BodyID => ItemID.ShadewoodBreastplate;
        public override int LegsID => ItemID.ShadewoodGreaves;
    }

    /// <summary>灰木套</summary>
    internal class GsAshWoodArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.AshWoodHelmet];
        public override int BodyID => ItemID.AshWoodBreastplate;
        public override int LegsID => ItemID.AshWoodGreaves;
    }

    /// <summary>珍珠木套</summary>
    internal class GsPearlwoodArmor : GsWoodArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PearlwoodHelmet];
        public override int BodyID => ItemID.PearlwoodBreastplate;
        public override int LegsID => ItemID.PearlwoodGreaves;
    }

    /// <summary>
    /// 木甲套装奖励的落地面：树干每格掉落结算时（原版物品掉落只在单人/服务端跑），
    /// 找到附近穿着任一木套的玩家，按 50% 概率多掉一份同种木材、5% 概率多掉一枚橡子。
    /// 木材种类按树根下的地面用原版 GetTreeType 判定，与原版掉落同源
    /// </summary>
    internal class GsWoodLumberDrop : GlobalTile
    {
        /// <summary>认领距离（格）：树顶离伐木者可能很远，取宽一点</summary>
        private const float ClaimRangeTiles = 100f;

        public override void Drop(int i, int j, int type) {
            if (!GameModeSystem.GodSmithActive || Main.netMode == NetmodeID.MultiplayerClient
                || type < 0 || type >= TileID.Sets.IsATreeTrunk.Length || !TileID.Sets.IsATreeTrunk[type]) {
                return;
            }
            if (FindWoodWearer(i, j) == null) {
                return;
            }
            int lumber = LumberFor(i, j);
            if (lumber <= 0) {
                return;
            }
            IEntitySource source = new EntitySource_TileBreak(i, j, "GodSmithWoodEndow");
            Rectangle at = new(i * 16, j * 16, 16, 16);
            if (Main.rand.NextBool(2)) {
                Item.NewItem(source, at, lumber);
            }
            if (Main.rand.NextBool(20)) {
                Item.NewItem(source, at, ItemID.Acorn);
            }
        }

        /// <summary>离该格最近、正穿着整套木甲的玩家</summary>
        private static Player FindWoodWearer(int i, int j) {
            Player best = null;
            float bestDist = ClaimRangeTiles * 16f;
            Vector2 tileCenter = new(i * 16f + 8f, j * 16f + 8f);
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.GetModPlayer<GodSmithArmorPlayer>().HasBonus<GsWoodArmorScheme>()) {
                    continue;
                }
                float dist = player.Center.Distance(tileCenter);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = player;
                }
            }
            return best;
        }

        /// <summary>该树干所属树种的木材；无法判定（宝石树等）返回 0</summary>
        private static int LumberFor(int i, int j) {
            WorldGen.GetTreeBottom(i, j, out int groundX, out int groundY);
            if (!WorldGen.InWorld(groundX, groundY, 2)) {
                return 0;
            }
            Tile ground = Main.tile[groundX, groundY];
            if (!ground.HasTile) {
                return 0;
            }
            return WorldGen.GetTreeType(ground.TileType) switch {
                TreeTypes.Forest => ItemID.Wood,
                TreeTypes.Corrupt or TreeTypes.PalmCorrupt => ItemID.Ebonwood,
                TreeTypes.Crimson or TreeTypes.PalmCrimson => ItemID.Shadewood,
                TreeTypes.Hallowed or TreeTypes.PalmHallowed => ItemID.Pearlwood,
                TreeTypes.Jungle => ItemID.RichMahogany,
                TreeTypes.Snow => ItemID.BorealWood,
                TreeTypes.Palm => ItemID.PalmWood,
                TreeTypes.Ash => ItemID.AshWood,
                TreeTypes.Mushroom => ItemID.GlowingMushroom,
                _ => 0,
            };
        }
    }
}
