using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal class MermanRod : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ADV/Abysse/MermanRod";
        public static LocalizedText Text1;
        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "世界中已经放置了一座鱼人钓!");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.Green;
            Item.createTile = ModContent.TileType<MermanRodTile>();
        }

        public override bool CanUseItem(Player player) {
            if (TileProcessorLoader.TP_ID_To_InWorld_Count.TryGetValue(TPUtils.GetID<MermanRodTP>(), out var num) && num > 0) {
                if (Main.mouseLeftRelease)
                    CombatText.NewText(player.getRect(), Color.Cyan, Text1.Value);
                return false;
            }
            return base.CanUseItem(player);
        }
    }

    internal class MermanRodTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ADV/Abysse/MermanRodTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<MermanRod>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 5;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(2, 4);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
            HitSound = SoundID.Dig;
            MineResist = 4f;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.WoodFurniture);
            return false;
        }
    }

    internal class MermanRodTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<MermanRodTile>();
        public override void Update() {
            UpdateOldDukeRelocation();
            UpdateFishingBuff();
        }

        private void UpdateOldDukeRelocation() {
            if (OldDukeCampsite.CampsitePosition == PosInWorld) {
                return;
            }
            bool inView = false;
            foreach (var player in Main.player) {
                if (!player.active) continue;
                Rectangle screenRect = new Rectangle((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
                //扩大视野判定，防边缘闪
                screenRect.Inflate(200, 200);
                if (screenRect.Contains(PosInWorld.ToPoint())) {
                    inView = true;
                    break;
                }
            }
            if (!inView) {
                //清旧营地再生成
                OldDukeCampsite.ClearCampsiteAndSync();

                //搬家跳过箱子
                if (VaultUtils.isServer) {
                    OldDukeCampsite.GenerateCampsite(PosInWorld, isRelocation: true);
                    ModPacket packet = CWRNetWork.GetPacket<OldDukeCampsiteSyncNet>();
                    packet.Write(true);
                    packet.WriteVector2(PosInWorld);
                    packet.Send();
                }
                else if (VaultUtils.isSinglePlayer) {
                    OldDukeCampsite.GenerateCampsite(PosInWorld, isRelocation: true);
                }
            }
        }

        private void UpdateFishingBuff() {
            foreach (var player in Main.player) {
                if (!player.active || player.dead) continue;

                if (Vector2.Distance(player.Center, PosInWorld) < 600) {
                    player.fishingSkill += 20;
                    if (Main.rand.NextBool(60)) {
                        Dust.NewDust(player.position, player.width, player.height, DustID.Water, 0, -2, 0, default, 1.2f);
                    }
                }
            }
        }

        public override void OnKill() {
            OldDukeCampsite.MermanRodMoveback = true;
            //多人立刻清；单人等远离(ShouldGenerateCampsite)
            if (VaultUtils.isServer) {
                OldDukeCampsite.ClearCampsiteAndSync();
            }
        }
    }
}
