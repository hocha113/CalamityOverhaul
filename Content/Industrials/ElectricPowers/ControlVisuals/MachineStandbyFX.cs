using CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches;
using CalamityOverhaul.Content.Industrials.ElectricPowers.WireInterfaces;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ControlVisuals
{
    /// <summary>
    /// 机器待机统一视觉语言 + 待机位翻转的全端反馈中枢。零基类改动:
    /// 挂 InnoVault <see cref="GlobalTileProcessor.PostDraw"/>,在每台屏内机器
    /// 自己的绘制之后、主 AlphaBlend 批内追加绘制,覆盖所有 <see cref="MachineTP"/>
    /// 而无需逐机器接线。<br/>
    /// 边沿源:<see cref="MachineTP.Disabled"/>(基类包尾同步字段)的逐实例绘制帧
    /// 缓存比对——本地翻转(右键/单人机关线)当帧检出,远端经 ReceiveData 写入后
    /// 下一绘制帧检出;屏外错过的翻转按陈旧检测(超过 <see cref="StaleFrameGap"/>
    /// 绘制帧未见)静默重同步,不补播。<br/>
    /// 总闸除外:它的 Disabled=分闸,是工作状态而非待机,开合演出由其自绘;
    /// 但仍参与边沿追踪,用于通知邻接接口器播端口闪光
    /// </summary>
    internal class MachineStandbyFX : GlobalTileProcessor
    {
        /// <summary>
        /// 绘制帧时钟,PreDrawEverything 推进。暂停时逻辑帧冻结而绘制帧照走,
        /// 控制层各表现器件的边沿陈旧检测统一以它为准
        /// </summary>
        internal static uint DrawFrame { get; private set; }

        /// <summary>超过此绘制帧数未见的机器视为陈旧,状态变化静默重同步不播反馈</summary>
        internal const int StaleFrameGap = 30;

        private sealed class TrackState
        {
            public bool Disabled;
            public uint LastSeen;
        }

        private static readonly Dictionary<TileProcessor, TrackState> tracked = new();
        private static readonly HashSet<WireInterfaceTP> notifyCache = new();
        private static uint lastPruneFrame;
        private static int soundBudget;

        public override bool PreDrawEverything(SpriteBatch spriteBatch) {
            DrawFrame++;
            soundBudget = 2;//同帧批量翻转只响两声,防接口器整排翻转爆音

            //周期清理:已死实例,以及久未绘制的实例(不依赖世界卸载是否翻 Active 位,防跨世界残留)
            if (DrawFrame - lastPruneFrame > 600) {
                lastPruneFrame = DrawFrame;
                List<TileProcessor> dead = null;
                foreach (var pair in tracked) {
                    if (!pair.Key.Active || DrawFrame - pair.Value.LastSeen > 3600) {
                        (dead ??= new List<TileProcessor>()).Add(pair.Key);
                    }
                }
                if (dead != null) {
                    foreach (var key in dead) {
                        tracked.Remove(key);
                    }
                }
            }
            return true;
        }

        public override void PostDraw(TileProcessor tileProcessor, SpriteBatch spriteBatch) {
            //管道不参与翻转,跳过追踪省开销
            if (tileProcessor is not MachineTP machine || machine is BaseUEPipelineTP) {
                return;
            }

            if (!tracked.TryGetValue(tileProcessor, out var state)) {
                //首次见到(含世界加载即待机的机器):静默登记,不播反馈
                tracked[tileProcessor] = new TrackState { Disabled = machine.Disabled, LastSeen = DrawFrame };
            }
            else {
                if (state.Disabled != machine.Disabled) {
                    bool stale = DrawFrame - state.LastSeen > StaleFrameGap;
                    state.Disabled = machine.Disabled;
                    if (!stale) {
                        OnDisabledEdge(machine);
                    }
                }
                state.LastSeen = DrawFrame;
            }

            if (machine.Disabled && machine is not GridSwitchTP) {
                DrawStandbyOverlay(machine, spriteBatch);
            }
        }

        /// <summary>待机位翻转边沿:图腾+播报+咔哒声,并通知邻接接口器播端口闪光</summary>
        private static void OnDisabledEdge(MachineTP machine) {
            NotifyAdjacentInterfaces(machine);

            if (machine is GridSwitchTP) {
                return;//总闸自有开合演出(见 GridSwitchTP.UpdateLeverEnvelope)
            }

            bool off = machine.Disabled;
            PRTLoader.NewParticle<PRT_CtrlToggleTotem>(
                machine.CenterInWorld + new Vector2(0f, -machine.Height * 0.5f - 6f),
                new Vector2(0f, -0.9f), Color.White, 1f)?.Configure(!off);
            CombatText.NewText(machine.HitBox, WireInterface.Tint,
                off ? WireInterface.MachineOffText.Value : WireInterface.MachineOnText.Value);

            if (soundBudget > 0) {
                soundBudget--;
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.4f, Pitch = off ? -0.3f : 0.15f }, machine.CenterInWorld);
            }
        }

        /// <summary>翻转机器的外缘若贴着接口器,让它闪一下端口:信号"经它而来"的示意</summary>
        private static void NotifyAdjacentInterfaces(MachineTP machine) {
            notifyCache.Clear();
            int tileWidth = machine.Width / 16;
            int tileHeight = machine.Height / 16;
            for (int i = 0; i < tileWidth; i++) {
                CollectInterface(new Point16(machine.Position.X + i, machine.Position.Y - 1));
                CollectInterface(new Point16(machine.Position.X + i, machine.Position.Y + tileHeight));
            }
            for (int j = 0; j < tileHeight; j++) {
                CollectInterface(new Point16(machine.Position.X - 1, machine.Position.Y + j));
                CollectInterface(new Point16(machine.Position.X + tileWidth, machine.Position.Y + j));
            }
            foreach (var wireInterface in notifyCache) {
                wireInterface.NotifyRelayFlash();
            }
        }

        private static void CollectInterface(Point16 point) {
            if (!Framing.GetTileSafely(point).HasTile) {
                return;
            }
            if (!VaultUtils.SafeGetTopLeft(point, out var topLeft)) {
                return;
            }
            if (TileProcessorLoader.ByPositionGetTP(topLeft, out var tp)
                && tp is WireInterfaceTP wireInterface && wireInterface.Active) {
                notifyCache.Add(wireInterface);
            }
        }

        /// <summary>统一待机语言:呼吸暗化罩 + 右上蓝灰"‖"角标(与关停图腾同符号)</summary>
        private static void DrawStandbyOverlay(MachineTP machine, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 drawPos = machine.PosInWorld - Main.screenPosition;
            int x = (int)drawPos.X;
            int y = (int)drawPos.Y;

            //呼吸暗化:placeholder2 是真 alpha 像素,才能画"暗";按机器坐标错相
            float breath = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + machine.Position.X * 0.9f);
            spriteBatch.Draw(px, new Rectangle(x, y, machine.Width, machine.Height), src,
                Color.Black * (0.20f + 0.08f * breath));

            //右上角标:暗板 + 双竖条
            int tagX = x + machine.Width - 11;
            int tagY = y + 2;
            spriteBatch.Draw(px, new Rectangle(tagX, tagY, 9, 9), src, new Color(30, 33, 42) * 0.9f);
            Color bar = new Color(150, 166, 196) * (0.5f + 0.35f * breath);
            spriteBatch.Draw(px, new Rectangle(tagX + 2, tagY + 2, 2, 5), src, bar);
            spriteBatch.Draw(px, new Rectangle(tagX + 5, tagY + 2, 2, 5), src, bar);
        }
    }
}
