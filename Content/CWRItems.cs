using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.StorageBattery;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Projectiles.Weapons;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.UIs;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static Humanizer.In;

namespace CalamityOverhaul.Content
{
    public enum SpecialAmmoStateEnum
    {
        /// <summary>
        /// 普通，默认值，即无特殊效果
        /// </summary>
        ordinary,
        /// <summary>
        /// 燃烧弹
        /// </summary>
        napalmBomb,
        /// <summary>
        /// 破甲
        /// </summary>
        armourPiercer,
        /// <summary>
        /// 高爆
        /// </summary>
        highExplosive,
        /// <summary>
        /// 龙息
        /// </summary>
        dragonBreath
    }

    public class CWRItems : GlobalItem
    {
        public override bool InstancePerEntity => true;

        #region Date
        /// <summary>
        /// 用于存储物品的状态值，对这个数组的使用避免了额外类成员的创建
        /// (自建类成员数据对于修改物品而言总是令人困惑)
        /// 这个数组不会自动的网络同步，需要在合适的时机下调用同步指令
        /// </summary>
        public float[] ai = new float[] { 0, 0, 0 };
        /// <summary>
        /// 是否是一个重制物品，在基类为<see cref="EctypeItem"/>时自动启用
        /// </summary>
        public bool remakeItem;
        /// <summary>
        /// 是否正在真近战
        /// </summary>
        public bool closeCombat;
        /// <summary>
        /// 正在手持这个物品的玩家实例
        /// </summary>
        public Player HoldOwner = null;
        /// <summary>
        /// 一般用于近战类武器的充能值
        /// </summary>
        public float MeleeCharge;
        /// <summary>
        /// 是否是一个手持物品，改判定与<see cref="heldProjType"/> >0 具有同样的功效，都会被系统认定为手持物品
        /// </summary>
        public bool isHeldItem;
        /// <summary>
        /// 设置这个物品的手持弹幕Type，默认为0，如果是0便认定为无手持
        /// </summary>
        public int heldProjType;
        /// <summary>
        /// 在有手持弹幕存在时还可以使用武器吗？设置为<see langword="true"/>在拥有手持弹幕时禁止物品使用，设置为<see langword="false"/>默认物品的原使用
        /// </summary>
        public bool hasHeldNoCanUseBool;
        /// <summary>
        /// 是否已经装好了弹药，一般来讲，该字段用于存储可装弹式手持弹幕的装弹状态
        /// </summary>
        public bool IsKreload;
        /// <summary>
        /// 该物品是否具有弹夹系统
        /// </summary>
        public bool HasCartridgeHolder;
        /// <summary>
        /// 使用的弹匣UI类型
        /// </summary>
        public CartridgeUIEnum CartridgeEnum;
        /// <summary>
        /// 特种状态
        /// </summary>
        public SpecialAmmoStateEnum SpecialAmmoState;
        /// <summary>
        /// 弹匣内容，管理装填后的弹药部分
        /// </summary>
        internal Item[] MagazineContents;
        /// <summary>
        /// 该物品对应的剩余子弹数量，一般用于给手持枪械弹幕访问
        /// </summary>
        public int NumberBullets;
        /// <summary>
        /// 该物品的弹容量
        /// </summary>
        public int AmmoCapacity;
        /// <summary>
        /// 大于0时不可以装弹
        /// </summary>
        public int NoKreLoadTime;
        /// <summary>
        /// 该物品是否可以开启侦察
        /// </summary>
        public bool Scope;
        /// <summary>
        /// 退弹时，该物品是否返还
        /// </summary>
        public bool AmmoProjectileReturn;
        /// <summary>
        /// 是否是一个无尽物品，这个的设置决定物品是否会受到湮灭机制的影响
        /// </summary>
        internal bool isInfiniteItem;
        /// <summary>
        /// 是否湮灭，在设置<see cref="isInfiniteItem"/>为<see langword="true"/>后启用，决定无尽物品是否湮灭
        /// </summary>
        internal bool noDestruct;
        /// <summary>
        /// 湮灭倒计时间
        /// </summary>
        internal int destructTime;
        /// <summary>
        /// 这个物品所属的终焉合成内容，这决定了它的物品简介是否绘制终焉合成表格
        /// </summary>
        internal string[] OmigaSnyContent;
        /// <summary>
        /// 用于动态开关该合成指示UI的变量
        /// </summary>
        internal bool DrawOmigaSnyUIBool;
        /// <summary>
        /// 电力交互时间
        /// </summary>
        public int PowerInteractionValue;
        /// <summary>
        /// 是一个蓄电池
        /// </summary>
        public bool IsStorageBattery;
        /// <summary>
        /// 是一把弓
        /// </summary>
        public bool IsBow;
        #endregion

        public override void SetDefaults(Item item) {
            if (CWRIDs.OnLoadContentBool) {
                if (item.createTile != -1 && !CWRIDs.TileToItem.ContainsKey(item.createTile)) {
                    CWRIDs.TileToItem.Add(item.createTile, item.type);
                }
                if (item.createWall != -1 && !CWRIDs.WallToItem.ContainsKey(item.createWall)) {
                    CWRIDs.WallToItem.Add(item.createWall, item.type);
                }
            }
            if (isInfiniteItem) {
                destructTime = 5;
            }
            if (AmmoCapacity == 0) {
                AmmoCapacity = 1;
            }
            PowerInteractionValue = 0;
            InitializeMagazine();
            CWRIDs.SetAmmoItem(item);
            remakeItem = (item.ModItem as EctypeItem) != null;
            int type = item.type;
            if (type == ModContent.ItemType<Ataraxia>()) {
                item.damage = 305;
            }
            if (type == ModContent.ItemType<Nadir>()) {
                item.damage = 180;
                item.useTime = item.useAnimation = 15;
            }
        }

        public void InitializeMagazine() {
            AmmoProjectileReturn = true;
            IsKreload = false;
            NumberBullets = 0;
            NoKreLoadTime += 10;
            MagazineContents = new Item[AmmoCapacity];
            for (int i = 0; i < MagazineContents.Length; i++) {
                MagazineContents[i] = new Item();
            }
        }

        //有意思的是，在数次令角色死亡死后，我确认当角色死亡时，该函数会被加载一次
        public override void SaveData(Item item, TagCompound tag) {
            tag.Add("_MeleeCharge", MeleeCharge);
            tag.Add("_noDestruct", noDestruct);
            if (HasCartridgeHolder) {
                if (MagazineContents != null && MagazineContents.Length > 0) {
                    Item[] safe_MagazineContent = MagazineContents.ToArray();//这里需要一次安全的保存中转
                    for (int i = 0; i < safe_MagazineContent.Length; i++) {
                        if (safe_MagazineContent[i] == null) {
                            safe_MagazineContent[i] = new Item(ItemID.None);
                        }
                        if (safe_MagazineContent[i].type != ItemID.None) {
                            if (!safe_MagazineContent[i].CWR().AmmoProjectileReturn) {
                                safe_MagazineContent[i] = new Item(ItemID.None);
                            }
                        }
                    }
                    tag.Add("_MagazineContents", safe_MagazineContent);
                }
                tag.Add("_IsKreload", IsKreload);
            }
        }

        public override void LoadData(Item item, TagCompound tag) {
            MeleeCharge = tag.GetFloat("_MeleeCharge");
            noDestruct = tag.GetBool("_noDestruct");
            if (HasCartridgeHolder) {
                if (tag.ContainsKey("_MagazineContents")) {
                    Item[] magazineContents = tag.Get<Item[]>("_MagazineContents");
                    for (int i = 0; i < magazineContents.Length; i++) {
                        if (magazineContents[i] == null) {
                            magazineContents[i] = new Item(ItemID.None);
                        }
                    }
                    MagazineContents = tag.Get<Item[]>("_MagazineContents");
                }
                int ammoValue = 0;
                foreach (Item i in MagazineContents) {
                    if (i.type != ItemID.None) {
                        ammoValue += i.stack;
                    }
                }
                NumberBullets = ammoValue;
                if (tag.ContainsKey("_IsKreload")) {
                    IsKreload = tag.GetBool("_IsKreload");
                }
            }
        }

        public override void HoldItem(Item item, Player player) {
            if (heldProjType > 0) {
                if (player.ownedProjectileCounts[heldProjType] == 0 && Main.myPlayer == player.whoAmI) {
                    Projectile heldProj = Projectile.NewProjectileDirect(player.parent(), player.Center, Vector2.Zero, heldProjType, item.damage, item.knockBack, player.whoAmI);
                    BaseHeldProj baseHeldProj = heldProj.ModProjectile as BaseHeldProj;
                    if (baseHeldProj != null) {
                        baseHeldProj.SpawnItem = item;
                    }
                }
                if (CWRIDs.ItemToBaseRanged.TryGetValue(item.type, out BaseHeldRanged ranged)) {
                    bool lDown = player.PressKey();
                    bool rDown = player.PressKey(false);
                    if (lDown || (rDown && !lDown && ranged.CanRightClick && !player.cursorItemIconEnabled)) {
                        player.CWR().HeldStyle = 0;
                    }
                }
            }

            OwnerByDir(item, player);
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (remakeItem) {
                TooltipLine nameLine = tooltips.FirstOrDefault((x) => x.Name == "ItemName" && x.Mod == "Terraria");
                ApplyNameLineColor(
                    new Color(1f, 0.72f + 0.2f * Main.DiscoG / 255f, 0.45f + 0.5f * Main.DiscoG / 255f)
                    , nameLine
                    );
                TooltipLine line = new TooltipLine(CWRMod.Instance, "CalamityOverhaul",
                    CalamityUtils.ColorMessage(
                        CWRLocText.GetTextValue("CWRItem_IsRemakeItem_TextContent")
                        , new Color(196, 35, 44))
                    );
                tooltips.Add(line);
            }
            //if (isInfiniteItem) {
            //    TooltipLine line = new TooltipLine(CWRMod.Instance, "CalamityOverhaul",
            //        CalamityUtils.ColorMessage(
            //            CWRLocText.GetTextValue("CWRItem_IsInfiniteItem_TextContent")
            //            , CWRUtils.MultiStepColorLerp(Main.GameUpdateCount % 120 / 120f, Color.DarkRed, Color.Red, Color.DarkGoldenrod, Color.Gold, Color.Red))
            //        );
            //    tooltips.Add(line);
            //}
        }

        public override void PostUpdate(Item item) {
            if (isInfiniteItem) {
                //Destruct(item, item.position, CWRUtils.InPosFindPlayer(item.position, 9999));
            }
        }

        public override void UpdateInventory(Item item, Player player) {
            if (isInfiniteItem) {
                //Destruct(item, player.position, player);
            }
        }

        public void Destruct(Item item, Vector2 pos, Player player) {
            if (Main.myPlayer == player.whoAmI) {
                destructTime--;
                Item[] inven = player.inventory;
                if (!noDestruct && destructTime <= 0 && inven.Count((Item n) => n.type == CWRIDs.EndlessStabilizer) == 0) {
                    if (item.type != CWRIDs.StarMyriadChanges) {
                        Projectile.NewProjectile(new EntitySource_WorldEvent()
                        , pos, Vector2.Zero, ModContent.ProjectileType<InfiniteIngotTileProj>(), 9999, 0);
                    }
                    else {
                        Projectile.NewProjectile(new EntitySource_WorldEvent()
                        , pos, Vector2.Zero, ModContent.ProjectileType<StarMyriadChangesProj>(), 1, 0);
                    }
                    item.TurnToAir();
                    StarMyriadChanges.DompDestruct_TextContent();
                }
            }
        }

        //////////////////////////////////////////////////
        //我不知道为什么 CWRPlayer 里面的Modify方法         //
        //所修改的值无法作用到枪械射弹上，上帝，让这些东西去死吧//                                        
        //////////////////////////////////////////////////
        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) {
            CWRPlayer modPlayer = player.CWR();
            if (modPlayer.TyrantsFuryBuffBool) {
                if (item.DamageType == DamageClass.Melee
                    || item.DamageType == ModContent.GetInstance<MeleeNoSpeedDamageClass>()) {
                    knockback *= 0.9f;
                }
                if (item.DamageType == ModContent.GetInstance<TrueMeleeDamageClass>()
                    || item.DamageType == ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>()) {
                    knockback *= 0.8f;
                }
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit) {
            CWRPlayer modPlayer = player.CWR();
            if (modPlayer.LoadMuzzleBrake) {
                if (item.DamageType == DamageClass.Ranged) {
                    if (modPlayer.LoadMuzzleBrakeLevel == 1) {
                        crit += 5;
                    } else if (modPlayer.LoadMuzzleBrakeLevel == 2) {
                        crit += 15;
                    } else if (modPlayer.LoadMuzzleBrakeLevel == 3) {
                        crit += 25;
                    } else if (modPlayer.LoadMuzzleBrakeLevel == 4) {
                        crit += 100;
                    }
                }
            }
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            CWRPlayer modPlayer = player.CWR();
            if (modPlayer.LoadMuzzleBrake) {
                if (item.DamageType == DamageClass.Ranged) {
                    if (modPlayer.LoadMuzzleBrakeLevel == 1) {
                        damage *= 0.75f;
                    } else if (modPlayer.LoadMuzzleBrakeLevel == 2) {
                        damage *= 0.8f;
                    } else if (modPlayer.LoadMuzzleBrakeLevel == 3) {
                        damage *= 0.85f;
                    } else if (modPlayer.LoadMuzzleBrakeLevel == 4) {
                        damage *= 2;
                    }
                }
            }
            if (modPlayer.TyrantsFuryBuffBool) {
                if (item.DamageType == DamageClass.Melee
                    || item.DamageType == ModContent.GetInstance<MeleeNoSpeedDamageClass>()) {
                    damage *= 1.05f;
                }
                if (item.DamageType == ModContent.GetInstance<TrueMeleeDamageClass>()
                    || item.DamageType == ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>()) {
                    damage *= 1.1f;
                }
            }
        }

        private void OwnerByDir(Item item, Player player) {
            if (player.PressKey() || player.PressKey(false)) {
                if (player.whoAmI == Main.myPlayer && item.useStyle == ItemUseStyleID.Swing
                && (item.createTile == -1 && item.createWall == -1)
                && !player.mouseInterface && !player.cursorItemIconEnabled) {
                    player.direction = Math.Sign(player.position.To(Main.MouseWorld).X);
                }
            }
        }

        private void ApplyNameLineColor(Color color, TooltipLine nameLine) => nameLine.OverrideColor = color;

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            CWRPlayer modPlayer = player.CWR();
            int theReLdamags = damage / 3;
            Vector2 vr = player.Center.To(Main.MouseWorld).UnitVector() * 13;

            if (player.whoAmI == Main.myPlayer) {
                if (modPlayer.TheRelicLuxor == 1) {
                    if (item.CountsAsClass<MeleeDamageClass>() || item.CountsAsClass<TrueMeleeNoSpeedDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorMelee>(), theReLdamags, 0f, player.whoAmI, 1);
                    }
                    else if (item.CountsAsClass<ThrowingDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorRogue>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                    else if (item.CountsAsClass<RangedDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorRanged>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                    else if (item.CountsAsClass<MagicDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorMagic>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                    else if (item.CountsAsClass<SummonDamageClass>()
                        && player.ownedProjectileCounts[ModContent.ProjectileType<TheRelicLuxorSummon>()] < 3) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorSummon>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                }
                if (modPlayer.TheRelicLuxor == 2) {
                    theReLdamags += 15;

                    if (item.CountsAsClass<MeleeDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorMelee>(), theReLdamags, 0f, player.whoAmI, 1);
                    }
                    else if (item.CountsAsClass<ThrowingDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorRogue>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                    else if (item.CountsAsClass<RangedDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorRanged>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                    else if (item.CountsAsClass<MagicDamageClass>()) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorMagic>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                    else if (item.CountsAsClass<SummonDamageClass>()
                        && player.ownedProjectileCounts[ModContent.ProjectileType<TheRelicLuxorSummon>()] < 6) {
                        Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<TheRelicLuxorSummon>(), theReLdamags, 0f, player.whoAmI, 0);
                    }
                }
            }
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool CanUseItem(Item item, Player player) {
            if (heldProjType > 0 && hasHeldNoCanUseBool) {
                return false;
            }
            return base.CanUseItem(item, player);
        }

        public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines) {
            if (CWRServerConfig.Instance.ForceReplaceResetContent) {
                foreach (BaseRItem rItem in CWRMod.RItemInstances) {
                    if (rItem.SetReadonlyTargetID == item.type) {
                        Texture2D value = CWRUtils.GetT2DValue("CalamityOverhaul/icon_small");
                        Main.spriteBatch.Draw(value, Main.MouseScreen - new Vector2(0, -26), null, Color.Gold, 0, value.Size() / 2
                            , MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.05f + 0.7f, SpriteEffects.None, 0);
                    }
                }
            }
        }

        public override bool PreDrawTooltip(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y) {
            if (CWRServerConfig.Instance.AddExtrasContent) {
                int offsetX = SupertableUI.Instance.Active ? 0 : 600;
                if (OmigaSnyContent != null && InItemDrawRecipe.Instance != null && SupertableUI.Instance != null) {
                    MouseTextContactPanel.Instance.UpdateSets();
                    MouseTextContactPanel.Instance.Draw(Main.spriteBatch);
                    InItemDrawRecipe.Instance.Draw(Main.spriteBatch, new Vector2(offsetX + 100, 100), OmigaSnyContent);
                }
                if (CWRServerConfig.Instance.ResetItemReminder && item.CWR().remakeItem) {
                    if (ResetItemReminderUI.Instance != null) {
                        ResetItemReminderUI.Instance.Draw(Main.spriteBatch, new Vector2(offsetX, 100));
                    }
                }
            }
            return base.PreDrawTooltip(item, lines, ref x, ref y);
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (PowerInteractionValue > 0) {
                BaseStorageBattery.DrawPowerInteractionValue(item.Calamity(), spriteBatch, position, 0.5f, PowerInteractionValue);
                PowerInteractionValue--;
            }
        }
    }
}
