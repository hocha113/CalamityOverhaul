using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ModifyHeavenfallLongbow : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<HeavenfallLongbow>();
        public override bool DrawingInfo => false;
        public override bool CanLoadLocalization => false;
        //在某些不应该的情况下，武器会被禁止使用，使用这个钩子来防止这种事情的发生
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 0;
    }

    internal class HeavenfallLongbow : ModItem
    {
        public const int MaxVientNum = 13;
        public static Color[] rainbowColors = [Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Indigo, Color.Violet];
        public override string Texture => CWRConstant.Item_Ranged + "HeavenfallLongbow";
        public int ChargeValue {
            get {
                Item.Initialize();
                return (int)Item.CWR().ai[1];
            }
            set {
                Item.Initialize();
                Item.CWR().ai[1] = value;
            }
        }
        public bool SpanInfiniteRuneBool {
            get {
                Item.Initialize();
                return Item.CWR().ai[0] == 0;
            }
            set {
                Item.Initialize();
                Item.CWR().ai[0] = value ? 0 : 1;
            }
        }
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        public override bool AltFunctionUse(Player player) => true;
        public override void SetDefaults() {
            Item.damage = 9999;
            Item.width = 62;
            Item.height = 128;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = EndlessDamageClass.Instance;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<HeavenfallLongbowHeldProj>();
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_HeavenfallLongbow;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit = 9999;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage = damage.Scale(0);

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override void HoldItem(Player player) {
            //当充能达到阈值时，会释放一次无尽符文，此时可以按下技能键触发技能
            if (ChargeValue < 200 || Main.myPlayer != player.whoAmI) {
                return;
            }

            SpanInfiniteRune(player);

            if (!CWRKeySystem.HeavenfallLongbowSkillKey.JustPressed) {
                return;
            }

            int types = ModContent.ProjectileType<VientianePunishment>();

            if (player.ownedProjectileCounts[types] >= MaxVientNum) {
                return;
            }

            int randomOffset = Main.rand.Next(MaxVientNum);//生成一个随机的偏移值，这样可以让所有的弓都有机会出现
            int frmer = 0;
            for (int i = 0; i < MaxVientNum; i++) {
                int proj = Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero
                    , types, Item.damage, 0, player.whoAmI, i + randomOffset);//给予ai[0]一个可排序的索引量，这决定了该万象弹幕使用什么样的贴图
                if (i == 0)//让第一个万象弹幕作为主弹幕，负责多数代码执行
                {
                    frmer = proj;//将首号弹幕的索引储存起来
                }

                if (Main.projectile[proj].ModProjectile is VientianePunishment vientianePunishment) {
                    vientianePunishment.Index = i;//给每个万象弹幕分配合适索引，这决定了它们能否正确排序
                    vientianePunishment.FemerProjIndex = frmer;
                    vientianePunishment.Projectile.netUpdate = true;
                    vientianePunishment.Projectile.netUpdate2 = true;
                }
            }

            ChargeValue = 0;//清空能量
            SpanInfiniteRuneBool = true;//重置符文生成开关
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                InfiniteIngot.DrawColorText(Main.spriteBatch, line);
                return false;
            }
            if (line.Name == "Damage" && line.Mod == "Terraria") {
                InfiniteIngot.DrawColorText(Main.spriteBatch, line);
                return false;
            }
            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.IntegrateHotkey(CWRKeySystem.HeavenfallLongbowSkillKey);
            CWRUtils.SetItemLegendContentTops(ref tooltips, Name);
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) {
            return Main.rand.NextBool(3) && player.ownedProjectileCounts[Item.shoot] > 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            _ = Projectile.NewProjectile(source, position.X, position.Y, velocity.X, velocity.Y
                , ModContent.ProjectileType<HeavenfallLongbowHeldProj>(), damage, knockback, player.whoAmI, ai2: player.altFunctionUse == 0 ? 0 : 1);
            return false;
        }

        public void SpanInfiniteRune(Player player) {
            if (!SpanInfiniteRuneBool) {
                return;
            }

            _ = SoundEngine.PlaySound(CommonCalamitySounds.PlasmaBoltSound, player.Center);
            float rot = 0;
            for (int j = 0; j < 500; j++) {
                rot += MathHelper.TwoPi / 500f;
                float scale = 2f / (3f - (float)Math.Cos(2 * rot)) * 25;
                float outwardMultiplier = MathHelper.Lerp(4f, 220f, Utils.GetLerpValue(0f, 120f, 13, true));
                Vector2 lemniscateOffset = scale * new Vector2((float)Math.Cos(rot), (float)Math.Sin(2f * rot) / 2f);
                Vector2 pos = player.Center + lemniscateOffset * outwardMultiplier;
                Vector2 particleSpeed = Vector2.Zero;
                Color color = VaultUtils.MultiStepColorLerp(j / 500f, rainbowColors);
                BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                    , 1.5f, color, 120, 1, 1.5f, hueShift: 0.0f, _entity: player, _followingRateRatio: 1);
                PRTLoader.AddParticle(energyLeak);
            }

            if (player.ownedProjectileCounts[ModContent.ProjectileType<InfiniteRune>()] == 0) {
                _ = Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero, ModContent.ProjectileType<InfiniteRune>(), 99999, 0, player.whoAmI);
            }

            SpanInfiniteRuneBool = false;
        }

        public static void KillAction(NPC npc) {
            npc.dontTakeDamage = false;
            _ = npc.SimpleStrikeNPC(npc.lifeMax, 0);
            npc.life = 0;
            npc.checkDead();
            npc.HitEffect();
            npc.NPCLoot();
            if (npc.type == NPCID.TargetDummy) {
                VaultUtils.KillPuppet(new Point16((int)(npc.Center.X / 16), (int)(npc.Center.Y / 16)));
            }
            npc.netUpdate = true;
            npc.netUpdate2 = true;
            npc.active = false;
        }

        public static void Obliterate(Vector2 origPos) {
            const int maxLengthSquared = 90000;

            List<List<int>> allTargetNpcTypes = [
                 CWRLoad.targetNpcTypes,
                 CWRLoad.targetNpcTypes2,
                 CWRLoad.targetNpcTypes3,
                 CWRLoad.targetNpcTypes4,
                 CWRLoad.targetNpcTypes5,
                 CWRLoad.targetNpcTypes6,
                 CWRLoad.targetNpcTypes7,
                 CWRLoad.targetNpcTypes8,
                 CWRLoad.targetNpcTypes9,
                 CWRLoad.targetNpcTypes10,
                 CWRLoad.targetNpcTypes11,
                 CWRLoad.targetNpcTypes12,
                 CWRLoad.targetNpcTypes13,
                 CWRLoad.targetNpcTypes14,
                 CWRLoad.targetNpcTypes15
            ];

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.Center.To(origPos).LengthSquared() > maxLengthSquared) {
                    continue;
                }

                foreach (List<int> targetNpcTypes in allTargetNpcTypes) {
                    if (targetNpcTypes.Contains(npc.type)) {
                        foreach (NPC npcToKill in Main.npc.Where(n => targetNpcTypes.Contains(n.type))) {
                            KillAction(npcToKill);
                        }
                        break;
                    }
                }
                KillAction(npc);
            }
        }
    }
}
