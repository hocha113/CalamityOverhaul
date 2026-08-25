using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.BloodshotBombs
{
    /// <summary>
    /// 泣血瞳雷，克眼掉落的引线眼球炸弹
    /// 选中即点燃引线，引线共三档各两秒，越烧越短、眼球越红、爆炸越猛
    /// 引线烧尽仍握在手里则当场炸开并伤及持有者
    /// </summary>
    internal class BloodshotBomb : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "BloodshotBomb";

        /// <summary>引线总时长(帧)，烧尽即在手中炸开</summary>
        internal const int FuseMaxTime = 360;
        /// <summary>单档引线时长(帧)，三档共 <see cref="FuseMaxTime"/></summary>
        internal const int TierTime = 120;
        /// <summary>掷出或自爆后重新掏弹的时长(帧)，期间引线未点燃</summary>
        internal const int RearmTime = 60;
        /// <summary>三档末段的狂闪警告窗口(帧)</summary>
        internal const int WarnTime = 45;

        /// <summary>各档伤害倍率</summary>
        internal static readonly float[] TierDamageMul = new float[] { 1f, 2f, 3.6f };
        /// <summary>各档血雾爆炸半径(像素)</summary>
        internal static readonly int[] TierBlastRadius = new int[] { 70, 110, 160 };
        /// <summary>各档命中敌人时迸出的血肉碎块数</summary>
        internal static readonly int[] TierChunkCount = new int[] { 3, 6, 10 };

        /// <summary>由引线已燃帧数取档位(0-2)</summary>
        internal static int GetTier(float fuseTime) => Math.Min((int)(fuseTime / TierTime), 2);

        public override void SetStaticDefaults() {
            //提前注册自爆死亡讯息键
            _ = this.GetLocalization("SelfBoom", () => "{0} took one look too many");
        }

        public override void SetDefaults() {
            Item.width = 21;
            Item.height = 22;
            Item.damage = 30;
            Item.knockBack = 5f;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<BloodshotBombThrown>();
            Item.shootSpeed = 12.5f;
            Item.DamageType = DamageClass.Ranged;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 50);
        }

        //点燃、掷出、自爆全部由手持弹幕管理，物品本身不走使用流程
        public override bool CanUseItem(Player player) => false;

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI || player.dead) {
                return;
            }
            if (player.CountProjectilesOfID<BloodshotBombHeld>() == 0) {
                Projectile.NewProjectile(player.GetSource_FromThis(), player.Center, Vector2.Zero
                    , ModContent.ProjectileType<BloodshotBombHeld>(), 0, 0, player.whoAmI);
            }
        }
    }
}
