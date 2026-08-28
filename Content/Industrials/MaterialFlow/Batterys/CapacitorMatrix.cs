using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys
{
    /// <summary>
    /// 电容矩阵:热能电池的上位大储能,8000 → 40000 UE,
    /// 均衡步长 8 让充放吞吐跟上体量。镜像 <see cref="ThermalBattery"/> 全套结构
    /// </summary>
    internal class CapacitorMatrix : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/CapacitorMatrix";

        /// <summary>系列色调:荧翠绿,取自罐窗熔核配色</summary>
        internal static readonly Color Tint = new(150, 230, 150);

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
            Item.value = Item.buyPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.Pink;
            Item.createTile = ModContent.TileType<CapacitorMatrixTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = CapacitorMatrixTP._maxUEValue;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient(ItemID.HallowedBar, 12).
            AddIngredient(ItemID.CrystalShard, 15).
            AddIngredient(ItemID.Glass, 50).
            AddIngredient<CircuitBoard>(15).
            AddTile(TileID.MythrilAnvil).
            Register();

        }
    }

    internal class CapacitorMatrixTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/CapacitorMatrixTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(88, 142, 88), VaultUtils.GetLocalizedItemName<CapacitorMatrix>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(2, 3);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 20];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<CapacitorMatrix>();
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;
    }

    /// <summary>
    /// 电容矩阵TP:被动储能(管道抽取),40000 UE;
    /// 均衡步长升到 8,充放吞吐匹配体量。
    /// 注意它仍是超频协议的合法目标:超频预算按容量的四倍计,
    /// 到期一次性烧空 40000 存量同样是明码标价的代价,权衡见工业扩展文档 §2.8。<br/>
    /// 视觉(纯客户端):顶端子间电弧随充能度跳动(复用 <see cref="PRT_TeslaArc"/>
    /// ThunderTrail 管线),满电电晕呼吸+上冲小弧,充放电方向驱动核心流光吸入/呼出
    /// </summary>
    internal class CapacitorMatrixTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<CapacitorMatrixTile>();
        public override int TargetItem => ModContent.ItemType<CapacitorMatrix>();
        internal float oldUEValue;
        internal int activeTime;
        internal const float _maxUEValue = 40000;
        public override float MaxUEValue => _maxUEValue;
        internal bool fullLoad;
        //熔核显示比例与初始化标记
        internal float displayRatio;
        private bool ratioInited;

        /// <summary>电弧亮色,比系列色调更白热一档</summary>
        private static readonly Color ArcTint = new(222, 255, 222);
        /// <summary>端子电弧计时:充能越满越频</summary>
        private int arcTimer;
        /// <summary>充放方向 -1..1:+1 充入 -1 放出,平滑防抖</summary>
        internal float flowDir;

        public override void SetBattery() {
            //大容量储能的充放吞吐:均衡步长从 2 提到 8
            Efficiency = 8;
        }

        public override void UpdateMachine() {
            fullLoad = MachineData.UEvalue >= MaxUEValue;
            if (activeTime > 0) {
                activeTime--;
            }

            float ratio = MachineData.UEvalue / MaxUEValue;
            if (!ratioInited) {
                displayRatio = ratio;
                ratioInited = true;
            }
            else {
                displayRatio = MathHelper.Lerp(displayRatio, ratio, 0.1f);
            }
            //充放方向取自真实 UE 变化率符号,先算后写 oldUEValue
            float delta = MachineData.UEvalue - oldUEValue;
            if (oldUEValue != MachineData.UEvalue) {
                activeTime = 60;
                oldUEValue = MachineData.UEvalue;
            }
            float flowTarget = Math.Abs(delta) > 0.01f ? Math.Sign(delta) : 0f;
            flowDir = MathHelper.Lerp(flowDir, flowTarget, 0.05f);

            UpdateArcVisual();
        }

        #region 电弧表现(纯客户端)

        /// <summary>端子间电弧+满电上冲小弧;屏外不发,服务端不跑</summary>
        private void UpdateArcVisual() {
            if (VaultUtils.isServer || !InScreen) {
                return;
            }

            //端子间跳弧:频率随充能度爬升,近空不放
            if (displayRatio > 0.08f && --arcTimer <= 0) {
                arcTimer = (int)MathHelper.Lerp(96f, 16f, MathHelper.Clamp(displayRatio, 0f, 1f)) + Rand.Next(12);
                Defer(SpawnTerminalArc);
            }

            //满电电晕:偶发自端子向上的小弧,配合 Draw 里的呼吸辉光
            if (fullLoad && Rand.NextBool(34)) {
                Defer(SpawnCoronaArc);
            }

            //活跃照明:晶紫,亮度随充能度
            if (activeTime > 0 || fullLoad) {
                Defer(() => Lighting.AddLight(CenterInWorld,
                    CapacitorMatrix.Tint.ToVector3() * (0.10f + 0.28f * displayRatio)));
            }
        }

        /// <summary>两只顶端子之间的一道跳弧:两端钉死,中段拱起随机摆</summary>
        private void SpawnTerminalArc() {
            Vector2 t1 = PosInWorld + new Vector2(16f, 5f);
            Vector2 t2 = PosInWorld + new Vector2(Width - 16f, 5f);
            int pointCount = Main.rand.Next(5, 8);
            Vector2[] path = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++) {
                float t = i / (float)(pointCount - 1);
                //中段向上拱+法向随机摆,电走最短路却不走直线
                float arch = MathF.Sin(t * MathHelper.Pi) * Main.rand.NextFloat(2f, 7f);
                path[i] = Vector2.Lerp(t1, t2, t) - new Vector2(0f, arch)
                    + new Vector2(0f, Main.rand.NextFloat(-2.5f, 2.5f));
            }
            PRTLoader.NewParticle<PRT_TeslaArc>(path[pointCount / 2], Vector2.Zero, ArcTint, 1f)
                ?.Configure(path, Main.rand.Next(7, 13), Main.rand.NextFloat(3.5f, 6f)
                    * (0.6f + 0.6f * displayRatio), (0f, 4f), 3f);

            //端点微火花
            PRTLoader.NewParticle<PRT_GraniteVolt>(Main.rand.NextBool() ? t1 : t2,
                Main.rand.NextVector2Circular(1.2f, 0.8f), ArcTint,
                Main.rand.NextFloat(0.16f, 0.28f))?.Configure(Main.rand.Next(2, 5));
        }

        /// <summary>满电上冲小弧:从随机顶点向天钉一小段,电多得往外冒</summary>
        private void SpawnCoronaArc() {
            Vector2 from = PosInWorld + new Vector2(Main.rand.NextFloat(6f, Width - 6f), 2f);
            Vector2 to = from - new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(10f, 20f));
            Vector2[] path = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                path[i] = Vector2.Lerp(from, to, i / 3f) + Main.rand.NextVector2Circular(2f, 1f);
            }
            PRTLoader.NewParticle<PRT_TeslaArc>(path[2], Vector2.Zero, ArcTint, 1f)
                ?.Configure(path, Main.rand.Next(6, 10), Main.rand.NextFloat(2.5f, 4f), (0f, 3f), 2f);
        }

        #endregion

        #region 电晕与流光绘制(实体批内)

        /// <summary>熔核电量辉光 + 满电电晕呼吸 + 充放电方向流光(向心吸入=充,离心呼出=放)</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }

            //熔核窗辉光:贴图已画实心熔核,辉光只做电量反馈
            float coreRatio = MathHelper.Clamp(displayRatio, 0f, 1f);
            if (coreRatio > 0.02f) {
                float coreBreath = 1f + 0.14f * MathHelper.Clamp(activeTime / 60f, 0f, 1f)
                    * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.2f + Position.X * 0.7f);
                Vector2 coreP = PosInWorld + ThermalBatteryTP.CoreCenter - Main.screenPosition;
                float coreSize = 28f + 16f * coreRatio;
                spriteBatch.Draw(glow, coreP, null, (CapacitorMatrix.Tint with { A = 0 }) * ((0.20f + 0.45f * coreRatio) * coreBreath),
                    0f, glow.Size() * 0.5f, coreSize / glow.Width, SpriteEffects.None, 0f);
            }

            //满电电晕:两只顶端子上的呼吸辉光,A=0 加色不落黑块
            if (fullLoad || displayRatio > 0.96f) {
                float breath = 0.62f + 0.38f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f
                    + Position.X * 0.31f);
                Color corona = CapacitorMatrix.Tint with { A = 0 };
                Vector2 t1 = PosInWorld + new Vector2(16f, 6f) - Main.screenPosition;
                Vector2 t2 = PosInWorld + new Vector2(Width - 16f, 6f) - Main.screenPosition;
                float s = 0.34f + 0.10f * breath;
                spriteBatch.Draw(glow, t1, null, corona * (0.5f * breath), 0f, glow.Size() * 0.5f, s, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, t2, null, corona * (0.5f * breath), 0f, glow.Size() * 0.5f, s, SpriteEffects.None, 0f);
            }

            //充放电流光:六粒游光绕核径向进/出,方向即真实功率流向
            float dirAbs = MathF.Abs(flowDir);
            if (dirAbs > 0.15f && activeTime > 0) {
                Vector2 core = PosInWorld + new Vector2(Width * 0.5f, Height * 0.5f) - Main.screenPosition;
                Color streak = CapacitorMatrix.Tint with { A = 0 };
                for (int i = 0; i < 6; i++) {
                    float lane = i / 6f;
                    float phase = (Main.GlobalTimeWrappedHourly * 0.9f + lane * 1.31f) % 1f;
                    //充电向心(半径收),放电离心(半径涨)
                    float radial = flowDir > 0f ? 1f - phase : phase;
                    float radius = MathHelper.Lerp(7f, 27f, radial);
                    float ang = lane * MathHelper.TwoPi + Main.GlobalTimeWrappedHourly * 0.35f;
                    Vector2 pos = core + ang.ToRotationVector2() * radius * new Vector2(1f, 1.25f);
                    float alpha = MathF.Sin(phase * MathHelper.Pi) * dirAbs * 0.55f;
                    spriteBatch.Draw(glow, pos, null, streak * alpha, 0f, glow.Size() * 0.5f,
                        0.10f, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
