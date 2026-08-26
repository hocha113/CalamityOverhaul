using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Scenarios.SupCal.SupCalDisplayTexts;
using CalamityOverhaul.Content.UIs.WeaponSkills;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>万魔殿</summary>
    internal class Pandemonium : ModItem, IWeaponSkillProvider
    {
        public override string Texture => CWRConstant.Item_Magic + "Pandemonium";

        //技能按钮身份色:天罚硫火橙,终焉绯红
        private static readonly Color JudgementAccent = new(255, 145, 60);
        private static readonly Color FinaleAccent = new(235, 75, 70);

        public static LocalizedText SkillLeftName { get; private set; }
        public static LocalizedText SkillLeftDesc { get; private set; }
        public static LocalizedText SkillRightName { get; private set; }
        public static LocalizedText SkillRightDesc { get; private set; }

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
            SkillLeftName = this.GetLocalization(nameof(SkillLeftName), () => "硫磺火天罚");
            SkillLeftDesc = this.GetLocalization(nameof(SkillLeftDesc), () => "在准星周围降下成片硫磺火柱");
            SkillRightName = this.GetLocalization(nameof(SkillRightName), () => "万魔终焉");
            SkillRightDesc = this.GetLocalization(nameof(SkillRightDesc), () => "以自身为心展开巨型法阵，雷火交加轰击阵内敌人");
        }

        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 25;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.knockBack = 5;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = CWRID.Rarity_BurnishedAuric;
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
            Item.shootSpeed = 10f;
            Item.channel = true;
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_AshesofAnnihilation, CWRID.Item_Heresy
                , CWRID.Item_Vehemence, CWRID.Item_Rock)) {
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_Heresy)
                .AddIngredient(CWRID.Item_Vehemence)
                .AddIngredient(CWRID.Item_AshesofAnnihilation, 38)
                .AddIngredient(CWRID.Item_Rock)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (EbnState.OnEbn(Main.LocalPlayer)) {
                TooltipLine line = new(Mod, "Story", SupCalDisplayText.Story4.Value);
                line.OverrideColor = Color.OrangeRed;
                tooltips.Add(line);
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.mana = 40;
                Item.useTime = Item.useAnimation = 35;
                Item.channel = false;
                Item.shoot = ModContent.ProjectileType<PandemoniumCircle>();
                return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumCircle>()] < 13; //最多13个法阵
            }
            else {
                Item.mana = 25;
                Item.useTime = Item.useAnimation = 20;
                Item.channel = true;
                Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
                return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumChannel>()] == 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {

                Vector2 targetPos = Main.MouseWorld;
                Projectile.NewProjectile(
                    source,
                    targetPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<PandemoniumCircle>(),
                    (int)(damage * 0.8f), //右键伤害为左键的80%
                    knockback,
                    player.whoAmI
                );
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        #region 技能按钮 HUD 接线
        WeaponSkillView IWeaponSkillProvider.GetWeaponSkill(int slot, Player player) {
            bool left = slot == 0;
            bool alive = left
                ? player.CountProjectilesOfID<PandemoniumQSkill>() > 0
                : player.CountProjectilesOfID<PandemoniumRSkill>() > 0;
            return new WeaponSkillView {
                Name = (left ? SkillLeftName : SkillRightName).Value,
                Desc = (left ? SkillLeftDesc : SkillRightDesc).Value,
                CostLine = null,
                Accent = left ? JudgementAccent : FinaleAccent,
                CooldownLeft = 0,
                CooldownTotal = 0,
                Alive = alive,
                Ready = !alive,
            };
        }

        bool IWeaponSkillProvider.TriggerWeaponSkill(int slot, Player player)
            => slot == 0 ? TryCastJudgement(player) : TryCastFinale(player);

        private static bool TryCastJudgement(Player player) {
            if (Main.myPlayer != player.whoAmI
                || player.CountProjectilesOfID<PandemoniumQSkill>() > 0) {
                return false;
            }
            ShootState shootState = player.GetShootState();
            Projectile.NewProjectile(shootState.Source, player.Center
                , Vector2.Zero, ModContent.ProjectileType<PandemoniumQSkill>()
                , shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI);
            return true;
        }

        private static bool TryCastFinale(Player player) {
            if (Main.myPlayer != player.whoAmI
                || player.CountProjectilesOfID<PandemoniumRSkill>() > 0) {
                return false;
            }
            ShootState shootState = player.GetShootState();
            Projectile.NewProjectile(shootState.Source, player.Center
                , Vector2.Zero, ModContent.ProjectileType<PandemoniumRSkill>()
                , shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI);
            return true;
        }

        void IWeaponSkillProvider.DrawWeaponSkillIcon(SpriteBatch sb, int slot
            , Vector2 center, float radius, float lit, float time, float alpha) {
            if (slot == 0) {
                DrawJudgementIcon(sb, center, radius, lit, time, alpha);
            }
            else {
                DrawFinaleIcon(sb, center, radius, lit, time, alpha);
            }
        }

        /// <summary>天罚图标:符刻环缓转,中央硫火柱,柱顶随火摆</summary>
        private static void DrawJudgementIcon(SpriteBatch sb, Vector2 c, float r, float lit, float time, float a) {
            float litMul = MathHelper.Lerp(0.4f, 1f, lit);
            Color col = JudgementAccent * (litMul * a);
            float ringR = r * 0.92f;
            WeaponSkillBrush.DrawRing(sb, c, ringR, 1.1f, col * 0.65f, 34);
            float phase = -time * 0.5f;
            for (int i = 0; i < 6; i++) {
                Vector2 dir = (phase + MathHelper.TwoPi * i / 6f).ToRotationVector2();
                WeaponSkillBrush.Line(sb, c + dir * (ringR - 2.2f), c + dir * (ringR + 2.2f), col * 0.9f, 1.4f);
            }
            float sway = MathF.Sin(time * 6.5f) * 1.2f * lit;
            Vector2 basePos = c + new Vector2(0f, r * 0.62f);
            Vector2 topPos = c + new Vector2(sway, -r * 0.66f);
            WeaponSkillBrush.Line(sb, basePos, topPos, new Color(190, 45, 30) * (litMul * a), 4.2f);
            WeaponSkillBrush.Line(sb, basePos, Vector2.Lerp(basePos, topPos, 0.88f), col, 2.6f);
            WeaponSkillBrush.Line(sb, basePos, Vector2.Lerp(basePos, topPos, 0.5f),
                Color.Lerp(JudgementAccent, Color.White, 0.55f) * (litMul * a), 1.2f);
            WeaponSkillBrush.DrawGlow(sb, basePos + new Vector2(0f, -2f), 7f, JudgementAccent, 0.4f * lit * a);
        }

        /// <summary>终焉图标:双环六辐的同心大法阵,恶魔之芯居中</summary>
        private static void DrawFinaleIcon(SpriteBatch sb, Vector2 c, float r, float lit, float time, float a) {
            float litMul = MathHelper.Lerp(0.4f, 1f, lit);
            Color col = FinaleAccent * (litMul * a);
            WeaponSkillBrush.DrawRing(sb, c, r * 0.92f, 1.3f, col, 36);
            WeaponSkillBrush.DrawRing(sb, c, r * 0.56f, 1.1f, col * 0.85f, 30);
            float phase = time * 0.6f;
            for (int i = 0; i < 6; i++) {
                Vector2 dir = (phase + MathHelper.TwoPi * i / 6f).ToRotationVector2();
                WeaponSkillBrush.Line(sb, c + dir * r * 0.56f, c + dir * r * 0.92f, col * 0.8f, 1.2f);
            }
            WeaponSkillBrush.DrawFilledCircle(sb, c, 2.6f,
                Color.Lerp(FinaleAccent, Color.White, 0.4f * lit) * (litMul * a));
            WeaponSkillBrush.DrawGlow(sb, c, 7f, FinaleAccent, 0.35f * lit * a);
        }
        #endregion
    }
}
