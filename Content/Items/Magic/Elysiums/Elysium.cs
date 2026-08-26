using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.UIs.WeaponSkills;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>天国极乐，与万魔殿相对的教皇权杖</summary>
    internal class Elysium : ModItem, IWeaponSkillProvider
    {
        public override string Texture => CWRConstant.Item_Magic + "Elysium";

        //技能按钮身份色：天雷雷霆金，审判炽印橙红
        private static readonly Color ThunderAccent = new(250, 220, 96);
        private static readonly Color SealAccent = new(240, 140, 90);

        public static LocalizedText SkillLeftName { get; private set; }
        public static LocalizedText SkillLeftDesc { get; private set; }
        public static LocalizedText SkillRightName { get; private set; }
        public static LocalizedText SkillRightDesc { get; private set; }
        public static LocalizedText SkillRightLockedDesc { get; private set; }
        public static LocalizedText SkillOpenName { get; private set; }
        public static LocalizedText SkillOpenDesc { get; private set; }
        public static LocalizedText SkillMeteorName { get; private set; }
        public static LocalizedText SkillMeteorDesc { get; private set; }

        /// <summary>十二门徒名(席位序)</summary>
        public static LocalizedText[] DiscipleNameTexts { get; private set; }
        /// <summary>硬拒绝：附近没有可升华的居民</summary>
        public static LocalizedText NoConvertTargetText { get; private set; }
        /// <summary>硬拒绝：十二圣位已满</summary>
        public static LocalizedText SeatsFullText { get; private set; }

        //转化搜寻半径
        private const float ConvertRange = 300f;

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;

            DiscipleNameTexts = new LocalizedText[ElysiumPlayer.SeatCount];
            string[] defaultNames = ["彼得", "安德鲁", "雅各", "约翰", "腓力", "巴多罗买",
                "多马", "马太", "小雅各", "达太", "奋锐党西门", "犹大"];
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                string name = defaultNames[i];
                DiscipleNameTexts[i] = this.GetLocalization($"DiscipleName_{i}", () => name);
            }

            NoConvertTargetText = this.GetLocalization(nameof(NoConvertTargetText), () => "附近没有可升华的居民");
            SeatsFullText = this.GetLocalization(nameof(SeatsFullText), () => "十二圣位已满");
            //犹大背叛的死亡讯息(死因文本经NetworkText.FromKey取用)
            _ = this.GetLocalization("JudasDeathReasonText", () => "{0}被犹大以三十银币出卖了");

            SkillLeftName = this.GetLocalization(nameof(SkillLeftName), () => "神圣天雷");
            SkillLeftDesc = this.GetLocalization(nameof(SkillLeftDesc), () => "召下一道圣雷，劈向准星旁的敌人");
            SkillRightName = this.GetLocalization(nameof(SkillRightName), () => "后三印审判");
            SkillRightDesc = this.GetLocalization(nameof(SkillRightDesc), () => "第五、六、七印相继轰开，终幕审判四野，启示录随之落幕");
            SkillRightLockedDesc = this.GetLocalization(nameof(SkillRightLockedDesc), () => "启示录降临后方可揭印");
            SkillOpenName = this.GetLocalization(nameof(SkillOpenName), () => "揭开启示录");
            SkillOpenDesc = this.GetLocalization(nameof(SkillOpenDesc), () => "约翰将殉道升天，天国领域随之降临");
            SkillMeteorName = this.GetLocalization(nameof(SkillMeteorName), () => "天体陨石");
            SkillMeteorDesc = this.GetLocalization(nameof(SkillMeteorDesc), () => "自天穹唤落一颗天体，砸向准星所指");
        }

        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 20;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 6;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = CWRID.Rarity_BurnishedAuric;
            Item.UseSound = null;//音效归手持弹幕
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ElysiumHeld>();
            Item.shootSpeed = 12f;
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_Apotheosis, CWRID.Item_DivineGeode
                , CWRID.Item_AshesofAnnihilation, CWRID.Item_Rock)) {
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_Apotheosis)
                .AddIngredient(CWRID.Item_DivineGeode, 15)
                .AddIngredient(CWRID.Item_AshesofAnnihilation, 38)
                .AddIngredient(CWRID.Item_Rock)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                if (!player.TryGetModPlayer(out ElysiumPlayer ep)) {
                    return false;
                }

                //启示录期间右键：召唤下一位骑士
                if (ep.IsRevelationActive) {
                    Item.mana = 15;
                    Item.useTime = Item.useAnimation = 18;
                    return !ep.IsSealJudgmentActive && ep.HorsemenCount < 4;
                }

                //右键：升华城镇居民为门徒
                Item.mana = 50;
                Item.useTime = Item.useAnimation = 30;
                if (!ep.TryGetFreeSeat(out _)) {
                    if (player.whoAmI == Main.myPlayer) {
                        CombatText.NewText(player.Hitbox, Color.Gold, SeatsFullText.Value);
                    }
                    return false;
                }
                return true;
            }

            //左键：化蛇术蓄力
            Item.mana = 20;
            Item.useTime = Item.useAnimation = 25;
            return player.ownedProjectileCounts[ModContent.ProjectileType<ElysiumHeld>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                if (player.TryGetModPlayer(out ElysiumPlayer ep) && ep.IsRevelationActive) {
                    ep.SummonNextHorseman();
                }
                else {
                    TryConvertNearest(player);
                }
                return false;
            }
            return BaseHeldGun.SpawnHeldProj<ElysiumHeld>(player, source);
        }

        /// <summary>主人端：搜寻最近城镇居民并向服务器请求升华</summary>
        private static void TryConvertNearest(Player player) {
            if (player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out ElysiumPlayer ep)
                || !ep.TryGetFreeSeat(out int seat)) {
                return;
            }

            int targetIndex = -1;
            float closest = ConvertRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.townNPC || npc.life <= 0) {
                    continue;
                }
                float dist = Vector2.Distance(player.Center, npc.Center);
                if (dist < closest) {
                    closest = dist;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0) {
                CombatText.NewText(player.Hitbox, Color.Gray, NoConvertTargetText.Value);
                return;
            }

            ElysiumNet.RequestConvert(player, targetIndex, seat);
        }

        #region 技能按钮 HUD 接线
        public const int ThunderCooldownMax = 150;
        public const int MeteorCooldownMax = 24;

        WeaponSkillView IWeaponSkillProvider.GetWeaponSkill(int slot, Player player) {
            player.TryGetModPlayer(out ElysiumPlayer ep);
            if (slot == 0) {
                //槽0三态：启示录就绪→揭幕；启示录中→陨石；平时→天雷
                if (ep != null && ep.RevelationReady) {
                    return new WeaponSkillView {
                        Name = SkillOpenName.Value,
                        Desc = SkillOpenDesc.Value,
                        CostLine = null,
                        Accent = new Color(255, 250, 230),
                        CooldownLeft = 0,
                        CooldownTotal = 0,
                        Alive = false,
                        Ready = true,
                    };
                }
                if (ep != null && ep.IsRevelationActive) {
                    int meteorCd = ep.MeteorCooldown;
                    int meteorMax = ep.HasDeathHorseman ? MeteorCooldownMax / 2 : MeteorCooldownMax;
                    return new WeaponSkillView {
                        Name = SkillMeteorName.Value,
                        Desc = SkillMeteorDesc.Value,
                        CostLine = null,
                        Accent = new Color(255, 190, 100),
                        CooldownLeft = meteorCd,
                        CooldownTotal = meteorMax,
                        Alive = false,
                        Ready = meteorCd <= 0 && !ep.IsSealJudgmentActive,
                    };
                }
                int cooldown = ep?.ThunderCooldown ?? 0;
                return new WeaponSkillView {
                    Name = SkillLeftName.Value,
                    Desc = SkillLeftDesc.Value,
                    CostLine = null,
                    Accent = ThunderAccent,
                    CooldownLeft = cooldown,
                    CooldownTotal = ThunderCooldownMax,
                    Alive = false,
                    Ready = cooldown <= 0,
                };
            }

            //槽1：后三印审判，启示录期间解锁
            bool revelation = ep != null && ep.IsRevelationActive;
            bool judging = ep != null && ep.IsSealJudgmentActive;
            return new WeaponSkillView {
                Name = SkillRightName.Value,
                Desc = revelation ? SkillRightDesc.Value : SkillRightLockedDesc.Value,
                CostLine = null,
                Accent = SealAccent,
                CooldownLeft = 0,
                CooldownTotal = 0,
                Alive = judging,
                Ready = revelation && !judging,
            };
        }

        bool IWeaponSkillProvider.TriggerWeaponSkill(int slot, Player player) {
            if (!player.TryGetModPlayer(out ElysiumPlayer ep)) {
                return false;
            }
            if (slot == 0) {
                if (ep.RevelationReady) {
                    ep.ActivateRevelation();
                    return true;
                }
                if (ep.IsRevelationActive) {
                    return TryCastMeteor(player, ep);
                }
                return TryCastThunder(player);
            }
            return TryCastSealJudgment(player, ep);
        }

        /// <summary>天体陨石：自准星上方唤落(本地客户端触发)</summary>
        private static bool TryCastMeteor(Player player, ElysiumPlayer ep) {
            if (Main.myPlayer != player.whoAmI || ep.MeteorCooldown > 0 || ep.IsSealJudgmentActive) {
                return false;
            }
            Vector2 target = Main.MouseWorld;
            ShootState shootState = player.GetShootState();
            float damageMul = ep.HasDeathHorseman ? 2.4f : 1.8f;
            Projectile.NewProjectile(shootState.Source,
                new Vector2(target.X + Main.rand.NextFloat(-160f, 160f), target.Y - 860f),
                new Vector2(0f, 6f),
                ModContent.ProjectileType<Revelations.CelestialMeteor>(),
                (int)(shootState.WeaponDamage * damageMul), 8f, player.whoAmI, target.X, target.Y);
            ep.MeteorCooldown = ep.HasDeathHorseman ? MeteorCooldownMax / 2 : MeteorCooldownMax;
            return true;
        }

        /// <summary>后三印审判：启示录中触发一次(本地客户端触发)</summary>
        private static bool TryCastSealJudgment(Player player, ElysiumPlayer ep) {
            if (Main.myPlayer != player.whoAmI || !ep.IsRevelationActive || ep.IsSealJudgmentActive) {
                return false;
            }
            ShootState shootState = player.GetShootState();
            Projectile.NewProjectile(shootState.Source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<Revelations.RevelationSealJudgment>(),
                shootState.WeaponDamage, 10f, player.whoAmI);
            return true;
        }

        /// <summary>神圣天雷：劈向准星旁最近的敌人(本地客户端触发)</summary>
        private static bool TryCastThunder(Player player) {
            if (Main.myPlayer != player.whoAmI
                || !player.TryGetModPlayer(out ElysiumPlayer ep) || ep.ThunderCooldown > 0) {
                return false;
            }

            //落点：准星附近最近的敌人，没有就劈准星
            Vector2 strikePoint = Main.MouseWorld;
            float closest = 400f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Main.MouseWorld);
                if (dist < closest) {
                    closest = dist;
                    strikePoint = npc.Center;
                }
            }

            ShootState shootState = player.GetShootState();
            Projectile.NewProjectile(shootState.Source, strikePoint, Vector2.Zero,
                ModContent.ProjectileType<ElysiumThunder>(),
                (int)(shootState.WeaponDamage * 1.6f), shootState.WeaponKnockback, player.whoAmI);
            ep.ThunderCooldown = ThunderCooldownMax;
            return true;
        }

        void IWeaponSkillProvider.DrawWeaponSkillIcon(SpriteBatch sb, int slot
            , Vector2 center, float radius, float lit, float time, float alpha) {
            if (slot == 0) {
                DrawThunderIcon(sb, center, radius, lit, time, alpha);
            }
            else {
                DrawSealIcon(sb, center, radius, lit, time, alpha);
            }
        }

        /// <summary>天雷图标：细环内一道折光落雷，尖端悬着光点</summary>
        private static void DrawThunderIcon(SpriteBatch sb, Vector2 c, float r, float lit, float time, float a) {
            float litMul = MathHelper.Lerp(0.4f, 1f, lit);
            Color col = ThunderAccent * (litMul * a);
            WeaponSkillBrush.DrawRing(sb, c, r * 0.92f, 1.1f, col * 0.6f, 34);

            //折光落雷：三段折线自上而下
            float sway = MathF.Sin(time * 5f) * 1.2f * lit;
            Vector2 p0 = c + new Vector2(r * 0.22f + sway, -r * 0.62f);
            Vector2 p1 = c + new Vector2(-r * 0.16f, -r * 0.06f);
            Vector2 p2 = c + new Vector2(r * 0.14f, 0.04f * r);
            Vector2 p3 = c + new Vector2(-r * 0.2f, r * 0.62f);
            WeaponSkillBrush.Line(sb, p0, p1, col, 2.4f);
            WeaponSkillBrush.Line(sb, p1, p2, col, 2.2f);
            WeaponSkillBrush.Line(sb, p2, p3, col, 2f);
            Color coreCol = Color.Lerp(ThunderAccent, Color.White, 0.55f) * (litMul * a);
            WeaponSkillBrush.Line(sb, p0, p1, coreCol, 1f);
            WeaponSkillBrush.Line(sb, p1, p2, coreCol, 0.9f);
            WeaponSkillBrush.DrawGlow(sb, p3, 6f, ThunderAccent, 0.4f * lit * a);
        }

        /// <summary>三印图标：三重同心弧各缺一口，印点错落其上</summary>
        private static void DrawSealIcon(SpriteBatch sb, Vector2 c, float r, float lit, float time, float a) {
            float litMul = MathHelper.Lerp(0.4f, 1f, lit);
            Color col = SealAccent * (litMul * a);
            float phase = time * 0.4f;
            for (int i = 0; i < 3; i++) {
                float ringR = r * (0.38f + i * 0.26f);
                float gapAt = phase * (i % 2 == 0 ? 1f : -1f) + i * 2.1f;
                WeaponSkillBrush.DrawArc(sb, c, ringR, 1.2f, col * (0.85f - i * 0.15f),
                    gapAt + 0.5f, gapAt + MathHelper.TwoPi - 0.5f, 30);
                Vector2 sealDot = c + (gapAt + MathHelper.TwoPi - 0.25f).ToRotationVector2() * ringR;
                WeaponSkillBrush.DrawFilledCircle(sb, sealDot, 1.8f, Color.Lerp(SealAccent, Color.White, 0.4f) * (litMul * a));
            }
            WeaponSkillBrush.DrawGlow(sb, c, 6f, SealAccent, 0.3f * lit * a);
        }
        #endregion
    }
}
