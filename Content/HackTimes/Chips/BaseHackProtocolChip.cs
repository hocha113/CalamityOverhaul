using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    /// <summary>
    /// 协议芯片：一次性消耗品，用掉即把绑定协议写进玩家的骇入库。<br/>
    /// 图标由 <see cref="HackChipGlyph"/> 逐帧合成，不占贴图资产。<br/>
    /// 结算刻意全落在本机：解锁写的是客户端自己的 ModPlayer，
    /// 不像 RAM 芯片那样要等权威端回执再扣（<see cref="RAMSystems.BaseRamUpgradeChip"/>）。<br/>
    /// 本类是非泛型的，为的是让获取系统能用
    /// <c>Mod.GetContent&lt;BaseHackProtocolChip&gt;()</c> 一次枚举全家族；
    /// 写新芯片请继承泛型的 <see cref="BaseHackProtocolChip{T}"/>
    /// </summary>
    internal abstract class BaseHackProtocolChip : ModItem, ILocalizedModType
    {
        public override string LocalizationCategory => "Items";

        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>绑定协议，注册未完成时为 null</summary>
        internal abstract QuickHackDef Protocol { get; }

        /// <summary>晶粒纹登记名</summary>
        protected abstract string DieKey { get; }

        /// <summary>是否进入后续的获取池。获取系统尚未实现，此处只是留好接缝</summary>
        public virtual bool CanGenerateInLabChest => true;

        /// <summary>晶粒纹 SVG d 串；不给则退回通用电路纹</summary>
        protected virtual string DiePath => null;

        /// <summary>图标主色，默认跟协议类别走</summary>
        protected virtual Color GlyphColor {
            get {
                QuickHackDef hack = Protocol;
                return hack != null ? HackTheme.CategoryColor(hack.Category) : HackTheme.Accent;
            }
        }

        public override void SetStaticDefaults() {
            if (!string.IsNullOrEmpty(DiePath)) {
                HackChipGlyph.Register(DieKey, DiePath);
            }
        }

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 30;
            Item.maxStack = 99;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(gold: 2);
            Item.UseSound = CWRSound.ChipSet;
        }

        //只拦本机：远端玩家的举手动作照常演，不然队友用芯片时你这边是空举
        public override bool CanUseItem(Player player)
            => player.whoAmI != Main.myPlayer
                || Protocol != null && !HackProtocolOwned.Owns(player, Protocol);

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server) {
                return false;
            }
            QuickHackDef hack = Protocol;
            if (hack == null || !HackProtocolOwned.Unlock(player, hack)) {
                return false;
            }
            SoundEngine.PlaySound(SoundID.ResearchComplete, player.Center);
            return true;
        }

        //背包归客户端所有，本机直接扣，靠每帧玩家差分同步
        public override bool ConsumeItem(Player player) => player.whoAmI == Main.myPlayer;

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            QuickHackDef hack = Protocol;
            if (hack == null) {
                return;
            }
            Color catColor = HackTheme.CategoryColor(hack.Category);
            int index = tooltips.FindIndex(line => line.Name == "ItemName");
            if (index != -1) {
                tooltips.Insert(index + 1,
                    new TooltipLine(Mod, "HackChipCategory", HackTheme.CategoryLabel(hack.Category)) {
                        OverrideColor = catColor,
                    });
            }

            tooltips.Add(new TooltipLine(Mod, "HackChipGrants",
                HackTime.ChipGrants.Format(hack.DisplayName.Value)) {
                OverrideColor = Color.Lerp(catColor, Color.White, 0.4f),
            });
            tooltips.Add(new TooltipLine(Mod, "HackChipTarget",
                HackTime.ChipTarget.Format(DescribeTargets(hack.SupportedTargets))) {
                OverrideColor = HackTheme.AccentAlt,
            });
            tooltips.Add(new TooltipLine(Mod, "HackChipDesc", hack.Description.Value) {
                OverrideColor = HackTheme.TextNormal,
            });
            //"一次性写入"对每枚芯片都一样，走共用串而不是十七份重复的 Tooltip
            tooltips.Add(new TooltipLine(Mod, "HackChipOneShot", HackTime.ChipOneShot.Value) {
                OverrideColor = HackTheme.TextDim,
            });
            if (HackProtocolOwned.Owns(Main.LocalPlayer, hack)) {
                tooltips.Add(new TooltipLine(Mod, "HackChipOwned", HackTime.ChipAlreadyOwned.Value) {
                    OverrideColor = HackTheme.TextDim,
                });
            }
        }

        //协议可作用的目标种类，多选时顿号连写
        private static string DescribeTargets(HackTargetKind kinds) {
            List<string> names = [];
            foreach (HackTargetType type in HackTargetType.Instances) {
                if ((kinds & type.Kind) != 0) {
                    names.Add(type.DisplayName.Value);
                }
            }
            return names.Count > 0 ? string.Join('/', names) : "-";
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            HackChipGlyph.Draw(spriteBatch, DieKey, position, 13f * scale,
                drawColor.A / 255f, GlyphColor, 0f, Main.GameUpdateCount * 0.02f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor
            , ref float rotation, ref float scale, int whoAmI) {
            Vector2 center = Item.Center - Main.screenPosition;
            //暗处可寻：alpha 兜底 + 一点冷背光
            float a = MathHelper.Max(lightColor.A / 255f, 0.35f);
            Color accent = GlyphColor;
            HackChipGlyph.DrawBacklight(spriteBatch, center, 13f * scale, accent, a * 0.26f);
            HackChipGlyph.Draw(spriteBatch, DieKey, center, 13f * scale, a, accent,
                rotation, Main.GameUpdateCount * 0.02f);
            return false;
        }
    }

    /// <summary>按协议类型绑定的芯片，子类通常只需要给一条 <see cref="BaseHackProtocolChip.DiePath"/></summary>
    internal abstract class BaseHackProtocolChip<T> : BaseHackProtocolChip where T : QuickHackDef
    {
        internal sealed override QuickHackDef Protocol => QuickHackDef.Get<T>();

        protected override string DieKey => typeof(T).Name;
    }
}
