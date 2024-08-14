using CalamityMod;
using CalamityMod.Items.Tools;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.CWRDamageTypes;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class InfinitePick : ModItem
    {
        public bool IsPick = true;
        public override string Texture => CWRConstant.Item + "Tools/" + (IsPick ? "Pickaxe" : "Hammer");
        public Texture2D value => CWRUtils.GetT2DValue(Texture);
        private bool oldRDown;
        private bool rDown;
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetStaticDefaults() => Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 16));
        public override void SetDefaults() {
            Item.damage = 9999;
            Item.DamageType = EndlessDamageClass.Instance;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 1;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(gold: 999);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.pick = int.MaxValue;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems3;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit = 9999;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            damage = damage.Scale(0);
        }

        public override bool? UseItem(Player player) {
            return base.UseItem(player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            TooltipLine cumstops = tooltips.FirstOrDefault((TooltipLine x) => x.Name == "Damage" && x.Mod == "Terraria");
            if (cumstops != null) {
                tooltips.IntegrateHotkey(CWRKeySystem.InfinitePickSkillKey);
            }
        }

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI) {
                return;
            }
            if (IsPick) {
                Item.pick = 9999;
                Item.hammer = 0;
                Item.useAnimation = Item.useTime = 10;

            }
            else {
                Item.pick = 0;
                Item.hammer = 9999;
                Item.useAnimation = Item.useTime = 30;
            }
            if (CWRKeySystem.InfinitePickSkillKey.JustPressed) {
                IsPick = !IsPick;
                SoundEngine.PlaySound(!IsPick ? CWRSound.Pecharge : CWRSound.Peuncharge, player.Center);
                TextureAssets.Item[Type] = CWRUtils.GetT2DAsset(Texture);
            }
            rDown = player.PressKey(false);
            bool justRDown = rDown && !oldRDown;
            oldRDown = rDown;
            if (justRDown && !player.mouseInterface && !player.mouseInterface && !player.cursorItemIconEnabled && player.cursorItemIconID == 0) {
                SoundEngine.PlaySound(new SoundStyle(CWRConstant.Sound + "Pedestruct"), Main.MouseWorld);
                if (!IsPick) {
                    for (int i = 0; i < 188; i++) {
                        PRT_HeavenfallStar spark = new PRT_HeavenfallStar(Main.MouseWorld + CWRUtils.randVr(213), new Vector2(0, 3), false, 13, 1, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors));
                        PRTLoader.AddParticle(spark);
                    }
                    int maxX = 500;
                    int maxY = 500;
                    Vector2 pos = Main.MouseWorld - new Vector2(maxX, maxY) / 2;
                    Item ball = new Item(ModContent.ItemType<DarkMatterBall>());
                    DarkMatterBall darkMatterBall = (DarkMatterBall)ball.ModItem;
                    if (darkMatterBall != null) {
                        for (int x = 0; x < maxX; x++) {
                            for (int y = 0; y < maxY; y++) {
                                Vector2 tilePos = CWRUtils.WEPosToTilePos(pos + new Vector2(x, y));
                                Tile tile = CWRUtils.GetTile(tilePos);
                                if (tile.HasTile && tile.TileType != TileID.Cactus) {
                                    int dorptype = CWRUtils.GetTileDorp(tile);
                                    if (dorptype != 0)
                                        darkMatterBall.dorpTypes.Add(dorptype);

                                    tile.LiquidAmount = 0;
                                    tile.HasTile = false;
                                    CWRUtils.SafeSquareTileFrame(tilePos, tile);
                                    if (Main.netMode != NetmodeID.SinglePlayer) {
                                        NetMessage.SendTileSquare(player.whoAmI, x, y);
                                    }
                                }

                                if (tile.WallType != 0) {
                                    if (CWRLoad.WallToItem.TryGetValue(tile.WallType, out int value))
                                        darkMatterBall.dorpTypes.Add(value);

                                    tile.WallType = 0;

                                    if (Main.netMode != NetmodeID.SinglePlayer)
                                        NetMessage.SendTileSquare(player.whoAmI, x, y);
                                }
                            }
                        }
                        Projectile.NewProjectile(player.parent(), Main.MouseWorld, Vector2.Zero, ModContent.ProjectileType<InfinitePickProj>(), Item.damage * 10, 0, player.whoAmI);
                        if (darkMatterBall.dorpTypes.Count > 0)
                            player.QuickSpawnItem(player.parent(), darkMatterBall.Item, 1);
                    }
                }
                else {
                    int proj = Projectile.NewProjectile(player.parent(), player.Center, player.Center.To(Main.MouseWorld).UnitVector() * 32, ModContent.ProjectileType<InfinitePickProj>(), Item.damage * 10, 0, player.whoAmI, 1);
                    Main.projectile[proj].width = Main.projectile[proj].height = 64;
                }
            }
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                Vector2 basePosition = new Vector2(line.X, line.Y);
                string text = Language.GetTextValue("Mods.CalamityOverhaul.Items.InfinitePick.DisplayName");
                InfiniteIngot.drawColorText(Main.spriteBatch, line, text, basePosition);
                return false;
            }
            return true;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //HeavenHeavySmoke spark = new HeavenHeavySmoke(player.Center, Main.rand.NextVector2Unit() * Main.rand.Next(13, 17), CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors), 30, 1, 1, 0.1f);
            //CWRParticleHandler.AddParticle(spark);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CrystylCrusher>()
                .AddIngredient<DarkMatterBall>(12)
                .AddIngredient<InfiniteIngot>(18)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
