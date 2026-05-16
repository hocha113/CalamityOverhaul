using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameSystem;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class ModifyHeavenfallLongbow : ItemOverride
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
            Item.value = Item.buyPrice(990, 25, 5, 5);
            Item.rare = ItemRarityID.Red;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_HeavenfallLongbow;
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

            if (!CWRKeySystem.WeponSkill_Q.JustPressed) {
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
            tooltips.InsertHotkeyBinding(CWRKeySystem.WeponSkill_Q, noneTip: CWRLocText.Instance.Notbound.Value + $"[{CWRKeySystem.WeponSkill_Q.DisplayName}]");
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

            _ = SoundEngine.PlaySound("CalamityMod/Sounds/Item/PlasmaBolt".GetSound() with { Volume = 0.8f }, player.Center);

            //棱镜星河爆发: 沿伯努利双纽线分布的稀疏棱镜碎片 + 环形极光丝带
            if (!VaultUtils.isServer) {
                const int prismCount = 180;
                float rot = 0;
                float outwardMultiplier = MathHelper.Lerp(4f, 220f, Utils.GetLerpValue(0f, 120f, 13, true));
                for (int j = 0; j < prismCount; j++) {
                    rot += MathHelper.TwoPi / prismCount;
                    float scale = 2f / (3f - (float)Math.Cos(2 * rot)) * 25f;
                    Vector2 lemniscateOffset = scale * new Vector2((float)Math.Cos(rot), (float)Math.Sin(2f * rot) / 2f);
                    Vector2 pos = player.Center + lemniscateOffset * outwardMultiplier;
                    Color color = VaultUtils.MultiStepColorLerp(j / (float)prismCount, rainbowColors);
                    PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                        pos, Vector2.Zero, color,
                        Main.rand.NextFloat(1.0f, 1.7f), Main.rand.Next(90, 130),
                        Main.rand.NextFloat(3.5f, 6f), shortStretch: true));
                }

                //外环极光丝带 (16 段)
                const int auroraCount = 16;
                for (int i = 0; i < auroraCount; i++) {
                    float ang = MathHelper.TwoPi * i / auroraCount + Main.rand.NextFloat(-0.08f, 0.08f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(4f, 7f);
                    PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                        player.Center, vel,
                        Main.rand.NextFloat(140f, 200f), Main.rand.NextFloat(24f, 34f),
                        Main.rand.Next(45, 65),
                        huePhase: i / (float)auroraCount, hueSpeed: 0.025f, driftScale: 1.3f));
                }
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
            //已处理过的群组锚点集合，避免对同一个Boss重复触发
            HashSet<int> handledAnchors = [];
            //群组成员复用缓冲
            List<NPC> groupBuffer = [];

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.Center.To(origPos).LengthSquared() > maxLengthSquared) {
                    continue;
                }
                int anchor = NpcGroupHelper.GetAnchorIndex(npc);
                if (anchor >= 0 && !handledAnchors.Add(anchor)) {
                    //同群组的别的体节已经被处理过，跳过
                    continue;
                }
                //收集整个群组（蠕虫所有体节、月总所有实体等）一并击杀
                //无论它们是否都在范围内，避免出现"半个Boss被击杀"的情况
                NpcGroupHelper.CollectGroup(npc, groupBuffer);
                if (groupBuffer.Count == 0) {
                    KillAction(npc);
                    continue;
                }
                for (int i = 0; i < groupBuffer.Count; i++) {
                    KillAction(groupBuffer[i]);
                }
            }
        }
    }
}
