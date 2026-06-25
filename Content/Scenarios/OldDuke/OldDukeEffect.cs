using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    /// <summary>
    /// 硫磺海场景效果
    /// </summary>
    internal class OldDukeSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => OldDukeEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(SulfurSeaSky.Name, isActive);
    }

    /// <summary>
    /// 硫磺海天空效果，使用 <c>SulfurSeaSky.fx</c> 程序化着色器绘制毒雾/酸雨/气泡/腐蚀/爆发，
    /// 取代旧的逐像素复合绘制（上千次 SpriteBatch.Draw），由 GPU 一次性完成
    /// </summary>
    internal class SulfurSeaSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:SulfurSeaSky";
        private bool active;
        private float intensity;

        //硫酸爆发闪光通道（目标由 OldDukeEffect 触发，着色器内渲染）
        private float burst;          //爆发强度，触发后指数衰减
        private float burstX = 0.5f;  //爆发屏幕x位置

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //创建硫磺海毒绿色滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.25f, 0.15f)//毒绿色调
                .UseOpacity(0.6f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            burst = 0f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }
            //仅在最底层背景绘制一次
            if (maxDepth < 0 || minDepth >= 0) {
                return;
            }

            Effect shader = EffectLoader.SulfurSeaSky?.Value;
            if (shader == null) {
                //着色器缺失时回退为纯色叠加，氛围不至于完全丢失
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    new Color(15, 25, 18) * (intensity * 0.95f)
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
            shader.Parameters["uBurst"]?.SetValue(burst);
            shader.Parameters["uBurstX"]?.SetValue(burstX);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            //强度变化
            if (OldDukeEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.025f;
                }
            }
            else {
                intensity -= 0.02f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }

            //爆发闪光：瞬时脉冲 + 指数衰减，由 OldDukeEffect 触发
            if (OldDukeEffect.ConsumeSkyBurst(out float newBurstX, out float newStrength)) {
                burst = Math.Max(burst, newStrength);
                burstX = newBurstX;
            }
            burst *= 0.9f;
            if (burst < 0.01f) {
                burst = 0f;
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用毒绿色调
            if (intensity > 0.1f) {
                float toxicR = 0.85f;
                float toxicG = 1.0f;
                float toxicB = 0.8f;

                Color tintedColor = new Color(
                    (int)(inColor.R * toxicR),
                    (int)(inColor.G * toxicG),
                    (int)(inColor.B * toxicB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.5f);
            }
            return inColor;
        }
    }

    /// <summary>
    /// 硫磺海场景效果，IsActive 由 ComputeShouldBeActive 声明式推导
    /// </summary>
    internal class OldDukeEffect : ModSystem
    {
        public static bool IsActive;
        public static int ActiveTimer;

        private int poisonWaveTimer = 0;

        //天空爆发闪光（客户端视觉，OldDukeEffect 触发 → SulfurSeaSky 消费）
        private static bool skyBurstPending;
        private static float skyBurstX = 0.5f;
        private static float skyBurstStrength = 1f;

        /// <summary>
        /// 声明式 IsActive，唯一开关入口
        /// </summary>
        private static bool ComputeShouldBeActive() => OldDukeStorySync.IsAnyScenarioActive();

        /// <summary>触发一次天空硫酸爆发闪光；仅客户端，由着色器渲染（同帧多次取最强）</summary>
        private static void TriggerSkyBurst(Vector2 worldPosition, float strength = 1f) {
            if (VaultUtils.isServer || !IsActive) {
                return;
            }
            strength = MathHelper.Clamp(strength, 0f, 1f);
            if (skyBurstPending && strength < skyBurstStrength) {
                return;
            }
            skyBurstPending = true;
            skyBurstStrength = strength;
            skyBurstX = MathHelper.Clamp(
                (worldPosition.X - Main.screenPosition.X) / Main.screenWidth, 0.1f, 0.9f);
        }

        /// <summary>消费一次天空爆发闪光（SulfurSeaSky.Update 调用）</summary>
        public static bool ConsumeSkyBurst(out float screenX, out float strength) {
            screenX = skyBurstX;
            strength = skyBurstStrength;
            if (!skyBurstPending) {
                return false;
            }
            skyBurstPending = false;
            return true;
        }

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeEffect);
            packet.Write(IsActive);
            packet.Write(OldDukeCampsite.WannaToFight);
            packet.Write(Main.myPlayer);

            OldDukeInteractionState state = OldDukeStorySync.GetState(Main.LocalPlayer);

            packet.Write((byte)state);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.OldDukeEffect) {
                IsActive = reader.ReadBoolean();
                OldDukeCampsite.WannaToFight = reader.ReadBoolean();
                int playerIndex = reader.ReadInt32();

                OldDukeInteractionState state = (OldDukeInteractionState)reader.ReadByte();

                if (playerIndex.TryGetPlayer(out var player)) { OldDukeStorySync.Get(player).OldDukeState = state; }

                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.OldDukeEffect);
                    packet.Write(IsActive);
                    packet.Write(OldDukeCampsite.WannaToFight);
                    packet.Write(playerIndex);
                    packet.Write((byte)state);
                    packet.Send(-1, whoAmI);
                }
            }
        }

        public override void PostUpdateEverything() {
            //声明式计算：每帧从当前游戏状态推导IsActive，而非依赖手动开关
            //这样即使某处代码遗漏了关闭调用，效果也会在条件不满足时自动消失
            bool shouldBeActive = ComputeShouldBeActive();

            //仅在状态发生变化时触发网络同步，避免每帧发包
            if (IsActive != shouldBeActive) {
                IsActive = shouldBeActive;
                Send();
            }

            if (IsActive) {
                ActiveTimer++;
                poisonWaveTimer++;

                //毒雾、酸雨、上浮气泡、腐蚀斑等持续氛围已全部由 SulfurSeaSky.fx 着色器渲染，
                //此处不再逐帧生成大量环境粒子；仅保留少量与玩法事件挂钩的局部爆点

                //偶尔生成扩散的毒液波纹（局部点缀）
                if (poisonWaveTimer % 90 == 0) {
                    SpawnPoisonWave();
                }

                //偶尔生成硫酸爆发效果（含玩法投射物 + 天空闪光）
                if (ActiveTimer % 150 == 0) {
                    SpawnSulfuricBurst();
                }

                //播放硫磺海音乐
                if (!CWRRef.GetBossRushActive()) {
                    int index = NPC.FindFirstNPC(CWRID.NPC_OldDuke);
                    if (index.TryGetNPC(out var npc) && npc.friendly) {
                        Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityModMusic/Sounds/Music/AcidRainTier1");
                    }
                }
            }
            else {
                ActiveTimer = 0;
                poisonWaveTimer = 0;
            }
        }

        /// <summary>
        /// 生成扩散的毒液波纹（局部点缀，配合天空着色器的弱闪光）
        /// </summary>
        private static void SpawnPoisonWave() {
            Vector2 waveCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.25f, 0.75f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.25f, 0.75f)
            );

            //环形酸液飞溅
            int waveCount = 6;
            for (int i = 0; i < waveCount; i++) {
                float angle = MathHelper.TwoPi * i / waveCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                PRTLoader.NewParticle<PRT_AcidSplash>(waveCenter, velocity, Color.White, Main.rand.NextFloat(1f, 2f)).Configure(Main.rand.Next(70, 120));
            }

            //中心发光核心
            PRTLoader.NewParticle<PRT_SulfuricCore>(waveCenter, Vector2.Zero, Color.White, Main.rand.NextFloat(0.15f, 0.5f)).Configure(60);

            //天空弱闪光呼应
            TriggerSkyBurst(waveCenter, 0.5f);

            //播放水泡音效
            if (Main.rand.NextBool(4)) {
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.3f,
                    Pitch = -0.4f,
                    MaxInstances = 3
                }, waveCenter);
            }
        }

        /// <summary>
        /// 生成硫酸爆发效果：保留玩法投射物与音效，氛围闪光交由天空着色器，大幅削减粒子数量
        /// </summary>
        private static void SpawnSulfuricBurst() {
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.2f, 0.8f)
            );

            //爆发核心
            PRTLoader.NewParticle<PRT_SulfuricCore>(burstCenter, Vector2.Zero, Color.White, Main.rand.NextFloat(0.2f, 0.5f)).Configure(90);

            //少量内圈酸雾扩散
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                PRTLoader.NewParticle<PRT_ToxicMist>(burstCenter + Main.rand.NextVector2Circular(15f, 15f), velocity, Color.White, Main.rand.NextFloat(2f, 4f)).Configure(Main.rand.Next(10, 16), Main.rand.NextFloat(0.6f, 1f));
            }

            //少量腐蚀碎片
            for (int i = 0; i < 8; i++) {
                Vector2 fragmentVelocity = Main.rand.NextVector2Circular(6f, 6f);

                PRTLoader.NewParticle<PRT_AcidSplash>(burstCenter + Main.rand.NextVector2Circular(20f, 20f), fragmentVelocity, Color.White, Main.rand.NextFloat(0.5f, 1f)).Configure(Main.rand.Next(50, 100));
            }

            //天空强闪光呼应
            TriggerSkyBurst(burstCenter, 1f);

            //音效：硫酸沸腾声
            SoundEngine.PlaySound(SoundID.Item95 with {
                Volume = 0.5f,
                Pitch = -0.3f,
                MaxInstances = 2
            }, burstCenter);

            //额外的爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.4f,
                Pitch = -0.6f,
                MaxInstances = 2
            }, burstCenter);

            if (!VaultUtils.isClient && NPC.FindFirstNPC(CWRID.NPC_OldDuke).TryGetNPC(out var boss)) {
                Projectile.NewProjectile(boss.FromObjectGetParent(), burstCenter, Vector2.Zero, ModContent.ProjectileType<SulfuricacidExplosion>(), 120, 2, -1);
            }
        }

        public override void Unload() {
            IsActive = false;
            ActiveTimer = 0;
        }
    }
}
