using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
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
            CreateRecipe().
            AddIngredient(ItemID.Furnace).
            AddIngredient(ItemID.Wood, 20).
            AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
            AddIngredient<CircuitBoard>(4).
            AddTile(TileID.Anvils).
            Register();

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

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out BiomassGeneratorTP generator)) {
                return;
            }
            //有机燃烧的暖橙光,亮度跟客户端的燃烧包络走
            if (generator.burnGlow > 0.05f) {
                r = 0.42f * generator.burnGlow;
                g = 0.26f * generator.burnGlow;
                b = 0.09f * generator.burnGlow;
            }
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

        #region 客户端视觉字段(不入存档不入网络包)

        /// <summary>平滑燃烧强度 0~1,驱动炉口火光与瓦片照明;各端本地模拟</summary>
        internal float burnGlow;
        private bool lastBurning;
        private bool visualInit;
        /// <summary>投料点火拍剩余帧,给火光一记过冲</summary>
        private int igniteTimer;
        private int smokeTimer;

        /// <summary>炉口:借热电机贴图,取机身中下部;真机贴图对位列在游戏内查验项</summary>
        private Vector2 MouthPos => PosInWorld + new Vector2(Width * 0.5f, Height * 0.66f);
        /// <summary>烟囱口:机身顶面偏左</summary>
        private Vector2 StackPos => PosInWorld + new Vector2(Width * 0.32f, -2f);

        #endregion

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

            //客户端视觉:炉口火光/烟囱烟/投料拍,由本地模拟的燃烧状态驱动,零网络
            if (!VaultUtils.isServer) {
                UpdateClientVisual();
            }
        }

        #region 客户端视觉

        /// <summary>
        /// 客户端视觉总更新。燃烧倒计时各端同规则本地推进,
        /// 所以"熄→燃"的翻转在每个客户端各自发生,投料拍不用发包
        /// </summary>
        private void UpdateClientVisual() {
            bool burning = BiomassData.IsBurning;

            //入世首帧对齐快照:存档里烧着的炉子直接热态呈现,不补播点火拍
            if (!visualInit) {
                visualInit = true;
                lastBurning = burning;
                burnGlow = burning ? 1f : 0f;
            }

            //投料点火拍只认"冷炉起火":生物质单份燃烧只有一两百tick,
            //链式续料的熄→燃翻转每两秒就有一次,炉膛既然还热就不该再敲锣
            if (burning && !lastBurning && burnGlow < 0.55f) {
                igniteTimer = 22;
                Defer(PlayIgniteEffect);
            }
            lastBurning = burning;

            burnGlow = MathHelper.Lerp(burnGlow, burning ? 1f : 0f, burning ? 0.05f : 0.02f);
            if (igniteTimer > 0) {
                igniteTimer--;
            }

            if (!InScreen) {
                return;
            }

            if (burning) {
                //烟囱烟:湿生物质烧出来的绿灰烟,慢升受风
                if (--smokeTimer <= 0) {
                    smokeTimer = Rand.Next(14, 26);
                    Vector2 stack = StackPos + new Vector2(Rand.NextFloat(-2f, 2f), 0f);
                    Vector2 vel = new(Main.windSpeedCurrent * 0.4f, -Rand.NextFloat(0.5f, 0.9f));
                    Color smokeColor = Color.Lerp(new Color(122, 130, 112), new Color(94, 114, 86), Rand.NextFloat());
                    float scale = Rand.NextFloat(0.16f, 0.26f);
                    int life = Rand.Next(100, 160);
                    Defer(() => PRTLoader.NewParticle<PRT_FarmSmoke>(stack, vel, smokeColor, scale).Configure(life));
                }
                //炉口零星火粒上飘
                if (Rand.NextBool(9)) {
                    Vector2 mouth = MouthPos + new Vector2(Rand.NextFloat(-5f, 5f), Rand.NextFloat(-2f, 2f));
                    Vector2 vel = new(Rand.NextFloat(-0.3f, 0.3f), -Rand.NextFloat(0.4f, 1.1f));
                    float scale = Rand.NextFloat(0.22f, 0.38f);
                    Defer(() => PRTLoader.NewParticle<PRT_LavaFire>(mouth, vel, Color.White, scale)
                        .SetLifetime(36, 66));
                }
            }
            else if (burnGlow > 0.25f && Rand.NextBool(30)) {
                //刚熄火的余烟,几团即散
                Vector2 stack = StackPos;
                Defer(() => PRTLoader.NewParticle<PRT_FarmSmoke>(stack, new Vector2(0f, -0.4f),
                    new Color(110, 116, 104), Rand.NextFloat(0.12f, 0.2f)).Configure(Rand.Next(70, 110)));
            }
        }

        /// <summary>投料点火拍:炉口火星腾起+短促燃点声;主线程调用</summary>
        private void PlayIgniteEffect() {
            if (!InScreen) {
                return;
            }
            Vector2 mouth = MouthPos;
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_LavaFire>(mouth + Main.rand.NextVector2Circular(5f, 3f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1.2f, 2.8f)),
                    Color.White, Main.rand.NextFloat(0.28f, 0.5f)).SetLifetime(26, 46);
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(mouth - new Vector2(4f, 4f), 8, 8, DustID.Torch, 0f, -2f, 100, default, 1.3f);
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.25f, Pitch = -0.5f }, mouth);
        }

        /// <summary>
        /// 炉口火光呼吸:两个不可通约频率叠加+高频微颤,有机燃烧偏暖橙,
        /// 外圈一层苔绿缘光与热电机的火划清;点火拍带一记过冲
        /// </summary>
        private void DrawFurnaceGlow(SpriteBatch spriteBatch) {
            if (burnGlow <= 0.03f) {
                return;
            }
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            float t = Main.GameUpdateCount + WhoAmI * 61;
            float breath = 0.72f + 0.17f * MathF.Sin(t * 0.094f) + 0.11f * MathF.Sin(t * 0.0417f);
            //两条高频正弦相乘近似炉膛低频颤
            float flick = 1f + 0.06f * MathF.Sin(t * 0.51f) * MathF.Sin(t * 0.173f);
            float ignite = igniteTimer > 0 ? 1f + igniteTimer / 22f * 0.8f : 1f;
            float k = burnGlow * breath * flick * ignite;

            Vector2 drawPos = MouthPos - Main.screenPosition;
            Vector2 origin = glowTex.Size() * 0.5f;
            //苔绿有机缘光在最外,是与热电机的分野色
            spriteBatch.Draw(glowTex, drawPos, null, new Color(150, 205, 110, 0) * (k * 0.2f), 0f, origin, 0.62f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glowTex, drawPos, null, new Color(230, 130, 42, 0) * (k * 0.55f), 0f, origin, 0.4f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glowTex, drawPos, null, new Color(255, 205, 110, 0) * (k * 0.8f), 0f, origin, 0.18f, SpriteEffects.None, 0f);
        }

        /// <summary>状态灯:烧着=呼吸,断料=琥珀双闪,有料待烧(电将满)=昏暗常亮</summary>
        private void DrawStatusLamp(SpriteBatch spriteBatch) {
            FarmLampState state;
            bool hasFuel = BiomassData.FuelItem != null && !BiomassData.FuelItem.IsAir;
            if (Disabled) {
                state = FarmLampState.Off;
            }
            else if (BiomassData.IsBurning || burnGlow > 0.5f) {
                //燃尽与续料之间隔着 1 tick 的空档,用平滑量兜住,灯不抖帧
                state = FarmLampState.Working;
            }
            else if (!hasFuel) {
                state = FarmLampState.MissingResource;
            }
            else {
                state = FarmLampState.Idle;
            }
            FarmStatusLamp.Draw(spriteBatch, PosInWorld + new Vector2(Width - 5f, 5f), state, BiomassGenerator.Tint, WhoAmI);
        }

        #endregion

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

        public override void Draw(SpriteBatch spriteBatch) {
            //待机冻结时燃烧被挂起,火光不呼吸
            if (Disabled) {
                return;
            }
            DrawFurnaceGlow(spriteBatch);
            DrawStatusLamp(spriteBatch);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
