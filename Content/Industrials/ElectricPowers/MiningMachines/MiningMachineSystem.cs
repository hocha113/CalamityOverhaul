using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>
    /// 采矿机掉落上下文
    /// </summary>
    public struct MiningDropContext
    {
        public int Tier;
        public Point Position;
        public readonly bool IsSpace => Position.Y < Main.worldSurface * 0.35f;
        public readonly bool IsSurface => Position.Y < Main.worldSurface;
        public readonly bool IsUnderground => Position.Y >= Main.worldSurface && Position.Y < Main.rockLayer;
        public readonly bool IsCavern => Position.Y >= Main.rockLayer && Position.Y < Main.maxTilesY - 200;
        public readonly bool IsUnderworld => Position.Y >= Main.maxTilesY - 600;
        public readonly bool IsJungle => CheckTile(TileID.JungleGrass) || CheckTile(TileID.LihzahrdBrick) || CheckTile(TileID.Hive);
        public readonly bool IsSnow => CheckTile(TileID.SnowBlock) || CheckTile(TileID.IceBlock);
        public readonly bool IsDesert => CheckTile(TileID.Sand) || CheckTile(TileID.Ebonsand) || CheckTile(TileID.Crimsand) || CheckTile(TileID.Pearlsand);
        public readonly bool IsCorruption => CheckTile(TileID.Ebonstone) || CheckTile(TileID.Ebonsand);
        public readonly bool IsCrimson => CheckTile(TileID.Crimstone) || CheckTile(TileID.Crimsand);
        public readonly bool IsHallow => CheckTile(TileID.Pearlstone) || CheckTile(TileID.Pearlsand);
        private readonly bool CheckTile(int type) {
            //检查机器下方中心位置的方块
            Tile tile = Main.tile[Position.X + 1, Position.Y + 3];
            return tile.HasTile && tile.TileType == type;
        }
    }

    /// <summary>
    /// 矿物掉落规则
    /// </summary>
    public class OreDropRule
    {
        public int ItemID { get; set; }
        public float BaseChance { get; set; }
        public int MinTier { get; set; }
        public Func<MiningDropContext, bool> Condition { get; set; }
        public Func<MiningDropContext, float, float> ChanceModifier { get; set; }

        public OreDropRule(int itemID, float baseChance, int minTier = 1) {
            ItemID = itemID;
            BaseChance = baseChance;
            MinTier = minTier;
        }

        public OreDropRule SetCondition(Func<MiningDropContext, bool> condition) {
            Condition = condition;
            return this;
        }

        public OreDropRule SetChanceModifier(Func<MiningDropContext, float, float> modifier) {
            ChanceModifier = modifier;
            return this;
        }

        public bool CanDrop(MiningDropContext context) {
            if (context.Tier < MinTier) return false;
            if (Condition != null && !Condition(context)) return false;
            return true;
        }

        public float GetChance(MiningDropContext context) {
            float chance = BaseChance;
            if (ChanceModifier != null) {
                chance = ChanceModifier(context, chance);
            }
            //默认随等级提升概率
            chance *= (1f + (context.Tier - 1) * 0.2f);
            return chance;
        }
    }

    public class MiningMachineSystem : ModSystem
    {
        internal static List<OreDropRule> DropRules = new();

        public override void OnModLoad() {
            DropRules = [];
            LoadStandardOres();
            LoadSpecialOres();
        }

        public override void Unload() {
            DropRules = null;
        }

        private static void LoadStandardOres() {
            //基础矿物
            Register(new OreDropRule(ItemID.CopperOre, 0.1f));
            Register(new OreDropRule(ItemID.TinOre, 0.1f));
            Register(new OreDropRule(ItemID.IronOre, 0.1f));
            Register(new OreDropRule(ItemID.LeadOre, 0.1f));
            Register(new OreDropRule(ItemID.SilverOre, 0.09f));
            Register(new OreDropRule(ItemID.TungstenOre, 0.09f));
            Register(new OreDropRule(ItemID.GoldOre, 0.08f));
            Register(new OreDropRule(ItemID.PlatinumOre, 0.08f));
            Register(new OreDropRule(ItemID.Coal, 0.1f));

            //腐化/猩红矿物
            Register(new OreDropRule(ItemID.DemoniteOre, 0.07f).SetCondition(ctx => !ctx.IsCrimson));
            Register(new OreDropRule(ItemID.CrimtaneOre, 0.07f).SetCondition(ctx => !ctx.IsCorruption));

            //陨石 (需要2级)
            Register(new OreDropRule(ItemID.Meteorite, 0.05f, minTier: 2));

            //黑曜石 (需要2级)
            Register(new OreDropRule(ItemID.Obsidian, 0.05f, minTier: 2));

            //狱岩石 (需要2级，仅在地狱)
            Register(new OreDropRule(ItemID.Hellstone, 0.05f, minTier: 2)
                .SetCondition(ctx => ctx.IsUnderworld));

            //困难模式矿物 (需要2级，且在困难模式)
            Register(new OreDropRule(ItemID.CobaltOre, 0.04f, minTier: 2).SetCondition(ctx => Main.hardMode));
            Register(new OreDropRule(ItemID.PalladiumOre, 0.04f, minTier: 2).SetCondition(ctx => Main.hardMode));
            Register(new OreDropRule(ItemID.MythrilOre, 0.03f, minTier: 2).SetCondition(ctx => Main.hardMode));
            Register(new OreDropRule(ItemID.OrichalcumOre, 0.03f, minTier: 2).SetCondition(ctx => Main.hardMode));
            Register(new OreDropRule(ItemID.AdamantiteOre, 0.02f, minTier: 2).SetCondition(ctx => Main.hardMode));
            Register(new OreDropRule(ItemID.TitaniumOre, 0.02f, minTier: 2).SetCondition(ctx => Main.hardMode));

            //环境特色矿物
            //丛林 -> 叶绿矿 (需要2级，困难模式，且在丛林)
            Register(new OreDropRule(ItemID.ChlorophyteOre, 0.02f, minTier: 2)
                .SetCondition(ctx => Main.hardMode && ctx.IsJungle && (ctx.IsUnderground || ctx.IsCavern)));

            //沙漠 -> 化石 (需要1级，且在沙漠)
            Register(new OreDropRule(ItemID.FossilOre, 0.05f, minTier: 1)
                .SetCondition(ctx => ctx.IsDesert && (ctx.IsUnderground || ctx.IsCavern)));
        }

        private static void LoadSpecialOres() {
            if (CWRRef.Has) {
                Register(new OreDropRule(CWRID.Item_DubiousPlating, 0.05f, minTier: 1));
                Register(new OreDropRule(CWRID.Item_MysteriousCircuitry, 0.05f, minTier: 1));
            }
        }

        public static void Register(OreDropRule rule) => DropRules.Add(rule);

        public static bool TryGetDropItem(int tier, Point position, out int itemID) {
            itemID = 0;
            var context = new MiningDropContext {
                Tier = tier,
                Position = position
            };

            foreach (var rule in DropRules) {
                if (rule.CanDrop(context)) {
                    if (Main.rand.NextFloat() < rule.GetChance(context)) {
                        itemID = rule.ItemID;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
