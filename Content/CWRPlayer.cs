using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.Items.Magic.Extras;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Summon.Extras;
using CalamityOverhaul.Content.Items.Tools;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    public class CWRPlayer : ModPlayer
    {
        public bool InitialCreation;
        /// <summary>
        /// 圣物的装备等级，这个字段决定了玩家会拥有什么样的弹幕效果
        /// </summary>
        public int TheRelicLuxor = 0;
        /// <summary>
        /// 是否装备制动器
        /// </summary>
        public bool LoadMuzzleBrake;
        /// <summary>
        /// 应力缩放
        /// </summary>
        public float PressureIncrease;
        //未使用的，这个属性属于一个未完成的UI
        public int CompressorPanelID = -1;
        /// <summary>
        /// 是否开启超级合成台
        /// </summary>
        public bool SupertableUIStartBool;
        /// <summary>
        /// 玩家是否坐在大排档塑料椅子之上
        /// </summary>
        public bool InFoodStallChair;
        /// <summary>
        /// 玩家是否装备休谟稳定器
        /// </summary>
        public bool EndlessStabilizerBool;
        /// <summary>
        /// 玩家是否手持鬼妖
        /// </summary>
        public bool HeldMurasamaBool;
        /// <summary>
        /// 玩家是否正在进行终结技
        /// </summary>
        public bool EndSkillEffectStartBool;
        /// <summary>
        /// 升龙技冷却时间
        /// </summary>
        public int RisingDragonCoolDownTime;
        /// <summary>
        /// 是否受伤
        /// </summary>
        public bool OnHit;

        public override void Initialize() {
            OnHit = false;
            TheRelicLuxor = 0;
            PressureIncrease = 1;
            LoadMuzzleBrake = false;
            InitialCreation = true;
        }

        public override void ResetEffects() {
            OnHit = false;
            TheRelicLuxor = 0;
            PressureIncrease = 1;
            InFoodStallChair = false;
            EndlessStabilizerBool = false;
            HeldMurasamaBool = false;
            EndSkillEffectStartBool = false;
            LoadMuzzleBrake = false;
        }

        public override void SaveData(TagCompound tag) {
            tag.Add("_InitialCreation", InitialCreation);
        }

        public override void LoadData(TagCompound tag) {
            InitialCreation = tag.GetBool("_InitialCreation");
        }

        public override void OnEnterWorld() {
            if (CWRServerConfig.Instance.ForceReplaceResetContent) {
                CWRUtils.Text(CWRMod.RItemIndsDict.Count + CWRLocText.GetTextValue("OnEnterWorld_TextContent"), Color.GreenYellow);
            }
            if (InitialCreation) {
                for (int i = 0; i < Player.inventory.Length; i++) {
                    if (Player.inventory[i].type == ItemID.CopperAxe) {
                        Player.inventory[i] = new Item(ModContent.ItemType<PebbleAxe>());
                    }
                    if (Player.inventory[i].type == ItemID.CopperPickaxe) {
                        Player.inventory[i] = new Item(ModContent.ItemType<PebblePick>());
                    }
                    if (Player.inventory[i].type == ItemID.CopperBroadsword 
                        || Player.inventory[i].type == ItemID.CopperShortsword) {
                        Player.inventory[i] = new Item(ModContent.ItemType<PebbleSpear>());
                    }
                }
                InitialCreation = false;
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            OnHit = true;
        }

        public override void PostUpdate() {
            if (Player.sitting.TryGetSittingBlock(Player, out Tile t)) {
                if (t.TileType == CWRIDs.FoodStallChairTile) {
                    InFoodStallChair = true;
                    Main.raining = true;
                    Main.maxRaining = 0.99f;
                    Main.cloudAlpha = 0.99f;
                    Main.windSpeedTarget = 0.8f;
                    float sengs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f));
                    Lighting.AddLight(Player.Center, new Color(Main.DiscoB, Main.DiscoG, 220 + sengs * 30).ToVector3() * sengs * 113);
                    PunchCameraModifier modifier2 = new PunchCameraModifier(Player.Center, new Vector2(0, Main.rand.NextFloat(-2, 2)), 2f, 3f, 2, 1000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier2);
                }
            }
        }

        public override void ModifyWeaponDamage(Item item, ref StatModifier damage) {
            if (LoadMuzzleBrake) {
                if (item.DamageType == DamageClass.Ranged) {
                    damage *= 0.75f;
                }
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            base.ModifyHitByNPC(npc, ref modifiers);
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            base.ModifyHitByProjectile(proj, ref modifiers);
        }

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) {
            return base.CanBeHitByNPC(npc, ref cooldownSlot);
        }

        public override bool CanBeHitByProjectile(Projectile proj) {
            return base.CanBeHitByProjectile(proj);
        }

        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) {
            yield return new Item(ModContent.ItemType<PebbleSpear>());
            yield return new Item(ModContent.ItemType<PebblePick>());
            yield return new Item(ModContent.ItemType<PebbleAxe>());
            yield return new Item(ModContent.ItemType<TheSpiritFlint>());
            yield return new Item(ModContent.ItemType<TheUpiStele>());
            yield return new Item(ModContent.ItemType<Pebble>(), 999);
            yield return new Item(ModContent.ItemType<OverhaulTheBibleBook>());
        }

        public override void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath) {
            if (!mediumCoreDeath) {
                itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperAxe);
                itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperShortsword);
                itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperPickaxe);
            }
        }
    }
}
