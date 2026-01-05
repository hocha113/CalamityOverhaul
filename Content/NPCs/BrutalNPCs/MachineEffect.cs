using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs
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

    ///<summary>
    ///机械工业天空效果
    ///</summary>
    internal class MachineSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:MachineSky";
        private bool active;
        private float intensity;

        //机械齿轮转动效果
        private float gearRotation = 0f;
        private float gearSpeed = 0.5f;

        //电路闪烁效果
        private float circuitFlicker = 0f;

        //全局机械脉动
        private float mechanicalPulse = 0f;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //创建暗红工业滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.08f, 0.08f)//暗红工业调
                .UseOpacity(0.4f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            gearRotation = 0f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }
            if (maxDepth >= float.MaxValue && minDepth < float.MaxValue) {
                //暗红机械背景
                Color bgColor = new Color(18, 10, 10);
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    bgColor * intensity * 0.64f
                );
            }

            //绘制电路火花
            DrawCircuitSparks(spriteBatch);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = MachineEffect.Cek();

            //强度变化
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

            gearRotation += gearSpeed * 0.016f;
            if (gearRotation > MathHelper.TwoPi) {
                gearRotation -= MathHelper.TwoPi;
            }

            mechanicalPulse += 0.04f;
            if (mechanicalPulse > MathHelper.TwoPi) {
                mechanicalPulse -= MathHelper.TwoPi;
            }

            //更新电路闪烁
            circuitFlicker = (float)Math.Sin(mechanicalPulse * 1.2f) * 0.3f + 0.4f;
        }

        public override Color OnTileColor(Color inColor) {
            //应用暗红工业调
            if (intensity > 0.1f) {
                float mechR = 0.95f;
                float mechG = 0.6f;
                float mechB = 0.6f;

                Color tintedColor = new Color(
                    (int)(inColor.R * mechR),
                    (int)(inColor.G * mechG),
                    (int)(inColor.B * mechB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.35f);
            }
            return inColor;
        }

        private void DrawCircuitSparks(SpriteBatch sb) {
            //电路火花效果
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int sparkCount = (int)(30 * circuitFlicker * intensity);

            for (int i = 0; i < sparkCount; i++) {
                int x = Main.rand.Next(Main.screenWidth);
                int y = Main.rand.Next(Main.screenHeight);
                int size = Main.rand.Next(2, 4);
                float sparkAlpha = Main.rand.NextFloat(0.3f, 0.7f);

                Color sparkColor = new Color(255, 150, 120);
                sb.Draw(pixel,
                    new Rectangle(x, y, size, size),
                    sparkColor * (sparkAlpha * circuitFlicker * intensity));
            }
        }
    }

    ///<summary>
    ///机械场景效果管理器
    ///</summary>
    internal class MachineEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;

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

            if (CWRWorld.MachineRebellion) {
                IsActive = true;
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

        public override void PostUpdateEverything() {
            if (!Main.gameMenu) {
                Start();
            }

            if (!Cek()) {
                return;
            }

            if (++CekTimer > 60 * 60 * 3) {
                IsActive = false;
                return;
            }

            if (!CWRRef.GetBossRushActive()) {
                int slot = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Metal");
                if (Main.newMusic != slot) {
                    MusicToast.ShowMusic(
                        title: "位元堕落",
                        artist: "Ryusa",
                        albumCover: CWRUtils.GetT2DValue(CWRConstant.NPC + "BSP/Skeletron_Head"),
                        style: MusicToast.MusicStyle.Neon,
                        displayDuration: 360//6秒
                    );
                }
                Main.newMusic = Main.musicBox2 = slot;
            }
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
