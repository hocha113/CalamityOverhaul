using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.MagmaThermal
{
    /// <summary>岩浆热能发电机:液体网络的首个耗液发电机(占位贴图沿用热能电池,待专属美术)</summary>
    internal class MagmaThermalGenerator : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryLegacy";
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
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<MagmaThermalGeneratorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 2000;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient(ItemID.HellstoneBar, 12).
            AddIngredient(ItemID.Obsidian, 20).
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddIngredient<CircuitBoard>(10).
            AddTile(TileID.Anvils).
            Register();

        }
    }

    internal class MagmaThermalGeneratorTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryLegacyTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<MagmaThermalGeneratorTP>();
        public override int GeneratorUI => UIHandleLoader.GetUIHandleID<GeneratorReadoutUI>();
        public override int TargetItem => ModContent.ItemType<MagmaThermalGenerator>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(128, 64, 32), VaultUtils.GetLocalizedItemName<MagmaThermalGenerator>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Item item = Main.LocalPlayer.GetItem();
            int type = TargetItem;
            if (item.type == ItemID.LavaBucket || item.type == ItemID.BottomlessLavaBucket) {
                type = item.type;
            }
            Main.LocalPlayer.SetMouseOverByTile(type);
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out MagmaThermalGeneratorTP tp)) {
                return;
            }
            if (tp.WorkLevel > 0f) {
                r = 0.8f * tp.WorkLevel;
                g = 0.35f * tp.WorkLevel;
                b = 0.05f * tp.WorkLevel;
            }
        }
    }

    /// <summary>
    /// 岩浆热能发电机TP:双模式发电。
    /// 被动:机体接触世界岩浆时低功率运转(免费弱电,插在地狱就能用);
    /// 主动:内部岩浆罐有储浆时全功率运转,储浆经液体管道供给或右键岩浆桶倒入。
    /// 液体角色为耗液机,液管只进不出
    /// </summary>
    internal class MagmaThermalGeneratorTP : BaseGeneratorTP, IFluidContainer, IGeneratorReadout
    {
        public override int TargetTileID => ModContent.TileType<MagmaThermalGeneratorTile>();
        public override int TargetItem => ModContent.ItemType<MagmaThermalGenerator>();
        public override float MaxUEValue => 2000 * ModuleRack.StorageMult;
        public override MachineModuleTarget ModuleHostKind => MachineModuleTarget.MagmaGenerator;
        public override int ModuleSlotCount => 2;

        #region 读数板:工况与 GeneratorUpdate 同一组状态字段推导
        public GeneratorReadoutKind ReadoutKind => GeneratorReadoutKind.Magma;
        /// <summary>当前基础功率:烧储浆全功率,贴浆被动低功率,否则停摆</summary>
        private float CurrentBasePower => FluidAmount > 0 ? ActivePower : touchingLava ? PassivePower : 0f;
        /// <summary>工况比 = 当前功率对满功率(主动 1,被动 0.16,停摆 0)</summary>
        public float ConditionRatio => CurrentBasePower / ActivePower;
        public bool ConditionOk => CurrentBasePower > 0f;
        public float OutputPerSecond => CurrentBasePower * ModuleRack.GenOutputMult * 60f;
        #endregion

        #region 液体容器契约:只收岩浆
        public int FluidType { get; set; } = LiquidID.Lava;
        public int FluidAmount { get; set; }
        public int FluidCapacity => 4 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Consumer;
        public bool CanAcceptFluid(int liquidId)
            => liquidId == LiquidID.Lava && FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>被动模式功率(UE/tick),机体接触世界岩浆</summary>
        internal const float PassivePower = 0.4f;
        /// <summary>主动模式功率(UE/tick),烧内部储浆</summary>
        internal const float ActivePower = 2.5f;
        /// <summary>主动模式岩浆消耗:255 单位烧 600 帧</summary>
        internal const float LavaPerTick = FluidHelper.UnitsPerTile / 600f;
        /// <summary>被动接触检测节流(帧)</summary>
        private const int LavaScanInterval = 30;

        /// <summary>当前工况 0..1,喂给物块发光与显示</summary>
        internal float WorkLevel;

        private bool touchingLava;
        private int lavaScanTimer;
        /// <summary>岩浆消耗的小数累加器</summary>
        private float lavaAcc;

        #region 纯客户端表现状态(炉膛呼吸/烬/烟)
        private float animTime;
        private int emberTimer;
        private int smokeTimer;
        #endregion

        public override void GeneratorUpdate() {
            if (!Main.dedServ) {
                UpdateFurnaceVisual();
            }

            //被动接触检测按节拍缓存,扫描只读并行安全
            if (--lavaScanTimer <= 0) {
                lavaScanTimer = LavaScanInterval;
                touchingLava = ScanLavaContact();
            }

            float power = 0f;
            if (FluidAmount > 0) {
                //主动:烧储浆全功率
                power = ActivePower;
                lavaAcc += LavaPerTick;
                int steps = (int)lavaAcc;
                lavaAcc -= steps;
                if (steps > 0) {
                    FluidAmount -= steps;
                    if (FluidAmount < 0) {
                        FluidAmount = 0;
                    }
                }
            }
            else if (touchingLava) {
                //被动:贴浆低功率
                power = PassivePower;
            }

            if (power > 0f) {
                power *= ModuleRack.GenOutputMult;
                float availableCapacity = MaxUEValue - MachineData.UEvalue;
                if (availableCapacity > 0f) {
                    MachineData.UEvalue += power < availableCapacity ? power : availableCapacity;
                }
                WorkLevel = MathHelper.Lerp(WorkLevel, FluidAmount > 0 ? 1f : 0.4f, 0.05f);
            }
            else {
                WorkLevel = MathHelper.Lerp(WorkLevel, 0f, 0.03f);
            }
        }

        #region 表现推进(纯客户端,零网络)
        /// <summary>
        /// 烧浆=熔炉全开:烬粒上浮+浓烟;贴浆被动=弱版只余零星烬。
        /// 全部挂在 WorkLevel/touchingLava/FluidAmount 真实状态字段上
        /// </summary>
        private void UpdateFurnaceVisual() {
            animTime += 1f / 60f;
            if (WorkLevel <= 0.05f || !FluidVFX.NearLocalPlayer(CenterInWorld)) {
                return;
            }

            bool activeBurn = FluidAmount > 0;
            //烬粒:热浮对流,自炉体上沿冒出
            int emberInterval = activeBurn ? 9 : 30;
            if (++emberTimer >= emberInterval) {
                emberTimer = 0;
                Vector2 spawn = new(PosInWorld.X + Main.rand.NextFloat(8f, Width - 8f), PosInWorld.Y + 4f);
                Defer(() => {
                    var ember = PRTLoader.NewParticle<PRT_SHPCThermalEmber>(spawn,
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1.0f)),
                        new Color(255, 190, 90), Main.rand.NextFloat(0.5f, 0.9f));
                    ember?.Configure(new Color(120, 40, 28), Main.rand.Next(30, 50));
                });
            }
            //浓烟:只在烧浆时,机顶缓升的暗烟(Fog 真 alpha 可染暗色)
            if (activeBurn && ++smokeTimer >= 16) {
                smokeTimer = 0;
                Vector2 spawn = new(PosInWorld.X + Main.rand.NextFloat(10f, Width - 10f), PosInWorld.Y + 2f);
                Defer(() => {
                    PRTLoader.NewParticle<PRT_FluidSteam>(spawn, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.5f),
                        new Color(52, 38, 34) * 0.55f, Main.rand.NextFloat(0.16f, 0.26f))
                        ?.Configure(Main.rand.Next(50, 80), 0.035f);
                });
            }
        }
        #endregion

        #region 机面覆层:储浆液窗(物块下)+炉膛熔光呼吸与熔缝(物块上)

        //储浆窗本地 UV,对齐遗留电池贴图的透明窗
        private static readonly Vector2 ChamberMin = new(0.21f, 0.20f);
        private static readonly Vector2 ChamberMax = new(0.71f, 0.82f);

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            //储浆窗:与储罐同一支液窗笔,岩浆材质(熔泡慢涌+黑壳浮斑)
            Vector2 basePos = PosInWorld - Main.screenPosition;
            Rectangle chamber = new(
                (int)(basePos.X + ChamberMin.X * Width),
                (int)(basePos.Y + ChamberMin.Y * Height),
                (int)((ChamberMax.X - ChamberMin.X) * Width),
                (int)((ChamberMax.Y - ChamberMin.Y) * Height));
            FluidVFX.DrawLiquidWindow(spriteBatch, chamber, LiquidID.Lava,
                MathHelper.Clamp(FluidAmount / (float)FluidCapacity, 0f, 1f), animTime,
                WorkLevel * 0.5f, WhoAmI + 29);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //待机冻结时 WorkLevel 不再衰减,这里显式断掉炉膛光
            if (WorkLevel <= 0.03f || Disabled) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 basePos = PosInWorld - Main.screenPosition;
            FluidStyle style = FluidVFX.GetStyle(LiquidID.Lava);

            //炉膛熔光:双层呼吸辉光压在机体下部,烧浆满亮,贴浆被动是弱版
            float breath = 0.82f + 0.18f * MathF.Sin(animTime * 2.4f + WhoAmI);
            Vector2 furnace = new(basePos.X + Width / 2f, basePos.Y + Height * 0.72f);
            spriteBatch.Draw(glow, furnace, null, FluidVFX.Glow(style.Main, 0.55f * WorkLevel * breath),
                0f, glow.Size() * 0.5f, 1.05f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, furnace, null, FluidVFX.Glow(style.Bright, 0.35f * WorkLevel * breath),
                0f, glow.Size() * 0.5f, 0.5f, SpriteEffects.None, 0f);

            //底缘熔缝:两道错相闪烁的亮线,读作炉体接缝透光
            for (int i = 0; i < 2; i++) {
                float flicker = 0.5f + 0.5f * MathF.Sin(animTime * (3.1f + i * 1.7f) + i * 2.4f + WhoAmI);
                int seamY = (int)(basePos.Y + Height) - 4 - i * 5;
                int seamW = (int)(Width * (0.5f - i * 0.14f));
                spriteBatch.Draw(px, new Rectangle((int)(basePos.X + (Width - seamW) / 2f), seamY, seamW, 1),
                    FluidVFX.Glow(style.Bright, (0.3f + 0.4f * flicker) * WorkLevel));
            }
        }
        #endregion

        /// <summary>机体及外缘一圈是否接触世界岩浆</summary>
        private bool ScanLavaContact() {
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            for (int i = Position.X - 1; i <= Position.X + tileWidth; i++) {
                for (int j = Position.Y - 1; j <= Position.Y + tileHeight; j++) {
                    Tile tile = Framing.GetTileSafely(i, j);
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>右键交互(交互客户端执行):岩浆桶倒入储浆,无尽岩浆桶不耗桶</summary>
        public override void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();
            if (item.IsAir) {
                return;
            }

            bool bottomless = item.type == ItemID.BottomlessLavaBucket;
            if (item.type != ItemID.LavaBucket && !bottomless) {
                return;
            }
            if (FluidCapacity - FluidAmount < FluidHelper.UnitsPerTile) {
                return;
            }

            FluidType = LiquidID.Lava;
            FluidAmount += FluidHelper.UnitsPerTile;
            if (!bottomless) {
                item.stack--;
                if (item.stack <= 0) {
                    item.TurnToAir();
                }
                Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), new Item(ItemID.EmptyBucket));
            }

            SendData();
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.4f });
        }

        #region 存档与同步:液体字段追加在基类(含模块架)之后
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write((byte)FluidType);
            data.Write(FluidAmount);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            FluidType = reader.ReadByte();
            FluidAmount = reader.ReadInt32();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["FluidType"] = FluidType;
            tag["FluidAmount"] = FluidAmount;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Lava;
            FluidAmount = tag.TryGet("FluidAmount", out int amount) ? amount : 0;
        }
        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
            if (HoverTP) {
                FluidHelper.DrawFluidBar(this, this);
            }
        }
    }
}
