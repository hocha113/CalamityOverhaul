using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.UIs.WeaponSkills;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 寰宇咏叹调
    internal class AriaofTheCosmos : ModItem, IWeaponSkillProvider
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";

        /// <summary>星环技能冷却(帧) 2秒</summary>
        public int QSkillCooldown;
        /// <summary>伽马暴技能冷却(帧) 3秒</summary>
        public int RSkillCooldown;
        private const int QSkillMaxCooldown = 120;
        private const int RSkillMaxCooldown = 180;

        //技能按钮身份色:星环青,伽马紫
        private static readonly Color StarRingAccent = new(110, 195, 255);
        private static readonly Color GammaAccent = new(185, 140, 255);

        public static LocalizedText SkillLeftName { get; private set; }
        public static LocalizedText SkillLeftDesc { get; private set; }
        public static LocalizedText SkillRightName { get; private set; }
        public static LocalizedText SkillRightDesc { get; private set; }

        public override void SetStaticDefaults() {
            SkillLeftName = this.GetLocalization(nameof(SkillLeftName), () => "星环护卫");
            SkillLeftDesc = this.GetLocalization(nameof(SkillLeftDesc), () => "召唤绕身星环，节点轮转弹射星屑打击周围敌人");
            SkillRightName = this.GetLocalization(nameof(SkillRightName), () => "伽马射线爆发");
            SkillRightDesc = this.GetLocalization(nameof(SkillRightDesc), () => "短暂凝聚后朝准星方向扇形轰出九道伽马射线");
        }

        public override void SetDefaults() {
            Item.damage = 285;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 20;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AccretionDisk>();
            Item.shootSpeed = 0f;
            Item.channel = true;
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_MiracleMatter, CWRID.Item_Rock)) {
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_MiracleMatter, 24)
                .AddIngredient<StarflowPlatedBlock>(16)
                .AddIngredient(CWRID.Item_Rock)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override bool AltFunctionUse(Player player) => true;

        //蓄力武器魔力在释放与技能时手动扣除
        public override void ModifyManaCost(Player player, ref float reduce, ref float mult) => mult = 0f;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<AriaofTheCosmosHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<AriaofTheCosmosHeld>(player, source);

        public override void HoldItem(Player player) {
            //冷却挂在物品持有上 不依赖手持弹幕;触发在技能按钮 HUD 点击时进来
            if (QSkillCooldown > 0) {
                QSkillCooldown--;
            }
            if (RSkillCooldown > 0) {
                RSkillCooldown--;
            }
        }

        #region 技能按钮 HUD 接线
        WeaponSkillView IWeaponSkillProvider.GetWeaponSkill(int slot, Player player) {
            bool left = slot == 0;
            bool alive = left
                ? player.CountProjectilesOfID<AriaQSkill>() > 0
                : player.CountProjectilesOfID<AriaRSkill>() > 0;
            int cdLeft = left ? QSkillCooldown : RSkillCooldown;
            return new WeaponSkillView {
                Name = (left ? SkillLeftName : SkillRightName).Value,
                Desc = (left ? SkillLeftDesc : SkillRightDesc).Value,
                CostLine = string.Format(WeaponSkillHud.ManaCostFormat.Value, Item.mana * (left ? 2 : 3)),
                Accent = left ? StarRingAccent : GammaAccent,
                CooldownLeft = cdLeft,
                CooldownTotal = left ? QSkillMaxCooldown : RSkillMaxCooldown,
                Alive = alive,
                Ready = cdLeft <= 0 && !alive,
            };
        }

        bool IWeaponSkillProvider.TriggerWeaponSkill(int slot, Player player)
            => slot == 0 ? TryCastStarRing(player) : TryCastGammaBurst(player);

        private bool TryCastStarRing(Player player) {
            if (Main.myPlayer != player.whoAmI || QSkillCooldown > 0
                || player.CountProjectilesOfID<AriaQSkill>() > 0) {
                return false;
            }
            ShootState state = player.GetShootState();
            EntitySource_ItemUse_WithAmmo source = new(player, Item, ItemID.None, "CWRGunShoot");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<AriaQSkill>(), state.WeaponDamage, state.WeaponKnockback, player.whoAmI);
            QSkillCooldown = QSkillMaxCooldown;
            player.statMana = Math.Max(player.statMana - Item.mana * 2, 0);
            //激活音效由 AriaQSkill 出场帧自播 物品侧不再重复
            return true;
        }

        private bool TryCastGammaBurst(Player player) {
            if (Main.myPlayer != player.whoAmI || RSkillCooldown > 0
                || player.CountProjectilesOfID<AriaRSkill>() > 0) {
                return false;
            }
            ShootState state = player.GetShootState();
            EntitySource_ItemUse_WithAmmo source = new(player, Item, ItemID.None, "CWRGunShoot");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<AriaRSkill>(), (int)(state.WeaponDamage * 1.5f), state.WeaponKnockback * 1.5f, player.whoAmI);
            RSkillCooldown = RSkillMaxCooldown;
            player.statMana = Math.Max(player.statMana - Item.mana * 3, 0);
            //激活音效由 AriaRSkill 蓄力首帧自播 物品侧不再重复
            return true;
        }

        void IWeaponSkillProvider.DrawWeaponSkillIcon(SpriteBatch sb, int slot
            , Vector2 center, float radius, float lit, float time, float alpha) {
            if (slot == 0) {
                DrawStarRingIcon(sb, center, radius, lit, time, alpha);
            }
            else {
                DrawGammaIcon(sb, center, radius, lit, time, alpha);
            }
        }

        /// <summary>星环图标:轮转的六节点星环,呼应 <see cref="AriaQSkill"/> 本体</summary>
        private static void DrawStarRingIcon(SpriteBatch sb, Vector2 c, float r, float lit, float time, float a) {
            float litMul = MathHelper.Lerp(0.4f, 1f, lit);
            Color col = StarRingAccent * (litMul * a);
            float ringR = r * 0.82f;
            WeaponSkillBrush.DrawRing(sb, c, ringR, 1.2f, col * 0.75f, 36);
            float phase = time * 0.9f;
            for (int i = 0; i < 6; i++) {
                Vector2 p = c + (phase + MathHelper.TwoPi * i / 6f).ToRotationVector2() * ringR;
                Color node = Color.Lerp(StarRingAccent, Color.White, 0.35f * lit) * (litMul * a);
                WeaponSkillBrush.DrawFilledCircle(sb, p, 2.1f, node);
                if (lit > 0.6f) {
                    WeaponSkillBrush.DrawGlow(sb, p, 5f, StarRingAccent, 0.4f * lit * a);
                }
            }
            WeaponSkillBrush.DrawFilledCircle(sb, c, 2.4f, Color.Lerp(StarRingAccent, Color.White, 0.5f) * (litMul * a));
            WeaponSkillBrush.DrawGlow(sb, c, 8f, StarRingAccent, 0.3f * lit * a);
        }

        /// <summary>伽马暴图标:自下而上的扇形射线束,中道最亮</summary>
        private static void DrawGammaIcon(SpriteBatch sb, Vector2 c, float r, float lit, float time, float a) {
            float litMul = MathHelper.Lerp(0.4f, 1f, lit);
            Vector2 origin = c + new Vector2(0f, r * 0.6f);
            float spread = MathHelper.ToRadians(46f);
            for (int i = 0; i < 5; i++) {
                float t01 = i / 4f;
                float ang = -MathHelper.PiOver2 - spread * 0.5f + spread * t01;
                bool mid = i == 2;
                float len = r * (mid ? 1.5f : 1.15f);
                float shimmer = 0.7f + 0.3f * MathF.Sin(time * 5f + i * 1.3f);
                Color beam = Color.Lerp(GammaAccent, Color.White, mid ? 0.45f : 0.15f) * (litMul * shimmer * a);
                WeaponSkillBrush.Line(sb, origin, origin + ang.ToRotationVector2() * len, beam, mid ? 1.8f : 1.1f);
            }
            WeaponSkillBrush.DrawFilledCircle(sb, origin, 2.6f, Color.Lerp(GammaAccent, Color.White, 0.5f) * (litMul * a));
            WeaponSkillBrush.DrawGlow(sb, origin, 8f, GammaAccent, 0.4f * lit * a);
        }
        #endregion
    }
}
