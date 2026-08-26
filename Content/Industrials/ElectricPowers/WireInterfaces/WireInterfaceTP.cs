using CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors;
using CalamityOverhaul.Content.Industrials.ElectricPowers.ControlVisuals;
using CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches;
using CalamityOverhaul.Content.Industrials.ElectricPowers.LifeWeavers;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks;
using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.WireInterfaces
{
    /// <summary>
    /// 机关接口器TP:原版机关线与电网的双向桥。<br/>
    /// 线→机:<see cref="OnHitWire"/> 由原版布线执行(多人只在服务器跑,天然权威端),
    /// 翻转全部邻接机器的 <see cref="MachineTP.Disabled"/> 并逐台推包传播;<br/>
    /// 机→线:右键循环输出模式(仅桥接/满电播报/空电播报),权威端监视邻接机器聚合电量,
    /// 越过满/空边沿时经 Wiring.TripWire 沿自身接线发一次脉冲,重臂迟滞防重发。<br/>
    /// 本体不持电不导电(Efficiency=0,管道侧对非电池 TP 也不建连),纯控制件
    /// </summary>
    internal class WireInterfaceTP : MachineTP
    {
        public override int TargetTileID => ModContent.TileType<WireInterfaceTile>();
        public override int TargetItem => ModContent.ItemType<WireInterface>();
        public override float MaxUEValue => 100;

        /// <summary>输出模式:0=仅桥接 1=满电播报 2=空电播报</summary>
        internal byte OutputMode;
        /// <summary>输出重臂标志:发过一次脉冲后须先离开触发区(迟滞)才允许再发;序列化防重载重发</summary>
        private bool armed = true;
        /// <summary>右键节流用时间戳,不走 per-tick 递减(待机中的机器也要能交互)</summary>
        private uint lastInteractTime;
        /// <summary>邻接机器聚合缓存,跨格大机器去重</summary>
        private readonly HashSet<MachineTP> neighborCache = new();

        //满电播报:99% 触发,回落 90% 重臂;空电播报:1% 触发,回升 10% 重臂
        private const float FullTripLine = 0.99f;
        private const float FullRearmLine = 0.90f;
        private const float EmptyTripLine = 0.01f;
        private const float EmptyRearmLine = 0.10f;

        public override void SetMachine() {
            //控制件不参与基类导电均衡:两根管道夹着接口器不会经它连通
            Efficiency = 0;
        }

        #region 序列化:MachineData+待机位(基类) → OutputMode → armed
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(OutputMode);
            data.Write(armed);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            OutputMode = reader.ReadByte();
            armed = reader.ReadBoolean();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["_OutputMode"] = OutputMode;
            tag["_Armed"] = armed;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet("_OutputMode", out byte mode)) {
                OutputMode = mode <= 2 ? mode : (byte)0;
            }
            if (tag.TryGet("_Armed", out bool armedValue)) {
                armed = armedValue;
            }
        }
        #endregion

        #region 邻接扫描
        /// <summary>
        /// 枚举外缘一圈邻格上的可控机器,跨格大机器 TopLeft 归一去重;
        /// 管道/同类接口器/荒野敌对结构不在名单内。
        /// 同岛并行分组(基类 CollectGroupLinks 已声明外圈)保证邻接读写安全
        /// </summary>
        private void ForEachAdjacentMachine(Action<MachineTP> action) {
            neighborCache.Clear();

            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            for (int i = Position.X; i < Position.X + tileWidth; i++) {
                CollectMachine(new Point16(i, Position.Y - 1));
                CollectMachine(new Point16(i, Position.Y + tileHeight));
            }
            for (int j = Position.Y; j < Position.Y + tileHeight; j++) {
                CollectMachine(new Point16(Position.X - 1, j));
                CollectMachine(new Point16(Position.X + tileWidth, j));
            }

            foreach (var machine in neighborCache) {
                action(machine);
            }
        }

        private void CollectMachine(Point16 point) {
            if (!Framing.GetTileSafely(point).HasTile) {
                return;
            }
            if (!VaultUtils.SafeGetTopLeft(point, out var topLeft)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(topLeft, out var tp)) {
                return;
            }
            if (tp is not MachineTP machine || !machine.Active || !IsWireControllable(machine)) {
                return;
            }
            neighborCache.Add(machine);
        }

        /// <summary>可被接口器控制的机器口径</summary>
        private static bool IsWireControllable(MachineTP machine) {
            if (machine is BaseUEPipelineTP) {
                return false;//管道翻转会把网络切碎,切网络请用电网总闸
            }
            if (machine is WireInterfaceTP) {
                return false;//同类互翻会成环
            }
            //荒野敌对结构:不该被玩家线控,且其中部分序列化绕过基类链,待机位不入包;新增 WGG 变体在此追加
            if (machine is WGGLumberjackTP or WGGCollectorTP or WGGLifeWeaverTP
                or WGGWildernessTP or WGGMK2WildernessTP) {
                return false;
            }
            return true;
        }
        #endregion

        #region 线→机:HitWire 翻转邻接机器
        /// <summary>
        /// 被机关线激活:翻转全部邻接机器的待机位。原版布线在多人只于服务器执行,
        /// 此处即权威端,翻转后逐台 SendData 把新待机位推给所有客户端。<br/>
        /// 反馈(图腾/播报/咔哒/端口闪)统一走 MachineStandbyFX 的 Disabled 绘制帧边沿:
        /// 本端与远端同一条路径,服务器上翻转也不再空放无人听见的音效
        /// </summary>
        internal void OnHitWire() {
            ForEachAdjacentMachine(machine => {
                machine.Disabled = !machine.Disabled;
                machine.SendData();
            });
        }
        #endregion

        #region 机→线:状态边沿输出脉冲
        public override void UpdateMachine() {
            //判定与发信仅权威端;客户端状态经包同步,仅供绘制
            if (VaultUtils.isClient || OutputMode == 0) {
                return;
            }

            if (!TryReadNeighborCharge(out float ratio)) {
                return;//无监视对象:状态保持,不触发也不重臂
            }

            if (OutputMode == 1) {
                if (armed && ratio >= FullTripLine) {
                    armed = false;
                    EmitPulse();
                }
                else if (!armed && ratio < FullRearmLine) {
                    armed = true;
                    SendData();
                }
            }
            else {
                if (armed && ratio <= EmptyTripLine) {
                    armed = false;
                    EmitPulse();
                }
                else if (!armed && ratio > EmptyRearmLine) {
                    armed = true;
                    SendData();
                }
            }
        }

        /// <summary>聚合邻接机器充能率;总闸本体恒空,计入即假读数,额外剔除</summary>
        private bool TryReadNeighborCharge(out float ratio) {
            float ue = 0f;
            float max = 0f;
            ForEachAdjacentMachine(machine => {
                if (machine is GridSwitchTP || machine.MachineData == null) {
                    return;
                }
                ue += machine.MachineData.UEvalue;
                max += machine.MaxUEValue;
            });
            ratio = max > 0f ? ue / max : 0f;
            return max > 0f;
        }

        private void EmitPulse() {
            Point16 pos = Position;
            //TripWire 操作全局布线状态,并行阶段延后主线程执行(串行阶段立即执行);
            //它在多人客户端内部直接返回,权威门已在 UpdateMachine 开头
            Defer(() => Wiring.TripWire(pos.X, pos.Y, 1, 1));
            SendData();//armed 变化入包,重载/重连不重发边沿
        }
        #endregion

        #region 交互与绘制
        /// <summary>TP右键经InnoVault总线在所有端各自翻转,推送只留权威端一份(镜像伐木者)</summary>
        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (Main.GameUpdateCount - lastInteractTime < 15) {
                return false;
            }
            lastInteractTime = Main.GameUpdateCount;

            OutputMode = (byte)((OutputMode + 1) % 3);
            //切换模式先撤防:须经历一次条件为假再武装,循环路过某模式时不会误发脉冲
            armed = false;
            if (!VaultUtils.isClient) {
                SendData();
            }
            CombatText.NewText(HitBox, WireInterface.Tint, CurrentModeText());
            SoundEngine.PlaySound(SoundID.MenuTick, CenterInWorld);
            return true;
        }

        internal string CurrentModeText() => OutputMode switch {
            1 => WireInterface.ModeFullText.Value,
            2 => WireInterface.ModeEmptyText.Value,
            _ => WireInterface.ModeBridgeText.Value,
        };

        internal Color CoreColor() => OutputMode switch {
            1 => new Color(110, 220, 130),
            2 => new Color(240, 150, 70),
            _ => new Color(186, 186, 198),
        };

        //===== 纯客户端表现状态:绘制帧推进,不入包不存档 =====
        /// <summary>上一绘制帧所见 armed,null=尚未见过</summary>
        private bool? lastArmedVisual;
        /// <summary>上一绘制帧所见输出模式</summary>
        private byte lastModeVisual;
        /// <summary>上一次绘制帧号,用于陈旧检测</summary>
        private uint lastVisualFrame;
        /// <summary>发信爆闪包络(armed 撤防边沿点燃)</summary>
        private float emitFlash;
        /// <summary>模式切换小闪</summary>
        private float modeFlash;
        /// <summary>收信端口闪包络,由 MachineStandbyFX 在邻接机器翻转边沿通知</summary>
        private float relayFlash;
        /// <summary>重新武装就绪微闪</summary>
        private float rearmBlink;

        /// <summary>邻接机器被翻转时的端口反馈入口(纯表现)</summary>
        internal void NotifyRelayFlash() => relayFlash = 1f;

        /// <summary>
        /// 绘制帧边沿检测。发信边沿源:包内 armed 字段 true→false 且 OutputMode 未同帧
        /// 变化(右键切模式也会撤防,靠模式同帧变化区分);重臂=false→true。
        /// 陈旧(屏外错过 <see cref="MachineStandbyFX.StaleFrameGap"/> 帧以上)时静默重同步不补播
        /// </summary>
        private void UpdateVisualEdges() {
            uint frame = MachineStandbyFX.DrawFrame;
            bool fresh = lastArmedVisual != null && frame - lastVisualFrame <= MachineStandbyFX.StaleFrameGap;
            if (fresh) {
                if (OutputMode != lastModeVisual) {
                    modeFlash = 1f;
                }
                else if (lastArmedVisual != armed && OutputMode != 0) {
                    if (!armed) {
                        //发信:芯体爆闪+沿线流光+轻咔哒
                        emitFlash = 1f;
                        CtrlWireFX.EmitWirePulse(this, CoreColor());
                        SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.3f, Pitch = 0.5f }, CenterInWorld);
                    }
                    else {
                        rearmBlink = 1f;
                    }
                }
            }
            lastArmedVisual = armed;
            lastModeVisual = OutputMode;
            lastVisualFrame = frame;

            emitFlash *= 0.86f;
            modeFlash *= 0.80f;
            relayFlash *= 0.88f;
            rearmBlink *= 0.90f;
        }

        /// <summary>程序化本体:暗铁底座+四向接线柱+模式芯;贴图后补,加载零资产</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            UpdateVisualEdges();

            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToPoint());
            Rectangle src = new(0, 0, 1, 1);
            int x = (int)drawPos.X;
            int y = (int)drawPos.Y;

            //底座外壳与内嵌面板
            spriteBatch.Draw(px, new Rectangle(x, y, 16, 16), src, new Color(52, 50, 56).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 2, 12, 12), src, new Color(33, 32, 38).MultiplyRGB(light));
            //面板受光顶缘 / 四角铆钉 / 底缘磨损暗线
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 2, 12, 1), src, new Color(58, 56, 64).MultiplyRGB(light));
            Color rivet = new Color(86, 84, 94).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 1, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 14, y + 1, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 14, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 14, y + 14, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 4, y + 12, 7, 1), src, new Color(26, 25, 30).MultiplyRGB(light));

            //四向接线柱(机关线的落点暗示):端口闪时铜色抬向白热
            Color post = Color.Lerp(new Color(150, 108, 66), new Color(255, 238, 205), relayFlash * 0.9f).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x + 6, y, 4, 2), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 6, y + 14, 4, 2), src, post);
            spriteBatch.Draw(px, new Rectangle(x, y + 6, 2, 4), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 14, y + 6, 2, 4), src, post);
            //接线柱中心螺点
            Color screw = new Color(102, 70, 42).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x + 7, y, 2, 1), src, screw);
            spriteBatch.Draw(px, new Rectangle(x + 7, y + 15, 2, 1), src, screw);
            spriteBatch.Draw(px, new Rectangle(x, y + 7, 1, 2), src, screw);
            spriteBatch.Draw(px, new Rectangle(x + 15, y + 7, 1, 2), src, screw);

            //端口闪:收到/转发机关信号一瞬的加色光斑
            if (relayFlash > 0.05f && CWRAsset.SoftGlow?.Value is Texture2D relayGlow) {
                Color flashGlow = new Color(255, 200, 130, 0) * (relayFlash * 0.55f);
                spriteBatch.Draw(relayGlow, drawPos + new Vector2(8f, 8f), null, flashGlow, 0f,
                    relayGlow.Size() * 0.5f, 0.2f + relayFlash * 0.5f, SpriteEffects.None, 0f);
            }

            //模式芯:桥接=中性银 / 满电播报=充能绿 / 空电播报=警示橙
            //armed=呼吸起伏(蓄势待发) / 未armed=低亮定值+周期就绪试闪(迟滞等待) / 待机=熄灭
            Color coreDraw;
            if (Disabled) {
                coreDraw = new Color(44, 46, 52);
            }
            else {
                float level;
                if (OutputMode == 0) {
                    level = 0.60f + 0.14f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + Position.X * 0.7f);
                }
                else if (armed) {
                    level = 0.68f + 0.30f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.8f + Position.X * 0.7f);
                }
                else {
                    float tick = (Main.GlobalTimeWrappedHourly * 60f + Position.X * 13f) % 56f < 3f ? 0.16f : 0f;
                    level = 0.40f + tick + rearmBlink * 0.30f;
                }
                level += emitFlash * 1.1f + modeFlash * 0.7f;
                coreDraw = CoreColor() * MathHelper.Clamp(level, 0f, 1.5f);
            }
            coreDraw.A = 255;
            spriteBatch.Draw(px, new Rectangle(x + 5, y + 5, 6, 6), src, coreDraw);

            //发信爆闪:白热小芯 + 模式色加色辉光
            if (emitFlash > 0.1f && !Disabled) {
                spriteBatch.Draw(px, new Rectangle(x + 6, y + 6, 4, 4), src,
                    Color.White * MathHelper.Clamp(emitFlash - 0.15f, 0f, 0.9f));
                if (CWRAsset.SoftGlow?.Value is Texture2D emitGlow) {
                    Color core = CoreColor();
                    spriteBatch.Draw(emitGlow, drawPos + new Vector2(8f, 8f), null,
                        new Color(core.R, core.G, core.B, 0) * (emitFlash * 0.8f), 0f,
                        emitGlow.Size() * 0.5f, 0.3f + emitFlash * 0.5f, SpriteEffects.None, 0f);
                }
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (!HoverTP) {
                return;
            }
            //无电量语义,悬停只报当前模式
            string text = CurrentModeText();
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.7f;
            Vector2 drawPos = CenterInWorld + new Vector2(0, 22) - Main.screenPosition;
            Utils.DrawBorderString(spriteBatch, text, drawPos - textSize * 0.5f, WireInterface.Tint, 0.7f);
        }
        #endregion
    }
}
