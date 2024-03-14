using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 妖刀
    /// </summary>
    internal class Murasama : EctypeItem
    {
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        static Dictionary<int, int> DamageDictionary => new Dictionary<int, int>(){
            {0, 10 },
            {1, 15 },
            {2, 25 },
            {3, 32 },
            {4, 45 },
            {5, 80 },
            {6, 120 },
            {7, 180 },
            {8, 270 },
            {9, 540 },
            {10, 890 },
            {11, 1780 },
            {12, 2222 },
            {13, 2581 },
            {14, 30001 }
        };
        /// <summary>
        /// 每个时期阶段对应的挥舞范围大小，这个成员一般不需要直接访问，而是使用<see cref="GetOnScale"/>
        /// </summary>
        static Dictionary<int, float> BladeVolumeRatioDictionary => new Dictionary<int, float>(){
            {0, 0.5f },
            {1, 0.6f },
            {2, 0.7f },
            {3, 0.8f },
            {4, 0.9f },
            {5, 0.95f },
            {6, 1.05f },
            {7, 1.1f },
            {8, 1.2f },
            {9, 1.3f },
            {10, 1.4f },
            {11, 1.5f },
            {12, 1.55f },
            {13, 1.6f },
            {14, 2f }
        };
        /// <summary>
        /// 每个时期阶段对应的额外暴击振幅的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnCrit"/>
        /// </summary>
        static Dictionary<int, int> SetLevelCritDictionary => new Dictionary<int, int>(){
            {0, 6 },
            {1, 11 },
            {2, 11 },
            {3, 16 },
            {4, 26 },
            {5, 26 },
            {6, 31 },
            {7, 36 },
            {8, 41 },
            {9, 46 },
            {10, 51 },
            {11, 56 },
            {12, 61 },
            {13, 61 },
            {14, 96 }
        };
        /// <summary>
        /// 每个时期阶段对应的升龙冷却的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnRDCD"/>
        /// </summary>
        static Dictionary<int, int> RDCDDictionary => new Dictionary<int, int>(){
            {0, 110 },
            {1, 100 },
            {2, 90 },
            {3, 80 },
            {4, 75 },
            {5, 75 },
            {6, 70 },
            {7, 65 },
            {8, 60 },
            {9, 55 },
            {10, 50 },
            {11, 45 },
            {12, 40 },
            {13, 35 },
            {14, 30 }
        };
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Murasama";
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage => DamageDictionary[InWorldBossPhase.Instance.Level()];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage => GetOnDamage / (float)GetStartDamage;
        /// <summary>
        /// 根据<see cref="GetOnDamage"/>获取一个与<see cref="TrueMeleeDamageClass"/>相关的乘算伤害
        /// </summary>
        public static int ActualTrueMeleeDamage => (int)(GetOnDamage * Main.LocalPlayer.GetDamage<TrueMeleeDamageClass>().Additive);
        /// <summary>
        /// 获取时期对应的范围增幅
        /// </summary>
        public static float GetOnScale => BladeVolumeRatioDictionary[InWorldBossPhase.Instance.Level()];
        /// <summary>
        /// 获取时期对应的额外暴击
        /// </summary>
        public static int GetOnCrit => SetLevelCritDictionary[InWorldBossPhase.Instance.Level()];
        /// <summary>
        /// 获取时期对应的冷却时间上限
        /// </summary>
        public static int GetOnRDCD => RDCDDictionary[InWorldBossPhase.Instance.Level()];
        /// <summary>
        /// 大小百分比例
        /// </summary>
        public static float ScaleOffset_PercentageValue => CWRServerConfig.Instance.MurasamaScaleOffset;
        /// <summary>
        /// 用于存储手持弹幕的ID，这个成员在<see cref="CWRIDs.Load"/>中被加载，不需要进行手动的赋值
        /// </summary>
        public static int heldProjType;
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public int frameCounter = 0;
        public int frame = 0;

        public int Charge {//在外部编辑时不必操纵Charge这个特有属性，而是可以编辑ai槽位这个通用数据，这将让项目的通用性更加的好
            get {
                Item.initialize();
                return (int)Item.CWR().ai[0];
            }
            set {
                Item.initialize();
                Item.CWR().ai[0] = value;
            }
        }
        /// <summary>
        /// 是否解锁升龙斩
        /// </summary>
        public static bool UnlockSkill1 => InWorldBossPhase.Instance.Level() >= 2;
        /// <summary>
        /// 是否解锁下砸
        /// </summary>
        public static bool UnlockSkill2 => InWorldBossPhase.Instance.Level() >= 5;
        /// <summary>
        /// 是否解锁终结技
        /// </summary>
        public static bool UnlockSkill3 => InWorldBossPhase.Instance.Level() >= 9;

        public static readonly SoundStyle OrganicHit = new("CalamityMod/Sounds/Item/MurasamaHitOrganic") { Volume = 0.45f };
        public static readonly SoundStyle InorganicHit = new("CalamityMod/Sounds/Item/MurasamaHitInorganic") { Volume = 0.55f };
        public static readonly SoundStyle Swing = new("CalamityMod/Sounds/Item/MurasamaSwing") { Volume = 0.2f };
        public static readonly SoundStyle BigSwing = new("CalamityMod/Sounds/Item/MurasamaBigSwing") { Volume = 0.25f };

        public static bool NameIsVergil(Player player) => player.name == "维吉尔" || player.name == "Vergil";
        private static string[] samNameList = new string[] { "激流山姆", "山姆" , "Samuel Rodrigues" , "Jetstream Sam" , "Sam" };
        public static bool NameIsSam(Player player) => samNameList.Contains(player.name);

        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 14));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.height = 134;
            Item.width = 90;
            Item.damage = GetStartDamage;
            Item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 5;
            Item.knockBack = 6.5f;
            Item.autoReuse = false;
            Item.value = CalamityGlobalItem.Rarity15BuyPrice;
            Item.shoot = ModContent.ProjectileType<MurasamaRSlash>();
            Item.shootSpeed = 24f;
            Item.rare = ModContent.RarityType<Violet>();
            Item.CWR().isHeldItem = true;
        }

        public static void SetTooltip(ref List<TooltipLine> tooltips, string modName = "Terraria") {
            tooltips.SetHotkey(CWRKeySystem.Murasama_TriggerKey, "[KEY1]", modName);
            tooltips.SetHotkey(CWRKeySystem.Murasama_DownKey, "[KEY2]", modName);
            string text2 = CWRLocText.GetTextValue("Murasama_Text0");
            tooltips.ReplaceTooltip("[Lang1]", UnlockSkill1 ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text1")}]", modName);
            tooltips.ReplaceTooltip("[Lang2]", UnlockSkill2 ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text2")}]", modName);
            tooltips.ReplaceTooltip("[Lang3]", UnlockSkill3 ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text3")}]", modName);
            tooltips.ReplaceTooltip("[Lang4]", $"[c/00736d:试炼{InWorldBossPhase.Instance.Level() + 1}]", modName);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[Text]") && x.Mod == "Terraria");
            if (legendtops != null) {
                string lang = CWRLocText.GetTextValue("Murasama_TextDictionary_Content");
                string[] langs = lang.Split("\n");
                int index = InWorldBossPhase.Instance.Level();
                if (index >= 0 && index < langs.Length) {
                    legendtops.Text = langs[index];
                }
                else {
                    legendtops.Text = "ERROR";
                }
                legendtops.OverrideColor = Color.Lerp(Color.IndianRed, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }

            SetTooltip(ref tooltips);
        }

        public override void HoldItem(Player player) {
            player.CWR().HeldMurasamaBool = true;
            //这个代码实现了玩家手持时的动画，生成一个对玩家来说唯一的弹幕来实现这些
            if (player.ownedProjectileCounts[heldProjType] == 0 && player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.parent(), player.Center, Vector2.Zero, heldProjType, Item.damage, 0, player.whoAmI);
            }
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += GetOnCrit;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            //damage.Base *= GetOnDamage / (float)GetStartDamage;
            damage *= GetOnDamage / (float)GetStartDamage;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frameI, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D texture;
            if (Main.LocalPlayer.CWR().HeldMurasamaBool) {
                texture = ModContent.Request<Texture2D>(Texture).Value;
                spriteBatch.Draw(texture, position, Item.GetCurrentFrame(ref frame, ref frameCounter, 2, 13), Color.White, 0f, origin, scale, SpriteEffects.None, 0);
                return false;//老实说，我不清楚灾厄的制作组为什么要自定义绘制这个，因为他们自定义的方法除了会出现更多的异常之外，没有什么优势
            }
            else {
                texture = ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/MurasamaSheathed").Value;
                spriteBatch.Draw(texture, position, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (!(Charge <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack", (AssetRequestMode)2).Value;
                Texture2D barFG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront", (AssetRequestMode)2).Value;
                float barScale = 3f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
                Rectangle frameCrop = new Rectangle(0, 0, (int)(Charge / 10f * barFG.Width), barFG.Height);
                Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
                spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture;
            texture = ModContent.Request<Texture2D>(Texture).Value;
            spriteBatch.Draw(texture, Item.position - Main.screenPosition, Item.GetCurrentFrame(ref frame, ref frameCounter, 2, 13), lightColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0);
            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/MurasamaGlow").Value;
            spriteBatch.Draw(texture, Item.position - Main.screenPosition, Item.GetCurrentFrame(ref frame, ref frameCounter, 2, 13, false), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0);
        }

        public override bool CanUseItem(Player player) {
            //在升龙斩或者爆发弹幕存在时不能使用武器
            if (player.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakSwing>()] > 0 
                || player.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakOut>()] > 0
                || player.PressKey(false)//如果玩家按下了右键，也要禁止武器的使用
                ) {
                return false;
            }
            return player.ownedProjectileCounts[Item.shoot] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<MurasamaRSlash>(), damage, knockback, player.whoAmI, 0f, 0f);
            return false;
        }
    }
}
