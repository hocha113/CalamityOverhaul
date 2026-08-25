using CalamityOverhaul.Content.Industrials.MachineModules;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.Biomass
{
    /// <summary>
    /// 生物质发电机:专烧农业废料流(种子/草药/蘑菇/鱼获/凝胶)的早期发电机。
    /// 定位在风电与热电之间:无 Boss 门槛,平功率无温度曲线,
    /// 与蘑菇农场机/史莱姆培养槽构成产烧闭环。贴图复用热电机,靠苔绿色调区分
    /// </summary>
    internal class BiomassGenerator : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGenerator";

        /// <summary>系列色调:苔绿,同贴图靠它与热电机区分</summary>
        internal static readonly Color Tint = new(150, 205, 110);

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 0, 40, 0);
            Item.rare = ItemRarityID.Green;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<BiomassGeneratorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.Furnace).
                AddIngredient(ItemID.Wood, 20).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
                AddIngredient(CWRID.Item_DubiousPlating, 6).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 4).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.Furnace).
                AddIngredient(ItemID.Wood, 20).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 8).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    internal class BiomassGeneratorTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGeneratorTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<BiomassGeneratorTP>();
        public override int GeneratorUI => UIHandleLoader.GetUIHandleID<BiomassGeneratorUI>();
        public override int TargetItem => ModContent.ItemType<BiomassGenerator>();

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(96, 130, 70), VaultUtils.GetLocalizedItemName<BiomassGenerator>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Item item = Main.LocalPlayer.GetItem();
            int type = TargetItem;
            if (BiomassFuel.IsBiomass(item.type)) {
                type = item.type;
            }
            Main.LocalPlayer.SetMouseOverByTile(type);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out BiomassGeneratorTP generator)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += generator.frame * 2 * 18;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            //共用热电机贴图,乘上苔绿色调区分机种
            Color drawColor = Lighting.GetColor(i, j).MultiplyRGB(BiomassGenerator.Tint);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class BiomassGeneratorTP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<BiomassGeneratorTile>();
        public override int TargetItem => ModContent.ItemType<BiomassGenerator>();
        public override float MaxUEValue => 500 * ModuleRack.StorageMult;
        public override MachineModuleTarget ModuleHostKind => MachineModuleTarget.BiomassGenerator;
        public override int ModuleSlotCount => 2;

        internal int frame;
        internal BiomassData BiomassData => MachineData as BiomassData;
        public int MaxFrame = 4;
        /// <summary>自动进料节拍</summary>
        private int autoFeedTimer;

        public override MachineData GetGeneratorDataInds() {
            var data = new BiomassData();
            data.MaxUEValue = MaxUEValue;
            data.PowerPerTick = 0.6f;
            return data;
        }

        /// <summary>UI燃料槽放入/取出/交换,含类型校验;客户端权威编辑,改完推送</summary>
        internal void HandlerItem() {
            Item mouseItem = Main.mouseItem;
            bool mouseHasFuel = !mouseItem.IsAir && BiomassFuel.IsBiomass(mouseItem.type);

            if (BiomassData.FuelItem.IsAir) {
                //空槽只收生物质
                if (mouseHasFuel) {
                    BiomassData.FuelItem = mouseItem.Clone();
                    mouseItem.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Grab);
                }
            }
            else if (mouseItem.IsAir) {
                //取出
                Main.mouseItem = BiomassData.FuelItem.Clone();
                BiomassData.FuelItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else if (mouseItem.type == BiomassData.FuelItem.type) {
                //同种堆叠
                int canAdd = BiomassData.FuelItem.maxStack - BiomassData.FuelItem.stack;
                int toAdd = canAdd < mouseItem.stack ? canAdd : mouseItem.stack;
                if (toAdd > 0) {
                    BiomassData.FuelItem.stack += toAdd;
                    mouseItem.stack -= toAdd;
                    if (mouseItem.stack <= 0) mouseItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else if (mouseHasFuel) {
                //异种交换
                Item temp = BiomassData.FuelItem.Clone();
                BiomassData.FuelItem = mouseItem.Clone();
                Main.mouseItem = temp;
                SoundEngine.PlaySound(SoundID.Grab);
            }

            SendData();
        }

        /// <summary>条件满足时消耗一份燃料开烧;电快满时不点新料,避免白烧</summary>
        private void TryConsumeFuel() {
            if (BiomassData.FuelItem == null || BiomassData.FuelItem.IsAir) return;
            if (!BiomassFuel.BiomassToCombustion.TryGetValue(BiomassData.FuelItem.type, out int combustion)) return;
            if (BiomassData.UEvalue >= BiomassData.MaxUEValue * 0.99f) return;

            //燃烧时长沿用热电的 sqrt 缩放,总出电 = 时长 × 平功率
            int burnDuration = FuelItems.GetBurnDuration(combustion);
            BiomassData.BurnTimeRemaining = burnDuration;
            BiomassData.BurnTimeMax = burnDuration;

            BiomassData.FuelItem.stack--;
            if (BiomassData.FuelItem.stack <= 0) {
                BiomassData.FuelItem.TurnToAir();
            }
        }

        public sealed override void GeneratorUpdate() {
            //UI与近距标记只对本地端有意义,专用服务器上LocalPlayer是占位实例
            if (!VaultUtils.isServer) {
                if (PosInWorld.Distance(Main.LocalPlayer.Center) > MaxFindMode) {
                    if (GeneratorUI?.GeneratorTP == this
                        && UIHandleLoader.GetUIHandleOfType<BiomassGeneratorUI>().IsActive) {
                        UIHandleLoader.GetUIHandleOfType<BiomassGeneratorUI>().IsActive = false;
                        //并行阶段延后到主线程
                        Defer(() => SoundEngine.PlaySound(SoundID.MenuTick));
                    }
                }
            }

            //储能扩容模块动上限,数据侧字段每帧对齐
            BiomassData.MaxUEValue = MaxUEValue;

            //平功率发电:烧着就出电,输出模块可放大
            if (BiomassData.IsBurning) {
                BiomassData.BurnTimeRemaining--;
                float power = BiomassData.PowerPerTick * ModuleRack.GenOutputMult;
                if (BiomassData.UEvalue < BiomassData.MaxUEValue) {
                    float availableCapacity = BiomassData.MaxUEValue - BiomassData.UEvalue;
                    BiomassData.UEvalue += power < availableCapacity ? power : availableCapacity;
                }
                VaultUtils.ClockFrame(ref frame, 5, MaxFrame, 1);
            }
            else {
                frame = 0;
                TryConsumeFuel();
            }

            //自动进料斗:燃料槽空了就从近旁存储补一批(权威端,主线程经 Defer)
            if (!VaultUtils.isClient && ModuleRack.AutoFeed && ++autoFeedTimer >= 30) {
                autoFeedTimer = 0;
                if (BiomassData.FuelItem == null || BiomassData.FuelItem.IsAir) {
                    Defer(() => {
                        if (BiomassData.FuelItem != null && !BiomassData.FuelItem.IsAir) {
                            return;
                        }
                        Item got = MachineLogistics.TryWithdraw(Position,
                            stored => BiomassFuel.IsBiomass(stored.type), 15);
                        if (!got.IsAir) {
                            BiomassData.FuelItem = got;
                            SendData();
                        }
                    });
                }
            }
        }

        public override void GeneratorKill() {
            if (!VaultUtils.isClient && BiomassData.FuelItem != null && !BiomassData.FuelItem.IsAir) {
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, BiomassData.FuelItem.Clone());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }

            BiomassData.FuelItem.TurnToAir();

            if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this
                    && UIHandleLoader.GetUIHandleOfType<BiomassGeneratorUI>().IsActive) {
                UIHandleLoader.GetUIHandleOfType<BiomassGeneratorUI>().IsActive = false;
            }
        }

        public override void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                if (!BiomassData.FuelItem.IsAir) {
                    //直接入背包,MP下QuickSpawnItem是地面掉落会被队友截走
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), BiomassData.FuelItem.Clone());
                    BiomassData.FuelItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (item.IsAir || !BiomassFuel.IsBiomass(item.type)) {
                return;
            }

            //同种堆叠
            if (!BiomassData.FuelItem.IsAir && BiomassData.FuelItem.type == item.type) {
                int canAdd = BiomassData.FuelItem.maxStack - BiomassData.FuelItem.stack;
                int toAdd = canAdd < item.stack ? canAdd : item.stack;
                if (toAdd > 0) {
                    BiomassData.FuelItem.stack += toAdd;
                    item.stack -= toAdd;
                    if (item.stack <= 0) item.TurnToAir();
                }
            }
            //异种先吐再放(旧燃料直接回背包)
            else if (!BiomassData.FuelItem.IsAir) {
                Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), BiomassData.FuelItem.Clone());
                BiomassData.FuelItem = item.Clone();
                item.TurnToAir();
            }
            else {
                BiomassData.FuelItem = item.Clone();
                item.TurnToAir();
            }

            SendData();
            SoundEngine.PlaySound(SoundID.Grab);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
