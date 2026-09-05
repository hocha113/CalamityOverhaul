using CalamityOverhaul.Common;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys.RTerraBlades
{
    /// <summary>
    /// 泰拉之刃重做，五拍连段：两记厚重的上下劈砍 → 两圈快速椭圆环斩 → 一记泰拉大回旋<br/>
    /// 每一拍都沿刀路放出大型追踪的泰拉之光；泰拉之光命中时唤出铸成此剑的两魂之一：<br/>
    /// 真圣剑之魂自天而降贯穿目标，真永夜之刃之魂在命中处回旋斩击<br/>
    /// 刀光与彗尾走 TerraBlade.fx（翠绿叶脉体 + 白金/白紫刃缘 + 夜紫内沉）
    /// </summary>
    internal class RTerraBlade : ItemOverride
    {
        public override int TargetID => ItemID.TerraBlade;

        /// <summary>连段拍数</summary>
        internal const int Beats = 5;
        /// <summary>泰拉之光伤害占刀身底伤比例</summary>
        internal const float BoltDamageMul = 0.45f;

        //泰拉色板：翠绿主体 + 光魂金 + 夜魂紫
        internal static readonly Color TerraCore = new(200, 255, 205);    //白翠热核
        internal static readonly Color TerraBright = new(108, 255, 140);  //亮翠
        internal static readonly Color TerraMain = new(34, 178, 82);      //大地翠绿
        internal static readonly Color TerraDeep = new(12, 82, 44);       //深翠
        internal static readonly Color LightGold = new(255, 214, 112);    //光魂金
        internal static readonly Color LightCream = new(255, 246, 204);   //光魂白金
        internal static readonly Color NightViolet = new(160, 86, 255);   //夜魂紫
        internal static readonly Color NightDeep = new(58, 18, 104);      //夜魂深紫

        /// <summary>连段计数，取模五拍；只在本地玩家的 Shoot 里消费</summary>
        private int comboCounter;
        /// <summary>断手回第一拍的倒计时</summary>
        private int comboResetTimer;

        /// <summary>魂位 0 夜 1 光</summary>
        internal static Color SoulColor(float soul) => Color.Lerp(NightViolet, LightGold, soul);
        internal static Color SoulBright(float soul) => Color.Lerp(new Color(214, 184, 255), LightCream, soul);

        public override void SetStaticDefaults() {
            //noMelee 会丢近战词缀，强行标回
            ItemMeleePrefixDic[ItemID.TerraBlade] = true;
        }

        public override void SetDefaults(Item item) {
            //节奏由手持存活期接管（重劈~26/环斩~16/回旋~36 帧，吃近战攻速）
            item.damage = 72;
            item.useTime = item.useAnimation = 14;
            item.knockBack = 6.5f;
            item.useStyle = ItemUseStyleID.Shoot;
            item.useTurn = false;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.autoReuse = true;
            item.UseSound = null;
            item.shoot = ModContent.ProjectileType<RTerraBladeHeld>();
            item.shootSpeed = 12f;
        }

        public override bool? CanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<RTerraBladeHeld>()] > 0) {
                return false;
            }
            return null;
        }

        public override void HoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int beat = comboCounter % Beats;
            //拍0 下劈 拍1 上撩 拍2/3 环斩反向交替 拍4 回旋；正号=顺挥向（下劈向），手持侧再乘朝向
            float swingSign = beat switch { 0 => 1f, 1 => -1f, 2 => -1f, 3 => 1f, _ => 1f };
            comboCounter++;
            //终结拍之后给更长的续段窗，其余拍断手即回首拍
            comboResetTimer = beat == Beats - 1 ? 90 : 70;
            Projectile.NewProjectile(source, player.Center, velocity
                , ModContent.ProjectileType<RTerraBladeHeld>(), damage, knockback, player.whoAmI, beat, swingSign);
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            string[] lines = Tooltip.Value.Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    continue;
                }
                tooltips.Add(new TooltipLine(CWRMod.Instance, "CWR_RTerraBlade" + i, lines[i]));
            }
        }
    }

    /// <summary>泰拉之刃族共用资源</summary>
    internal static class TerraBladeFX
    {
        public static Effect Shader => EffectLoader.TerraBlade?.Value;
        public static Texture2D Noise => CWRAsset.PerlinNoise?.Value;
        public static Texture2D SoftGlow => CWRAsset.SoftGlow?.Value;
        public static Texture2D StarWhite => CWRAsset.StarTexture_White?.Value;
        public static Texture2D StarBlack => CWRAsset.StarTexture?.Value;
        public static Texture2D LightShot => CWRAsset.LightShot?.Value;
        public static Texture2D WaveFallback => CWRAsset.SemiCircularSmear?.Value;

        /// <summary>原版物品贴图懒加载取用</summary>
        public static Texture2D ItemTex(int itemID) {
            Main.instance.LoadItem(itemID);
            return Terraria.GameContent.TextureAssets.Item[itemID].Value;
        }

        /// <summary>把 s1 噪声槽归还，防止同帧邻居串采</summary>
        public static void ReleaseNoiseSlot(GraphicsDevice device) => device.Textures[1] = null;
    }
}
