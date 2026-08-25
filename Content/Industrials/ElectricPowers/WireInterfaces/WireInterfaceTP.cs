using CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors;
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
        /// 此处即权威端,翻转后逐台 SendData 把新待机位推给所有客户端
        /// </summary>
        internal void OnHitWire() {
            int flipped = 0;
            ForEachAdjacentMachine(machine => {
                machine.Disabled = !machine.Disabled;
                machine.SendData();
                flipped++;
                //单人可见的即时反馈;服务器上写入无人渲染,无害
                CombatText.NewText(machine.HitBox, WireInterface.Tint,
                    machine.Disabled ? WireInterface.MachineOffText.Value : WireInterface.MachineOnText.Value);
            });
            if (flipped > 0) {
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.5f }, CenterInWorld);
            }
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

        /// <summary>程序化本体:暗铁底座+四向接线柱+模式芯;贴图后补,加载零资产</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToPoint());
            Rectangle src = new(0, 0, 1, 1);
            int x = (int)drawPos.X;
            int y = (int)drawPos.Y;

            //底座外壳与内嵌面板
            spriteBatch.Draw(px, new Rectangle(x, y, 16, 16), src, new Color(52, 50, 56).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 2, 12, 12), src, new Color(33, 32, 38).MultiplyRGB(light));

            //四向接线柱(机关线的落点暗示)
            Color post = new Color(150, 108, 66).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x + 6, y, 4, 2), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 6, y + 14, 4, 2), src, post);
            spriteBatch.Draw(px, new Rectangle(x, y + 6, 2, 4), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 14, y + 6, 2, 4), src, post);

            //模式芯:桥接=中性银 / 满电播报=充能绿 / 空电播报=警示橙;呼吸微动
            Color core = OutputMode switch {
                1 => new Color(110, 220, 130),
                2 => new Color(240, 150, 70),
                _ => new Color(186, 186, 198),
            };
            float pulse = 0.78f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + Position.X * 0.7f) * 0.22f;
            spriteBatch.Draw(px, new Rectangle(x + 5, y + 5, 6, 6), src, core * pulse);
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
