using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    ///<summary>
    ///机械场景效果
    ///</summary>
    internal class MachineSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => MachineEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(MachineSky.Name, isActive);
    }

    internal class MachinePlayer : PlayerOverride
    {
        public override IEnumerable<string> GetActiveSceneEffectFullNames() {
            yield return "UnCalamityModMusic.Common.Music.Mechs";
        }
        public override bool? PreIsSceneEffectActive(ModSceneEffect modSceneEffect) {
            return false;
        }
    }

    ///<summary>
    ///机械工业天空：全程序化着色器天空（锈红工业战争夜空——熔炉地平线、
    ///天穹齿轮剪影、滚动阴云、探照灯与上升余烬），并实时响应机械Boss行为——
    ///冲刺/俯冲瞬间天空如闪雷亮起、蓄力时地平线警报呼吸、死亡演出时天空过载电涌。
    ///所有响应只发生在天空背景内，不触碰世界照明与全屏滤镜
    ///</summary>
    internal class MachineSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:MachineSky";
        private bool active;
        private float intensity;

        //Boss行为响应通道（目标值由 MachineEffect 每帧聚合）
        private float warn;          //蓄力警告 0-1
        private float overload;      //死亡过载 0-1
        private float flash;         //闪电强度，触发后指数衰减
        private float flashX = 0.5f; //闪电屏幕x位置（兼作电弧形状种子）

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //暗红工业滤镜（保持恒定，行为响应只发生在天空背景着色器内）
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.08f, 0.08f)
                .UseOpacity(0.4f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;

        public override void Deactivate(params object[] args) => active = false;

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = MachineEffect.Cek();

            //强度淡入淡出
            if (MachineEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.02f;
                }
            }
            else {
                intensity -= 0.015f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }

            //平滑跟随聚合目标；闪电为瞬时脉冲 + 快速指数衰减
            warn = MathHelper.Lerp(warn, MachineEffect.SkyWarnTarget, 0.10f);
            overload = MathHelper.Lerp(overload, MachineEffect.SkyOverloadTarget, 0.08f);
            if (MachineEffect.ConsumeSkyFlash(out float newFlashX, out float newStrength)) {
                flash = Math.Max(flash, newStrength);
                flashX = newFlashX;
            }
            flash *= 0.82f;
            if (flash < 0.012f) {
                flash = 0f;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }
            if (maxDepth < 0 || minDepth >= 0) {
                return;
            }

            Effect shader = EffectLoader.MechSky?.Value;
            if (shader == null) {
                //着色器缺失时回退为纯色叠加，氛围不至于完全丢失
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    new Color(18, 10, 10) * (intensity * 0.64f)
                );
                return;
            }

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uIntensity"]?.SetValue(intensity);
            shader.Parameters["uAspectRatio"]?.SetValue(vpW / (float)vpH);
            shader.Parameters["uWarn"]?.SetValue(warn);
            shader.Parameters["uFlash"]?.SetValue(flash);
            shader.Parameters["uFlashX"]?.SetValue(flashX);
            shader.Parameters["uOverload"]?.SetValue(overload);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }

        public override Color OnTileColor(Color inColor) {
            //仅保留恒定的暗红工业调，闪电等行为响应不介入世界照明，避免不自然的全图亮度跳变
            if (intensity <= 0.1f) {
                return inColor;
            }

            Color tintedColor = new Color(
                (int)(inColor.R * 0.95f),
                (int)(inColor.G * 0.6f),
                (int)(inColor.B * 0.6f),
                inColor.A
            );
            return Color.Lerp(inColor, tintedColor, intensity * 0.35f);
        }
    }

    ///<summary>
    ///机械场景效果管理器：负责激活判定、网络同步与音乐，并承接天空行为响应——
    ///闪电由各Boss在AI关键帧主动调用 <see cref="TriggerSkyFlash"/>，
    ///警报/过载由 <see cref="MechBossVisualState.Push"/> 每帧顺带转发聚合，均无需遍历NPC
    ///</summary>
    internal class MachineEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        [VaultLoaden(CWRConstant.NPC + "Meld")]
        public static Asset<Texture2D> MeldAsset = null!;

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.MachineEffect);
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.MachineEffect) {
                IsActive = reader.ReadBoolean();
                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.MachineEffect);
                    packet.Write(IsActive);
                    packet.Send(-1, whoAmI);
                }
            }
        }

        public static bool Cek() {
            if (!IsActive) {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu) {
                IsActive = false;
                return false;
            }

            return true;
        }

        public static void Start() {
            IsActive = false;
            if (VaultUtils.isServer) {
                return;
            }

            if (!CWRServerConfig.Instance.BiologyOverhaul) {
                return;
            }

            if (!CWRWorld.HasBoss) {
                return;
            }

            if (HeadPrimeAI.DontReform()) {
                return;
            }

            bool found = false;
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.SkeletronPrime) {
                    found = true;
                    break;
                }
                else if (npc.type == NPCID.TheDestroyer) {
                    found = true;
                    break;
                }
                else if (npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism) {
                    found = true;
                    break;
                }
            }
            if (!found) {
                return;
            }

            IsActive = true;
        }

        #region 天空行为响应
        /// <summary>蓄力警告聚合目标：本帧所有机械Boss中最强的警告强度，驱动天空地平线警报</summary>
        public static float SkyWarnTarget { get; private set; }
        /// <summary>死亡过载聚合目标：任一机械Boss进入死亡演出时为1，驱动天空过载电涌</summary>
        public static float SkyOverloadTarget { get; private set; }
        private static float warnAccum;
        private static float overloadAccum;
        private static bool skyFlashPending;
        private static float skyFlashX = 0.5f;
        private static float skyFlashStrength = 1f;

        /// <summary>
        /// 机械Boss在AI关键帧（俯冲/冲刺释放等）主动呼唤一次天空闪电。
        /// <paramref name="worldPosition"/> 决定电弧在天幕上的落点，
        /// <paramref name="strength"/> 决定亮度（1=主角级俯冲，0.6~0.8=常规冲刺）。
        /// 仅客户端生效，服务端调用为空操作
        /// </summary>
        public static void TriggerSkyFlash(Vector2 worldPosition, float strength = 1f) {
            if (VaultUtils.isServer || !IsActive) {
                return;
            }
            strength = MathHelper.Clamp(strength, 0f, 1f);
            if (skyFlashPending && strength < skyFlashStrength) {
                return;//同帧多Boss齐闪时保留最强的一次
            }
            skyFlashPending = true;
            skyFlashStrength = strength;
            skyFlashX = MathHelper.Clamp(
                (worldPosition.X - Main.screenPosition.X) / Main.screenWidth, 0.12f, 0.88f);
        }

        /// <summary>消费一次天空闪电触发（由 <see cref="MachineSky.Update"/> 调用）</summary>
        public static bool ConsumeSkyFlash(out float screenX, out float strength) {
            screenX = skyFlashX;
            strength = skyFlashStrength;
            if (!skyFlashPending) {
                return false;
            }
            skyFlashPending = false;
            return true;
        }

        /// <summary>
        /// 由 <see cref="MechBossVisualState.Push"/> 每帧顺带转发Boss视觉状态，
        /// 聚合驱动天空警报/过载（取本帧最大值），无需遍历NPC。
        /// 死亡演出以 Warning 且 intensity≥0.99 为特征值（常规蓄力最高0.95）
        /// </summary>
        internal static void ReportSkyMood(MechBossVisualMode mode, float intensity, float progress) {
            if (VaultUtils.isServer || mode != MechBossVisualMode.Warning) {
                return;
            }
            warnAccum = Math.Max(warnAccum, intensity * progress);
            if (intensity >= 0.99f) {
                overloadAccum = 1f;
            }
        }

        //每帧末闩锁提交本帧聚合值并清零；Boss死亡或离场停止推送时自然归零
        private static void LatchSkyMood() {
            SkyWarnTarget = warnAccum;
            SkyOverloadTarget = overloadAccum;
            warnAccum = 0f;
            overloadAccum = 0f;
        }
        #endregion

        private static bool dompMusicWindown;

        public override void PostUpdateEverything() {
            if (!Main.gameMenu) {
                Start();
            }

            LatchSkyMood();

            if (!Cek()) {
                dompMusicWindown = false;
                return;
            }

            if (!dompMusicWindown) {
                dompMusicWindown = true;
                if (!VaultUtils.isServer) {
                    MusicToast.ShowMusic(
                        title: "位元堕落",
                        artist: "Ryusa",
                        albumCover: MeldAsset.Value,
                        style: MusicToast.MusicStyle.Neon,
                        displayDuration: 360//6秒
                    );
                }
            }

            if (++CekTimer > 60 * 60 * 3) {
                IsActive = false;
                return;
            }

            if (!CWRRef.GetBossRushActive() && !VaultUtils.isServer && !Main.LocalPlayer.GetModPlayer<SirenMusicalBoxPlayer>().IsCursed) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Metal");
            }
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
