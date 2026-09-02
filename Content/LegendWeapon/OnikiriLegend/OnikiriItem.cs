using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Rarities;
using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切太刀,按住左键绯红裂空连段;
    /// 里世界点中真身/媒介走肢解居合(<see cref="OnikiriPlayer.TryClickDismember"/>),落空回退连段
    /// </summary>
    internal class OnikiriItem : ModItem
    {
        /// <summary>普攻控制器切换,真=鬼门开缝(OniSlash) 假=绯红裂空斩;A/B 对比用,双方外部接口一致</summary>
        internal static bool UseOniSlash => false;

        /// <summary>绯红裂空斩表现层切换,真=扫掠版(CrimsonSweepSlash,刀身扫过的体积) 假=旧月牙版(CrimsonRendSlash);仅 UseOniSlash=false 时生效</summary>
        internal static bool UseSweepSlash => false;

        /// <summary>当前生效的连段控制器弹幕类型</summary>
        internal static int ComboProjectileType => UseOniSlash
            ? ModContent.ProjectileType<OniSlash>()
            : UseSweepSlash
                ? ModContent.ProjectileType<CrimsonSweepSlash>()
                : ModContent.ProjectileType<CrimsonRendSlash>();

        public override void SetStaticDefaults() {
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 90;
            Item.height = 96;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.knockBack = 6.5f;
            Item.crit = 8;
            Item.useAnimation = Item.useTime = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.channel = true;   //控制器按住循环,物品只触发首拍
            Item.UseSound = null;
            Item.shoot = ComboProjectileType;
            Item.shootSpeed = 1f;
            Item.rare = ModContent.RarityType<OnikiriLegendRarity>();
            Item.value = Item.buyPrice(0, 25, 0, 0);
            OnikiriOverride.SetDefaultsFunc(Item);
        }

        /// <summary>无视防御 + 半穿 DR（斩击弹幕管线统一调用）</summary>
        internal static void ApplySlashPenetration(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.DefenseEffectiveness *= 0f;
            float dr = CWRRef.GetNPCDR(target);
            if (dr > 0f && dr <= 0.9f) {
                modifiers.FinalDamage *= (1f - dr * 0.5f) / (1f - dr);
            }
        }

        /// <summary>连段/肢解在场时封锁再用(新旧控制器都查,切换开关时不双开)</summary>
        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CrimsonRendSlash>()] == 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<CrimsonSweepSlash>()] == 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<OniSlash>()] == 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<OniSeverStrike>()] == 0;

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            ReplaceInputPlaceholders(tooltips);
            OnikiriOverride.SetTooltip(Item, ref tooltips);
        }

        /// <summary>返回 false 接管 tooltip 全绘制(行数据仍来自 ModifyTooltips 管线)</summary>
        public override bool PreDrawTooltip(ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)
            => OniItemTooltipPanel.Draw(Item, lines, x, y);

        internal static void ReplaceInputPlaceholders(List<TooltipLine> tooltips) {
            InputMode mode = PlayerInput.UsingGamepad ? InputMode.XBoxGamepad : InputMode.Keyboard;
            string flashStepInput = CWRKeySystem.GetKeybindText(CWRKeySystem.Onikiri_FlashStep,
                CWRKeySystem.RightClickFallback.Value, mode);
            string sakuraFlightInput = CWRKeySystem.GetKeybindText(CWRKeySystem.Onikiri_SakuraFlight,
                CWRKeySystem.Notbound.Value, mode);
            string executeInput = CWRKeySystem.GetKeybindText(CWRKeySystem.Onikiri_Execute,
                CWRKeySystem.Notbound.Value, mode);
            tooltips.ReplacePlaceholder("[DASH]", flashStepInput);
            tooltips.ReplacePlaceholder("[SAKURA]", sakuraFlightInput);
            tooltips.ReplacePlaceholder("[EXECUTE]", executeInput);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            OnikiriPlayer okp = player.GetModPlayer<OnikiriPlayer>();
            //处决后续优先消费该左键,禁止同沿补发肢解/残心/普攻
            if (okp.TryExecutionAnnihilate(Item, edgeVerified: false)) {
                return false;
            }
            //里世界按下沿→肢解居合
            if (okp.TryClickDismember(Item)) {
                return false;
            }
            //追斩窗按下沿→残心斩
            if (okp.TryZanshinStrike(Item, edgeVerified: false)) {
                return false;
            }
            okp.CancelExecutionIntent(settleFollowup: true);
            float bladeScale = OnikiriOverride.GetBladeScale(Item);
            if (UseOniSlash) {
                OniSlash.Fire(player, player.Center, velocity, damage, knockback, scale: bladeScale, source);
            }
            else if (UseSweepSlash) {
                CrimsonSweepSlash.Fire(player, player.Center, velocity, damage, knockback, scale: bladeScale, source);
            }
            else {
                CrimsonRendSlash.Fire(player, player.Center, velocity, damage, knockback, scale: bladeScale, source);
            }
            return false;
        }
    }
}
