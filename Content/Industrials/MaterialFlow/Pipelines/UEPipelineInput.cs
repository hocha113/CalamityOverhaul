using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Industrials.Generator;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    internal class UEPipelineInput : BasePipelineItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/UEPipelineInput";
        public override int CreateTileID => ModContent.TileType<UEPipelineInputTile>();
        public override void AddRecipes() {
            CreateRecipe(333).
                AddIngredient<DubiousPlating>(5).
                AddIngredient<MysteriousCircuitry>(5).
                AddIngredient(ItemID.GoldBar).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class UEPipelineInputTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/UEPipelineInput";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<UEPipelineInput>());
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);
        }
        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.GreenTorch);
            return false;
        }
        public override void MouseOver(int i, int j) {
            Player localPlayer = Main.LocalPlayer;
            localPlayer.cursorItemIconEnabled = true;
            localPlayer.cursorItemIconID = ModContent.ItemType<UEPipelineInput>();
            localPlayer.noThrow = 2;
        }
        public override bool CanDrop(int i, int j) => false;
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }

    internal class SideStateInput(Point16 point16)
    {
        internal Point16 Position;
        internal readonly Point16 Offset = point16;
        /// <summary>
        /// 对于外部的物块数据
        /// </summary>
        internal Tile externalTile;
        /// <summary>
        /// 对于外部的TP实体
        /// </summary>
        internal TileProcessor externalTP;
        /// <summary>
        /// 自身的核心TP实体
        /// </summary>
        internal UEPipelineInputTP coreTP;
        /// <summary>
        /// 链接ID
        /// </summary>
        internal int linkID = 0;
        internal bool canDraw;
        /// <summary>
        /// 更新逻辑
        /// </summary>
        public void Update() {
            // 初始化
            externalTile = default;
            externalTP = null;
            linkID = 0;
            canDraw = true;
            // 获取当前 Tile 和相邻的 TileProcessor
            externalTile = Framing.GetTileSafely(Position + Offset);

            if (externalTile.HasTile
                && VaultUtils.SafeGetTopLeft(Position + Offset, out var point)
                && TileProcessorLoader.ByPositionGetTP(point, out externalTP)) {
                // 如果相邻的 TileProcessor 是发电机
                if (externalTP is BaseGeneratorTP baseGeneratorTP) {
                    // 如果发电机的 UEvalue 大于 0，从发电机到管道传递值
                    if (baseGeneratorTP.MachineData.UEvalue < baseGeneratorTP.MaxUEValue && coreTP.MachineData.UEvalue > 0) {
                        baseGeneratorTP.MachineData.UEvalue++;  // 从发电机增加能量
                        coreTP.MachineData.UEvalue--;      // 给管道减去能量
                    }
                    linkID = 1;
                }

                // 如果有能量传递的需求，且相邻的是管道
                if (externalTP is UEPipelineInputTP battery) {
                    // 计算总能量
                    float totalUE = coreTP.MachineData.UEvalue + battery.MachineData.UEvalue;

                    // 计算均衡后应该有多少能量
                    float averageUE = totalUE / 2;

                    // 直接平衡
                    coreTP.MachineData.UEvalue = averageUE;
                    battery.MachineData.UEvalue = averageUE;

                    if (battery.Decussation || battery.Turning) {
                        canDraw = false;
                    }

                    linkID = 2;
                }

                //如果挨着的是电池
                else if (externalTP is BaseBattery baseBattery) {
                    if (baseBattery.ReceivedEnergy) {//首先判断是否是一个需要被抽取能量的电源
                        if (baseBattery.MachineData.UEvalue < baseBattery.MaxUEValue && coreTP.MachineData.UEvalue > 0) {
                            baseBattery.MachineData.UEvalue++;
                            coreTP.MachineData.UEvalue--;
                        }
                    }
                    else if (baseBattery.MachineData.UEvalue > 0 && coreTP.MachineData.UEvalue < coreTP.MaxUEValue) {
                        baseBattery.MachineData.UEvalue--;
                        coreTP.MachineData.UEvalue++;
                    }

                    linkID = 3;
                }
            }

            if (linkID == 0) {
                canDraw = false;
            }
        }

        public void Draw(SpriteBatch spriteBatch) {
            if (coreTP == null || coreTP.MachineData == null || externalTP == null) {
                return;
            }

            Vector2 drawPos = coreTP.PosInWorld + Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = Offset.ToVector2().ToRotation();

            Vector2 orig = UEPipelineInputTP.PipelineChannel.Size() / 2;
            Color color = Color.White * (coreTP.MachineData.UEvalue / 10f);

            spriteBatch.Draw(UEPipelineInputTP.PipelineChannel.Value, drawPos + orig, null, color
                , drawRot, orig, 1, SpriteEffects.None, 0);

            color = Lighting.GetColor(Position.ToPoint());
            spriteBatch.Draw(UEPipelineInputTP.PipelineChannelSide.Value, drawPos + orig, null, color
                , drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }

    internal class UEPipelineInputTP : MachineTP, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<UEPipelineInputTile>();
        public static Asset<Texture2D> Pipeline { get; private set; }
        public static Asset<Texture2D> PipelineSide { get; private set; }
        public static Asset<Texture2D> PipelineCorner { get; private set; }
        public static Asset<Texture2D> PipelineCornerSide { get; private set; }
        public static Asset<Texture2D> PipelineCross { get; private set; }
        public static Asset<Texture2D> PipelineCrossSide { get; private set; }
        public static Asset<Texture2D> PipelineChannel { get; private set; }
        public static Asset<Texture2D> PipelineChannelSide { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutches { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutchesSide { get; private set; }
        internal List<SideStateInput> SideState { get; private set; }
        internal int TurningID { get; private set; }
        internal bool Turning { get; private set; }
        internal bool Decussation { get; private set; }
        internal int ThreeCrutchesID { get; private set; }
        public override int TargetItem => ModContent.ItemType<UEPipelineInput>();
        public override float MaxUEValue => 20;
        void ICWRLoader.LoadAsset() {
            PipelineChannel = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineInputChannel");
            PipelineChannelSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineChannelSide");
            Pipeline = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineInput");
            PipelineSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineSide");
            PipelineCorner = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineInputCorner");
            PipelineCornerSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineCornerSide");
            PipelineCross = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineInputCross");
            PipelineCrossSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineCrossSide");
            PipelineThreeCrutches = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineInputThreeCrutches");
            PipelineThreeCrutchesSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineThreeCrutchesSide");
        }
        void ICWRLoader.UnLoadData() {
            PipelineChannel = null;
            PipelineChannelSide = null;
            Pipeline = null;
            PipelineSide = null;
            PipelineCorner = null;
            PipelineCornerSide = null;
            PipelineCross = null;
            PipelineCrossSide = null;
            PipelineThreeCrutches = null;
            PipelineThreeCrutchesSide = null;
        }

        public override void SetMachine() {
            SideState = new List<SideStateInput>() {
            new (new Point16(0, -1)),//上0
            new (new Point16(0, 1)),//下1
            new (new Point16(-1, 0)),//左2
            new (new Point16(1, 0))//右3
            };
        }

        public override void Update() {
            foreach (var side in SideState) {
                side.coreTP = this;
                side.Position = Position;
                side.Update();
            }

            TurningID = 0;
            Turning = false;
            Decussation = false;
            ThreeCrutchesID = -1;

            if (SideState[0].linkID == 2 && (SideState[2].linkID == 2 || SideState[3].linkID == 2)) {
                Turning = true;//这种情况判定为拐角
                if (SideState[2].linkID == 2) {//上左
                    TurningID = 2;
                }
                else if (SideState[3].linkID == 2) {//上右
                    TurningID = 0;
                }
            }
            if (SideState[1].linkID == 2 && (SideState[2].linkID == 2 || SideState[3].linkID == 2)) {
                Turning = true;//这种情况判定为拐角
                if (SideState[2].linkID == 2) {//下左
                    TurningID = 3;
                }
                else if (SideState[3].linkID == 2) {//下右
                    TurningID = 1;
                }
            }

            int num = 0;
            int crutchesID = -1;
            for (int i = 0; i < SideState.Count; i++) {
                SideStateInput side = SideState[i];
                if (side.linkID == 2) {
                    num++;
                }
                else {
                    crutchesID = i;
                }
            }
            if (num == 3) {
                ThreeCrutchesID = crutchesID;
            }

            if (SideState[0].linkID == 2 && SideState[1].linkID == 2 && SideState[2].linkID == 2 && SideState[3].linkID == 2) {
                Decussation = true;//这种情况判定为十字交叉
            }
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Decussation) {
                return;//十字交叉下不能进行边缘绘制
            }
            foreach (var side in SideState) {
                if (side.canDraw && side.linkID != 2) {//链接其他时绘制在后面
                    side.Draw(spriteBatch);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!Decussation) {//十字交叉下不能进行边缘绘制
                foreach (var side in SideState) {
                    if (side.canDraw && side.linkID == 2) {//链接管道自己时绘制在前面
                        side.Draw(spriteBatch);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;

            if (ThreeCrutchesID >= 0) {
                Rectangle rectangle = CWRUtils.GetRec(PipelineThreeCrutches.Value, ThreeCrutchesID, 4);
                spriteBatch.Draw(PipelineThreeCrutches.Value, drawPos, rectangle, Color.White * (MachineData.UEvalue / 10f), 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                spriteBatch.Draw(PipelineThreeCrutchesSide.Value, drawPos, rectangle, Lighting.GetColor(Position.ToPoint()), 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                return;
            }

            if (Decussation) {
                drawPos = CenterInWorld - Main.screenPosition;
                spriteBatch.Draw(PipelineCross.Value, drawPos, null, Color.White * (MachineData.UEvalue / 10f), 0, PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                spriteBatch.Draw(PipelineCrossSide.Value, drawPos, null, Lighting.GetColor(Position.ToPoint()), 0, PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                return;
            }

            if (Turning) {
                Rectangle rectangle = CWRUtils.GetRec(PipelineCorner.Value, TurningID, 4);
                spriteBatch.Draw(PipelineCorner.Value, drawPos, rectangle, Color.White * (MachineData.UEvalue / 10f), 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                spriteBatch.Draw(PipelineCornerSide.Value, drawPos, rectangle, Lighting.GetColor(Position.ToPoint()), 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                return;
            }

            int linkCount = 0;
            int linkCount2 = 0;
            foreach (var side in SideState) {
                if (side.linkID != 0) {
                    linkCount++;
                    if (side.linkID != 2) {
                        linkCount2++;
                    }
                }
            }

            if (linkCount != 2 || linkCount2 == 2) {
                spriteBatch.Draw(Pipeline.Value, drawPos.GetRectangle(Size), Color.White * (MachineData.UEvalue / 10f));
                spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), Lighting.GetColor(Position.ToPoint()));
            }
        }
    }
}
