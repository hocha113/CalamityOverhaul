using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.FluidInjectors
{
    /// <summary>
    /// 灌注机TP:反向泵,按节拍消耗储液向机身正下方的世界空间放液。
    /// 放液沿竖直探杆自上而下找第一个可放格,遇实心块或异种液体即停;
    /// 世界改动仅权威端(主线程经 Defer),放液走原版 sendWater 同步。
    /// 岩浆放置与原版岩浆桶同权,不引入额外许可系统
    /// </summary>
    internal class FluidInjectorTP : BaseBattery, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<FluidInjectorTile>();
        public override int TargetItem => ModContent.ItemType<FluidInjector>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;

        #region 液体容器契约
        public int FluidType { get; set; }
        public int FluidAmount { get; set; }
        public int FluidCapacity => 4 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Consumer;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>放置一整格液体的电费,补格按比例折算</summary>
        internal const float InjectCostPerTile = 2f;
        /// <summary>作业节拍(帧)</summary>
        internal const int BeatTicks = 30;
        /// <summary>放液探杆向下深度(格)</summary>
        internal const int ScanDepth = 8;

        private int beatTimer;

        #region 纯客户端表现状态(液柱包络与落点)
        /// <summary>基础流量包络 0..1,观测到放液后抬升维持</summary>
        internal float FlowEnvelope;
        /// <summary>节拍脉冲,放液瞬间打满后指数退潮(柱身"涌"的节拍)</summary>
        internal float PulseVis;
        /// <summary>断流进度 0..1:前沿自源头向下撕脱,尾段照常下落</summary>
        internal float PourDrain;
        /// <summary>落点存在与强度(平滑),喂 uSplash</summary>
        internal float SplashVis;
        /// <summary>落点面世界 y(px),-1=打空(8 格内无承接面)</summary>
        internal float ImpactWorldY = -1f;
        /// <summary>喂给着色器的综合流量</summary>
        internal float CurrentFlowVis => MathHelper.Clamp(FlowEnvelope * 0.85f + PulseVis * 0.45f, 0f, 1.2f);

        private int pourSustain;
        private int lastFluidAmountVis = -1;
        private int impactScanTimer;
        private int splashFxTimer;
        private int dripFxTimer;
        #endregion

        public override void SetBattery() {
            //液柱最多探到机底下 8 格,放宽屏外剔除余量
            DrawExtendMode = 340;
        }

        public override void UpdateMachine() {
            if (!Main.dedServ) {
                UpdateVisualFx();
            }

            //作业与世界改动仅权威端
            if (VaultUtils.isClient) {
                return;
            }
            if (++beatTimer < BeatTicks) {
                return;
            }
            beatTimer = 0;

            if (MachineData.UEvalue < InjectCostPerTile || FluidAmount <= 0) {
                return;
            }

            //放液点扫描与世界写入都在主线程做(并行阶段经 Defer 延后,串行阶段立即执行)
            Defer(() => {
                if (MachineData.UEvalue < InjectCostPerTile || FluidAmount <= 0) {
                    return;
                }

                int tileWidth = Width / 16;
                int tileHeight = Height / 16;
                int top = Position.Y + tileHeight;
                int bottom = top + ScanDepth - 1;

                //每列一根探杆,被实心块或异种液体挡住的列不再向深处灌(不穿墙不混液)
                bool[] blocked = new bool[tileWidth];
                for (int y = top; y <= bottom; y++) {
                    for (int xi = 0; xi < tileWidth; xi++) {
                        if (blocked[xi]) {
                            continue;
                        }
                        int x = Position.X + xi;
                        if (!WorldGen.InWorld(x, y, 40)) {
                            blocked[xi] = true;
                            continue;
                        }
                        Tile tile = Framing.GetTileSafely(x, y);
                        if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                            blocked[xi] = true;
                            continue;
                        }
                        if (tile.LiquidAmount > 0 && tile.LiquidType != FluidType) {
                            blocked[xi] = true;
                            continue;
                        }
                        //已满同种格跳过,向更深处灌
                        if (tile.LiquidAmount >= byte.MaxValue) {
                            continue;
                        }

                        int need = byte.MaxValue - tile.LiquidAmount;
                        if (FluidAmount < need) {
                            //储液不足一格补量,等下个节拍
                            return;
                        }

                        FluidAmount -= need;
                        MachineData.UEvalue -= InjectCostPerTile * need / FluidHelper.UnitsPerTile;

                        tile.LiquidType = FluidType;
                        tile.LiquidAmount = byte.MaxValue;
                        WorldGen.SquareTileFrame(x, y);
                        if (VaultUtils.isServer) {
                            NetMessage.sendWater(x, y);
                        }

                        //事件推送:客户端立刻拿到新液量
                        SendData();
                        return;
                    }
                }
            });
        }

        #region 表现推进(纯客户端,零网络)
        /// <summary>
        /// 液柱表现由"观测到液量下降"驱动:放液只在权威端结算,液量经事件包到达各端,
        /// 因此各端看到的下降节拍就是真实作业节拍;缺电/缺液/下方全堵时观测不到下降,
        /// 包络自然断流——状态反馈不猜测原因,只如实反映"有没有在放"
        /// </summary>
        private void UpdateVisualFx() {
            if (lastFluidAmountVis < 0) {
                lastFluidAmountVis = FluidAmount;
            }
            if (FluidAmount < lastFluidAmountVis) {
                //一次放液节拍:维持窗覆盖两个节拍,脉冲打满
                pourSustain = BeatTicks * 2 + 15;
                PulseVis = 1f;
            }
            lastFluidAmountVis = FluidAmount;

            if (pourSustain > 0) {
                pourSustain--;
            }
            PulseVis *= 0.94f;

            bool flowing = pourSustain > 0 && !Disabled;
            if (flowing) {
                FlowEnvelope = MathHelper.Lerp(FlowEnvelope, 1f, 0.22f);
                PourDrain = MathHelper.Lerp(PourDrain, 0f, 0.45f);
            }
            else if (FlowEnvelope > 0.02f) {
                //断流:柱身前沿撕脱,走完后包络清零
                PourDrain += 1f / 22f;
                if (PourDrain >= 1f) {
                    PourDrain = 1f;
                    FlowEnvelope = 0f;
                }
            }
            else {
                FlowEnvelope = 0f;
                PulseVis = 0f;
            }

            //落点节流扫描(只读,并行安全)
            if (--impactScanTimer <= 0) {
                impactScanTimer = 20;
                ScanImpactSurface();
            }
            SplashVis = MathHelper.Lerp(SplashVis, ImpactWorldY > 0f && flowing ? 1f : 0f, 0.16f);

            if (!FluidVFX.NearLocalPlayer(CenterInWorld)) {
                return;
            }
            FluidStyle style = FluidVFX.GetStyle(FluidType);

            //落点飞沫与涟漪
            if (flowing && ImpactWorldY > 0f && SplashVis > 0.35f) {
                Vector2 impact = new(CenterInWorld.X, ImpactWorldY);
                if (++splashFxTimer % 7 == 0) {
                    Defer(() => {
                        for (int i = 0; i < 2; i++) {
                            Vector2 vel = new(Main.rand.NextFloat(-1.7f, 1.7f), Main.rand.NextFloat(-2.8f, -1.1f));
                            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(impact + new Vector2(Main.rand.NextFloat(-5f, 5f), -2f),
                                vel, Color.Lerp(style.Main, style.Bright, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.5f, 0.8f))
                                ?.Configure(Main.rand.Next(16, 26), 0.3f);
                        }
                    });
                }
                if (splashFxTimer % 26 == 0) {
                    Defer(() => {
                        var ring = PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(impact, Vector2.Zero,
                            style.Bright * 0.75f, 1f);
                        ring?.Configure(0.05f, 0.16f, 20);
                    });
                }
            }
            //阻塞悬滴:有电有液却放不出去(下方全堵/储液不足补格),出液口偶发滴挂
            else if (!flowing && FluidAmount > 0 && MachineData.UEvalue >= InjectCostPerTile && !Disabled) {
                if (++dripFxTimer >= 55) {
                    dripFxTimer = 0;
                    Vector2 outlet = new(CenterInWorld.X, PosInWorld.Y + Height - 2f);
                    Defer(() => {
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(outlet, new Vector2(0f, 0.4f),
                            style.Main, 0.55f)?.Configure(30, 0.22f);
                    });
                }
            }
        }

        /// <summary>
        /// 落点面扫描:自机底沿两根机身列向下找第一个承接面
        /// (实心非平台块顶,或足量液体的液面),8 格内无承接=打空
        /// </summary>
        private void ScanImpactSurface() {
            int tileWidth = Width / 16;
            int top = Position.Y + Height / 16;
            int bottom = top + ScanDepth - 1;

            for (int y = top; y <= bottom; y++) {
                for (int xi = 0; xi < tileWidth; xi++) {
                    int x = Position.X + xi;
                    if (!WorldGen.InWorld(x, y, 40)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                        ImpactWorldY = y * 16f;
                        return;
                    }
                    if (tile.LiquidAmount >= 48) {
                        //液面高度按存量折算
                        ImpactWorldY = y * 16f + (16f - tile.LiquidAmount / 255f * 16f);
                        return;
                    }
                }
            }
            ImpactWorldY = -1f;
        }
        #endregion

        #region 存档与同步:液体字段追加在基类之后
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
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Water;
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

    /// <summary>
    /// 灌注机放液液柱绘制:<see cref="EffectLoader.FluidPour"/> 画在竖直 quad 上,
    /// PostDrawTiles 层(物块之上实体之下),自开 Immediate 批合批所有灌注机。
    /// 画布折算契约:quadW = 柱宽×4.4(容纳溅丘与摆动),源头留 1.2 倍柱宽垫高,
    /// 打空时假想落点收进 quad 内让滴串在画布内散尽;着色器缺失走 CPU 分段条回退
    /// </summary>
    internal class FluidPourDraw : GlobalTileProcessor
    {
        /// <summary>柱满宽(px),判定无关,纯表现</summary>
        private const float WidthPx = 12f;

        public override bool PreDrawEverything(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return true;
            }
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (canvas == null) {
                return true;
            }
            Effect shader = EffectLoader.FluidPour?.Value;

            bool begun = false;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not FluidInjectorTP inj || !inj.Active) {
                    continue;
                }
                //待机冻结的包络不许继续放液:Disabled 即断流
                float flow = inj.Disabled ? 0f : inj.CurrentFlowVis;
                if (flow <= 0.02f && (inj.Disabled || inj.SplashVis <= 0.03f)) {
                    continue;
                }
                if (!VaultUtils.IsPointOnScreen(inj.PosInWorld - Main.screenPosition, inj.DrawExtendMode)) {
                    continue;
                }

                if (shader == null || noise == null) {
                    DrawFallback(spriteBatch, inj, flow);
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                        SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    GraphicsDevice gd = Main.instance.GraphicsDevice;
                    gd.Textures[1] = noise;
                    gd.SamplerStates[1] = SamplerState.LinearWrap;
                }

                FluidStyle style = FluidVFX.GetStyle(inj.FluidType);
                Vector2 outlet = new(inj.CenterInWorld.X, inj.PosInWorld.Y + inj.Height - 2f);
                bool hasImpact = inj.ImpactWorldY > 0f;

                float quadW = WidthPx * 4.4f;
                float topPad = WidthPx * 1.2f;
                float quadTop = outlet.Y - topPad;
                float quadBottom = hasImpact
                    ? inj.ImpactWorldY + WidthPx * 1.6f
                    : outlet.Y + FluidInjectorTP.ScanDepth * 16f + WidthPx * 2f;
                float quadH = MathF.Max(quadBottom - quadTop, 40f);
                //打空:假想落点收进画布,让滴串在 quad 内散尽而非贴底切断
                float impactY = hasImpact ? inj.ImpactWorldY - quadTop : quadH - WidthPx * 1.2f;

                //uniform 全量重设:共享着色器的参数是设备全局状态,漏一个=串上一台的残值
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uQuadPx"]?.SetValue(new Vector2(quadW, quadH));
                shader.Parameters["uWidthPx"]?.SetValue(WidthPx);
                shader.Parameters["uSourceY"]?.SetValue(topPad);
                shader.Parameters["uImpactY"]?.SetValue(impactY);
                shader.Parameters["uFlow"]?.SetValue(flow);
                shader.Parameters["uDrain"]?.SetValue(inj.PourDrain);
                shader.Parameters["uSplash"]?.SetValue(hasImpact ? inj.SplashVis * MathHelper.Clamp(flow * 1.4f, 0f, 1f) : 0f);
                shader.Parameters["uGlassy"]?.SetValue(style.Glassy);
                shader.Parameters["uCrust"]?.SetValue(style.Crust);
                shader.Parameters["uSparkle"]?.SetValue(style.Sparkle);
                shader.Parameters["uSeed"]?.SetValue(FluidVFX.Hash01(inj.Position.X * 53 + inj.Position.Y * 17));
                shader.Parameters["uColBright"]?.SetValue(style.Bright.ToVector3());
                shader.Parameters["uColMain"]?.SetValue(style.Main.ToVector3());
                shader.Parameters["uColDeep"]?.SetValue(style.Deep.ToVector3());
                shader.CurrentTechnique.Passes[0].Apply();

                spriteBatch.Draw(canvas, new Vector2(outlet.X - quadW * 0.5f, quadTop) - Main.screenPosition,
                    null, Color.White, 0f, Vector2.Zero,
                    new Vector2(quadW / canvas.Width, quadH / canvas.Height), SpriteEffects.None, 0f);

                //岩浆/微光液柱补光
                if (style.Glow > 0.3f) {
                    Vector2 mid = new(outlet.X, (quadTop + quadBottom) * 0.5f);
                    float k = style.Glow * 0.4f * MathHelper.Clamp(flow, 0f, 1f);
                    Lighting.AddLight(mid, style.Main.R / 255f * k, style.Main.G / 255f * k, style.Main.B / 255f * k);
                }
            }

            if (begun) {
                spriteBatch.End();
            }
            return true;
        }

        /// <summary>着色器缺失的 CPU 回退:分段渐窄条,两端 alpha 收口(不许平切),落点一枚软光斑</summary>
        private static void DrawFallback(SpriteBatch spriteBatch, FluidInjectorTP inj, float flow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            FluidStyle style = FluidVFX.GetStyle(inj.FluidType);
            Vector2 outlet = new(inj.CenterInWorld.X, inj.PosInWorld.Y + inj.Height - 2f);
            bool hasImpact = inj.ImpactWorldY > 0f;
            float len = hasImpact ? inj.ImpactWorldY - outlet.Y : FluidInjectorTP.ScanDepth * 16f;
            if (len <= 8f) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            const int segs = 8;
            float alphaBase = MathHelper.Clamp(flow, 0f, 1f) * 0.8f;
            for (int i = 0; i < segs; i++) {
                float t0 = i / (float)segs;
                float w = WidthPx * MathHelper.Lerp(1f, 0.72f, t0);
                //两端收口:首段淡入,打空时末段淡出
                float endFade = MathF.Min(MathHelper.Clamp(t0 * 5f, 0f, 1f),
                    hasImpact ? 1f : MathHelper.Clamp((1f - t0) * 2.4f, 0f, 1f));
                //断流前沿之上不画
                float drainCut = t0 < inj.PourDrain ? 0f : 1f;
                float a = alphaBase * endFade * drainCut;
                if (a <= 0.02f) {
                    continue;
                }
                Vector2 segPos = new(outlet.X - w * 0.5f, outlet.Y + len * t0);
                spriteBatch.Draw(px, segPos - Main.screenPosition, null, style.Main * a, 0f,
                    Vector2.Zero, new Vector2(w / px.Width, len / segs / px.Height), SpriteEffects.None, 0f);
            }
            if (hasImpact && inj.SplashVis > 0.1f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 impact = new(outlet.X, inj.ImpactWorldY);
                spriteBatch.Draw(glow, impact - Main.screenPosition, null,
                    FluidVFX.Glow(style.Bright, 0.5f * inj.SplashVis * MathHelper.Clamp(flow, 0f, 1f)),
                    0f, glow.Size() * 0.5f, new Vector2(0.7f, 0.28f), SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
