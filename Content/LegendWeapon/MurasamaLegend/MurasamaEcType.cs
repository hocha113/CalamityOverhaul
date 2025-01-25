using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    /// <summary>
    /// 妖刀
    /// </summary>
    internal class MurasamaEcType : EctypeItem
    {
        #region Data
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Murasama";
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期阶段对应的挥舞范围大小，这个成员一般不需要直接访问，而是使用<see cref="GetOnScale"/>
        /// </summary>
        private static Dictionary<int, float> BladeVolumeRatioDictionary = new Dictionary<int, float>();
        /// <summary>
        /// 每个时期阶段对应的额外暴击增幅的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnCrit"/>
        /// </summary>
        private static Dictionary<int, int> SetLevelCritDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期阶段对应的升龙冷却的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnRDCD"/>
        /// </summary>
        private static Dictionary<int, int> RDCDDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期对应的击退力度字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnKnockback"/>
        /// </summary>
        private static Dictionary<int, float> KnockbackDictionary = new Dictionary<int, float>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage => DamageDictionary[InWorldBossPhase.Instance.Mura_Level()];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage => GetOnDamage / (float)GetStartDamage;
        /// <summary>
        /// 根据<see cref="GetOnDamage"/>获取一个与<see cref="TrueMeleeDamageClass"/>相关的乘算伤害
        /// </summary>
        public static int ActualTrueMeleeDamage => (int)(GetOnDamage * Main.LocalPlayer.GetDamage<TrueMeleeDamageClass>().Additive);
        /// <summary>
        /// 获取时期对应的范围增幅
        /// </summary>
        public static float GetOnScale => BladeVolumeRatioDictionary[InWorldBossPhase.Instance.Mura_Level()];
        /// <summary>
        /// 获取时期对应的额外暴击
        /// </summary>
        public static int GetOnCrit => SetLevelCritDictionary[InWorldBossPhase.Instance.Mura_Level()];
        /// <summary>
        /// 获取时期对应的冷却时间上限
        /// </summary>
        public static int GetOnRDCD => RDCDDictionary[InWorldBossPhase.Instance.Mura_Level()];
        /// <summary>
        /// 获取开局的击退力度
        /// </summary>
        public static float GetStartKnockback => KnockbackDictionary[0];
        /// <summary>
        /// 获取时期对应的击退力度
        /// </summary>
        public static float GetOnKnockback => KnockbackDictionary[InWorldBossPhase.Instance.Mura_Level()];
        /// <summary>
        /// 用于存储手持弹幕的ID，这个成员在<see cref="CWRLoad.Setup"/>中被加载，不需要进行手动的赋值
        /// </summary>
        public static int heldProjType;
        public int frameCounter = 0;
        public int frame = 0;
        public int Charge {//在外部编辑时不必操纵Charge这个特有属性，而是可以编辑ai槽位这个通用数据，这将让项目的通用性更加的好
            get {
                Item.initialize();
                return (int)Item.CWR().ai[0];
            }
            set {
                Item.initialize();
                Item.CWR().ai[0] = value;
            }
        }
        /// <summary>
        /// 是否解锁升龙斩
        /// </summary>
        public static bool UnlockSkill1 => InWorldBossPhase.Instance.Mura_Level() >= 2;
        /// <summary>
        /// 是否解锁下砸
        /// </summary>
        public static bool UnlockSkill2 => InWorldBossPhase.Instance.Mura_Level() >= 5;
        /// <summary>
        /// 是否解锁终结技
        /// </summary>
        public static bool UnlockSkill3 => InWorldBossPhase.Instance.Mura_Level() >= 9;
        public static readonly SoundStyle OrganicHit = new("CalamityMod/Sounds/Item/MurasamaHitOrganic") { Volume = 0.45f };
        public static readonly SoundStyle InorganicHit = new("CalamityMod/Sounds/Item/MurasamaHitInorganic") { Volume = 0.55f };
        public static readonly SoundStyle Swing = new("CalamityMod/Sounds/Item/MurasamaSwing") { Volume = 0.2f };
        public static readonly SoundStyle BigSwing = new("CalamityMod/Sounds/Item/MurasamaBigSwing") { Volume = 0.25f };
        private static readonly string[] SamNameList = ["激流山姆", "山姆", "Samuel Rodrigues", "Jetstream Sam", "Sam"];
        private static readonly string[] VergilNameList = ["维吉尔", "Vergil"];
        #endregion

        public static bool NameIsSam(Player player) => SamNameList.Contains(player.name);
        public static bool NameIsVergil(Player player) => VergilNameList.Contains(player.name);
        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 10 },
                {1, 14 },
                {2, 23 },
                {3, 28 },
                {4, 40 },
                {5, 65 },
                {6, 90 },
                {7, 140 },
                {8, 280 },
                {9, 450 },
                {10, 750 },
                {11, 1600 },
                {12, 1900 },
                {13, 2400 },
                {14, 4000 }
            };
            BladeVolumeRatioDictionary = new Dictionary<int, float>(){
                {0, 0.6f },
                {1, 0.65f },
                {2, 0.7f },
                {3, 0.75f },
                {4, 0.8f },
                {5, 0.85f },
                {6, 0.9f },
                {7, 1f },
                {8, 1.1f },
                {9, 1.2f },
                {10, 1.3f },
                {11, 1.35f },
                {12, 1.4f },
                {13, 1.45f },
                {14, 1.5f }
            };
            SetLevelCritDictionary = new Dictionary<int, int>(){
                {0, 1 },
                {1, 5 },
                {2, 8 },
                {3, 10 },
                {4, 12 },
                {5, 15 },
                {6, 18 },
                {7, 20 },
                {8, 22 },
                {9, 24 },
                {10, 28 },
                {11, 32 },
                {12, 36 },
                {13, 40 },
                {14, 46 }
            };
            RDCDDictionary = new Dictionary<int, int>(){
                {0, 400 },
                {1, 380 },
                {2, 360 },
                {3, 360 },
                {4, 340 },
                {5, 320 },
                {6, 300 },
                {7, 280 },
                {8, 260 },
                {9, 260 },
                {10, 240 },
                {11, 220 },
                {12, 200 },
                {13, 180 },
                {14, 160 }
            };
            KnockbackDictionary = new Dictionary<int, float>(){
                {0, 1.6f },
                {1, 1.85f },
                {2, 2.1f },
                {3, 2.45f },
                {4, 2.8f },
                {5, 3.15f },
                {6, 3.9f },
                {7, 4.2f },
                {8, 4.4f },
                {9, 5.1f },
                {10, 5.3f },
                {11, 5.65f },
                {12, 5.8f },
                {13, 6.2f },
                {14, 6.5f }
            };
        }
        public override void SetStaticDefaults() {
            LoadWeaponData();
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 13));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.height = 134;
            Item.width = 90;
            Item.damage = GetStartDamage;
            Item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 5;
            Item.knockBack = GetStartKnockback;
            Item.autoReuse = false;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.shoot = ModContent.ProjectileType<MuraSlashDefault>();
            Item.shootSpeed = 24f;
            Item.rare = ModContent.RarityType<Violet>();
            Item.CWR().isHeldItem = true;
            Item.CWR().heldProjType = heldProjType;
            Item.CWR().GetMeleePrefix = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
            => TooltipHandler.SetTooltip(ref tooltips);

        public override void ModifyWeaponCrit(Player player, ref float crit)
            => crit += GetOnCrit;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
            => CWRUtils.ModifyLegendWeaponDamageFunc(player, Item, GetOnDamage, GetStartDamage, ref damage);

        public override void ModifyWeaponKnockback(Player player, ref StatModifier knockback)
            => CWRUtils.ModifyLegendWeaponKnockbackFunc(player, Item, GetOnKnockback, GetStartKnockback, ref knockback);

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frameI, Color drawColor, Color itemColor, Vector2 origin, float scale)
            => PreDrawInInventoryFunc(spriteBatch, position, origin, scale);
        public static bool PreDrawInInventoryFunc(SpriteBatch spriteBatch, Vector2 position, Vector2 origin, float scale) {
            if (Main.LocalPlayer.CWR().HeldMurasamaBool) {
                return true;
            }
            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/MurasamaSheathed").Value;
            spriteBatch.Draw(texture, position, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0);
            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/MurasamaGlow").Value;
            spriteBatch.Draw(texture, Item.position - Main.screenPosition, Item.GetCurrentFrame(ref frame, ref frameCounter, 2, 13, false), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0);
        }

        public override bool CanUseItem(Player player) => CanUseItemFunc(player, Item);
        public static bool CanUseItemFunc(Player player, Item Item) {
            //在升龙斩或者爆发弹幕存在时不能使用武器
            return player.ownedProjectileCounts[ModContent.ProjectileType<MuraBreakerSlash>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<MuraTriggerDash>()] > 0
                || player.PressKey(false)
                ? false
                : (CWRServerConfig.Instance.WeaponEnhancementSystem || InWorldBossPhase.Level11)
                && player.ownedProjectileCounts[Item.shoot] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<MuraSlashDefault>(), damage, knockback, player.whoAmI, 0f, 0f);
            return false;
        }
    }

    internal class RMurasama : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Murasama>();
        public override int ProtogenesisID => ModContent.ItemType<MurasamaEcType>();
        public override string TargetToolTipItemName => "MurasamaEcType";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(TargetID, new DrawAnimationVertical(5, 13));
            ItemID.Sets.AnimatesAsSoul[TargetID] = true;
        }
        public override void SetDefaults(Item item) => MurasamaEcType.SetDefaultsFunc(item);
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => TooltipHandler.SetTooltip(ref tooltips);
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => CWRUtils.ModifyLegendWeaponDamageFunc(player, item, MurasamaEcType.GetOnDamage, MurasamaEcType.GetStartDamage, ref damage);
        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => CWRUtils.ModifyLegendWeaponKnockbackFunc(player, item, MurasamaEcType.GetOnKnockback, MurasamaEcType.GetStartKnockback, ref knockback);
        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += MurasamaEcType.GetOnCrit;
            return false;
        }
        public override bool On_PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
            => MurasamaEcType.PreDrawInInventoryFunc(spriteBatch, position, origin, scale);
        public override bool? On_CanUseItem(Item item, Player player) => MurasamaEcType.CanUseItemFunc(player, item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<MuraSlashDefault>(), damage, knockback, player.whoAmI, 0f, 0f);
            return false;
        }
    }
}
