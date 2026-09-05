using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 渔夫套：单件沿用原版（各 +5% 渔力）；套装奖励为每次抛竿多甩三根鱼线，
    /// 并有机会钓出不属于当前环境的鱼，或一次钓上 3 到 7 瓶增益药水
    /// </summary>
    internal class GsAnglerArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.AnglerHat];
        public override int BodyID => ItemID.AnglerVest;
        public override int LegsID => ItemID.AnglerPants;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Casts 3 extra fishing lines; chance to catch fish from other biomes or a bundle of 3 to 7 buff potions";

        /// <summary>额外鱼线数</summary>
        private const int ExtraLines = 3;

        /// <summary>异地鱼替换概率（百分比）</summary>
        private const int ForeignFishChance = 15;

        /// <summary>药水束替换概率（百分比）</summary>
        private const int PotionChance = 10;

        /// <summary>环境鱼表：Here 判断该鱼是否属于玩家当前所在环境</summary>
        private readonly record struct BiomeFish(int Type, Func<Player, FishingAttempt, bool> Here);

        private static readonly BiomeFish[] Fishes = [
            new BiomeFish(ItemID.AtlanticCod, (p, a) => p.ZoneSnow),
            new BiomeFish(ItemID.FrostMinnow, (p, a) => p.ZoneSnow),
            new BiomeFish(ItemID.NeonTetra, (p, a) => p.ZoneJungle),
            new BiomeFish(ItemID.VariegatedLardfish, (p, a) => p.ZoneJungle),
            new BiomeFish(ItemID.DoubleCod, (p, a) => p.ZoneJungle),
            new BiomeFish(ItemID.Ebonkoi, (p, a) => p.ZoneCorrupt),
            new BiomeFish(ItemID.Hemopiranha, (p, a) => p.ZoneCrimson),
            new BiomeFish(ItemID.CrimsonTigerfish, (p, a) => p.ZoneCrimson),
            new BiomeFish(ItemID.PrincessFish, (p, a) => p.ZoneHallow),
            new BiomeFish(ItemID.Prismite, (p, a) => p.ZoneHallow),
            new BiomeFish(ItemID.ChaosFish, (p, a) => p.ZoneHallow && a.heightLevel >= 2),
            new BiomeFish(ItemID.Tuna, (p, a) => p.ZoneBeach),
            new BiomeFish(ItemID.RedSnapper, (p, a) => p.ZoneBeach),
            new BiomeFish(ItemID.Shrimp, (p, a) => p.ZoneBeach),
            new BiomeFish(ItemID.Trout, (p, a) => p.ZoneBeach),
            new BiomeFish(ItemID.Damselfish, (p, a) => a.heightLevel == 0),
            new BiomeFish(ItemID.ArmoredCavefish, (p, a) => a.heightLevel >= 2),
            new BiomeFish(ItemID.SpecularFish, (p, a) => a.heightLevel >= 2),
            new BiomeFish(ItemID.Stinkfish, (p, a) => a.heightLevel >= 2),
            new BiomeFish(ItemID.Honeyfin, (p, a) => a.inHoney),
            new BiomeFish(ItemID.FlarefinKoi, (p, a) => a.inLava),
            new BiomeFish(ItemID.Obsidifish, (p, a) => a.inLava),
            new BiomeFish(ItemID.Flounder, (p, a) => p.ZoneDesert),
            new BiomeFish(ItemID.RockLobster, (p, a) => p.ZoneDesert),
            new BiomeFish(ItemID.Salmon, (p, a) => a.heightLevel == 1),
        ];

        private static readonly int[] BuffPotions = [
            ItemID.IronskinPotion, ItemID.RegenerationPotion, ItemID.SwiftnessPotion, ItemID.GillsPotion,
            ItemID.MiningPotion, ItemID.ShinePotion, ItemID.NightOwlPotion, ItemID.BattlePotion,
            ItemID.ThornsPotion, ItemID.WaterWalkingPotion, ItemID.ArcheryPotion, ItemID.HunterPotion,
            ItemID.GravitationPotion, ItemID.FeatherfallPotion, ItemID.SpelunkerPotion, ItemID.InvisibilityPotion,
            ItemID.ObsidianSkinPotion, ItemID.MagicPowerPotion, ItemID.ManaRegenerationPotion, ItemID.TitanPotion,
            ItemID.FlipperPotion, ItemID.SummoningPotion, ItemID.RagePotion, ItemID.WrathPotion,
            ItemID.InfernoPotion, ItemID.EndurancePotion, ItemID.LifeforcePotion, ItemID.HeartreachPotion,
            ItemID.CalmingPotion, ItemID.FishingPotion, ItemID.SonarPotion, ItemID.CratePotion,
            ItemID.WarmthPotion, ItemID.AmmoReservationPotion, ItemID.BuilderPotion, ItemID.TrapsightPotion,
            ItemID.LuckPotion, ItemID.BiomeSightPotion,
        ];

        private static bool IsBuffPotion(int type) => Array.IndexOf(BuffPotions, type) >= 0;

        public override bool EndowShoot(Player player, Item item, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (item.fishingPole <= 0) {
                return true;
            }
            //原版那根照常出，再多甩三根，扇形略散、力度略异
            for (int k = 0; k < ExtraLines; k++) {
                float spread = (k - (ExtraLines - 1) * 0.5f) * 0.11f;
                Vector2 lineVelocity = velocity.RotatedBy(spread) * Main.rand.NextFloat(0.85f, 1.15f);
                Projectile.NewProjectile(source, position, lineVelocity, type, 0, 0f, player.whoAmI);
            }
            return true;
        }

        public override void EndowCatchFish(Player player, GodSmithArmorPlayer state, FishingAttempt attempt,
            ref int itemDrop, ref int npcSpawn) {
            //只替换普通渔获：任务鱼、宝匣、稀有以上、敌怪不动
            if (itemDrop <= 0 || npcSpawn > 0 || attempt.crate || attempt.legendary || attempt.veryrare
                || itemDrop == attempt.questFish) {
                return;
            }
            int roll = Main.rand.Next(100);
            if (roll < PotionChance) {
                itemDrop = BuffPotions[Main.rand.Next(BuffPotions.Length)];
                return;
            }
            if (roll >= PotionChance + ForeignFishChance) {
                return;
            }
            List<int> foreign = new(Fishes.Length);
            for (int i = 0; i < Fishes.Length; i++) {
                if (!Fishes[i].Here(player, attempt)) {
                    foreign.Add(Fishes[i].Type);
                }
            }
            if (foreign.Count > 0) {
                itemDrop = foreign[Main.rand.Next(foreign.Count)];
            }
        }

        public override void EndowModifyCaughtFish(Player player, GodSmithArmorPlayer state, Item fish) {
            //药水不是正常渔获，穿着本套钓上来的药水都出自上面的替换，直接给堆叠
            if (IsBuffPotion(fish.type)) {
                fish.stack = Main.rand.Next(3, 8);
            }
        }
    }
}
