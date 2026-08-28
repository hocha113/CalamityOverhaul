using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.OtherMods.Wikithis;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutOverride : ItemOverride, ILocalizedModType
    {
        public override string LocalizationCategory => "Legend";

        public static LocalizedText FishByStudied { get; private set; }
        public static LocalizedText FishOnStudied { get; private set; }

        //==================== 自绘面板文本(HalibutItemTooltipPanel) ====================
        public static LocalizedText KeyLabelDomain { get; private set; }
        public static LocalizedText KeyLabelClone { get; private set; }
        public static LocalizedText KeyLabelSuperpose { get; private set; }
        public static LocalizedText KeyLabelRestart { get; private set; }
        public static LocalizedText KeyLabelRestartWide { get; private set; }
        public static LocalizedText KeyLabelTeleport { get; private set; }
        public static LocalizedText KeyLabelWheel { get; private set; }
        public static LocalizedText KeyLabelAtlas { get; private set; }

        #region Data
        /// <summary>HalibutItem 类型 ID</summary>
        public static int ID => ModContent.ItemType<HalibutItem>();
        /// <summary>ItemOverride 目标 ID</summary>
        public override int TargetID => ID;

        /// <summary>改动信息由自绘面板承载,关掉鼠标旁的金色小图标</summary>
        public override bool DrawingInfo => false;
        /// <summary>武器缩放</summary>
        public static float ItemScale => 0.8f;
        /// <summary>各时期伤害表，请用 <see cref="GetOnDamage"/></summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>各时期额外暴击表，请用 <see cref="GetOnCrit"/></summary>
        private static Dictionary<int, int> SetLevelCritDictionary = new Dictionary<int, int>();
        /// <summary>开局伤害</summary>
        public static int GetStartDamage => DamageDictionary[0];
        #endregion
        /// <summary>时期伤害</summary>
        public static int GetOnDamage(Item item) => DamageDictionary[HalibutData.GetLevel(item)];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage(Item item) => GetOnDamage(item) / (float)GetStartDamage;
        /// <summary>远程乘算伤害（RangedDamageClass）</summary>
        public static int ActualRangedDamage(Item item) => (int)(Main.LocalPlayer.GetTotalDamage<RangedDamageClass>().ApplyTo(GetOnDamage(item)));
        /// <summary>时期额外暴击</summary>
        public static int GetOnCrit(Item item) => SetLevelCritDictionary[HalibutData.GetLevel(item)];

        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 4 },
                {1, 5 },
                {2, 6 },
                {3, 8 },
                {4, 11 },
                {5, 15 },
                {6, 20 },
                {7, 27 },
                {8, 35 },
                {9, 48 },
                {10, 65 },
                {11, 80 },
                {12, 110 },
                {13, 170 },
                {14, 280 }
            };
            SetLevelCritDictionary = new Dictionary<int, int>(){
                {0, 0 },
                {1, 1 },
                {2, 2 },
                {3, 3 },
                {4, 4 },
                {5, 5 },
                {6, 6 },
                {7, 7 },
                {8, 7 },
                {9, 8 },
                {10, 9 },
                {11, 10 },
                {12, 11 },
                {13, 13 },
                {14, 15 }
            };
        }
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += GetOnCrit(item);
            return false;
        }
        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            VaultUtils.ApplyWeaponDamageScaling(item, GetOnDamage(item), GetStartDamage, ref damage);
            return false;
        }

        public override bool? On_ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRItem.OverModifyTooltip(item, tooltips);
            SetTooltip(item, ref tooltips);
            WikithisRef.TryAppendWikiTooltip(item, tooltips);
            return false;
        }

        public override void SetStaticDefaults() {
            FishByStudied = this.GetLocalization(nameof(FishByStudied), () =>
                """
                [i:CalamityOverhaul/HalibutItem]:
                这条鱼可被研究
                """);
            FishOnStudied = this.GetLocalization(nameof(FishOnStudied), () =>
                """
                [i:CalamityOverhaul/HalibutItem]:
                这条鱼已经研究
                """);
            //自绘面板:键位功能名(与键位表动作名对齐)
            KeyLabelDomain = this.GetLocalization(nameof(KeyLabelDomain), () => "领域展开");
            KeyLabelClone = this.GetLocalization(nameof(KeyLabelClone), () => "过去身入侵");
            KeyLabelSuperpose = this.GetLocalization(nameof(KeyLabelSuperpose), () => "叠加");
            KeyLabelRestart = this.GetLocalization(nameof(KeyLabelRestart), () => "重启自身");
            KeyLabelRestartWide = this.GetLocalization(nameof(KeyLabelRestartWide), () => "大范围重启");
            KeyLabelTeleport = this.GetLocalization(nameof(KeyLabelTeleport), () => "领域传送");
            KeyLabelWheel = this.GetLocalization(nameof(KeyLabelWheel), () => "技能盘");
            KeyLabelAtlas = this.GetLocalization(nameof(KeyLabelAtlas), () => "深渊图鉴");
            LoadWeaponData();
        }

        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            //试炼进度与任务书提示已由自绘面板(HalibutItemTooltipPanel)承载,
            //旧 [Lang4]/legend_Text 占位符随正文裁短一并退役;正文预折行对齐面板宽
            LegendTooltipPanel.WrapBodyText(tooltips);
        }

        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.damage = GetStartDamage;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 128;
            Item.height = 76;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.scale = ItemScale;
            Item.rare = CWRID.Rarity_HotPink > 0 ? CWRID.Rarity_HotPink : ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 2, 50, 0);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.UseSound = SoundID.Item38 with { Volume = 0.6f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.CWR().LegendData = new HalibutData();
        }

        public override bool? CanUseItem(Item item, Player player) {
            item.UseSound = SoundID.Item38 with { Volume = 0.6f };
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.CanUseItem(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            if (player.CountProjectilesOfID<SuperpositionProj>() > 0) {
                return false;
            }
            return true;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.AltFunctionUse(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return false;
        }

        public override bool? UseItem(Item item, Player player) {
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.UseItem(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return null;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool isBullet = false;
            bool shouldSkipShoot = false;

            position += velocity.UnitVector() * 40;

            if (type == ProjectileID.Bullet) {
                isBullet = true;
                type = ModContent.ProjectileType<OceanCurrent>();
            }
            else {
                int num = 0;
                foreach (var p in Main.ActiveProjectiles) {
                    if (p.owner == player.whoAmI && p.friendly) {
                        num++;
                    }
                }
                if (num > 220) {
                    shouldSkipShoot = true;
                }
            }

            var hp = player.GetOverride<HalibutPlayer>();
            //记录克隆需要的射击事件
            if (hp.CloneFishActive) {
                hp.RegisterShoot(type, velocity, damage, knockback, item.type);
            }

            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.ShootAlt(item, player, source, position, velocity, type, damage, knockback);
                if (result.HasValue) {
                    return result.Value;
                }
            }

            if (player.altFunctionUse == 2) {
                return false;//右键不触发普通攻击
            }

            if (fishSkill != null) {
                bool? result = fishSkill.Shoot(item, player, source, position, velocity, type, damage, knockback);
                if (result.HasValue) {
                    return result.Value;
                }
            }

            int bulletAmt = Main.rand.Next((int)(1 + HalibutData.GetLevel() * 0.35f), (int)(1 + HalibutData.GetLevel() * 0.65f));
            if (isBullet) {
                damage = (int)(damage * (1f + (bulletAmt - 1) * (1f - 0.3 / Main.LocalPlayer.GetDamage<RangedDamageClass>().Additive)) * (1f + HalibutData.GetLevel() / 26f));
                if (damage < 12) {
                    damage = 12;
                }
                bulletAmt = 1;
            }
            else if (shouldSkipShoot) {
                damage *= bulletAmt;
                bulletAmt = 1;
            }

            for (int index = 0; index < bulletAmt; ++index) {
                float SpeedX = velocity.X + Main.rand.Next(-10, 11) * 0.05f;
                float SpeedY = velocity.Y + Main.rand.Next(-10, 11) * 0.05f;
                if (isBullet) {
                    SpeedX *= 1.4f;
                    SpeedY *= 1.4f;
                }
                Projectile.NewProjectile(source, position.X, position.Y, SpeedX, SpeedY, type, damage, knockback, player.whoAmI);
            }

            return false;
        }
    }
}
