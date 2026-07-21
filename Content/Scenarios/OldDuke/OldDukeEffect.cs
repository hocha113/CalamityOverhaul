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
    /// <summary>硫磺海 SceneEffect</summary>
    internal class OldDukeSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => OldDukeEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(SulfurSeaSky.Name, isActive);
    }

    /// <summary>硫磺海天空，走 <c>SulfurSeaSky.fx</c></summary>
    internal class SulfurSeaSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:SulfurSeaSky";
        private bool active;
        private float intensity;

        //爆发闪光，OldDukeEffect触发
        private float burst;//强度，指数衰减
        private float burstX = 0.5f;//屏x

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.25f, 0.15f)
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
            //最底层画一次
            if (maxDepth < 0 || minDepth >= 0) {
                return;
            }

            Effect shader = EffectLoader.SulfurSeaSky?.Value;
            if (shader == null) {
                //缺着色器则纯色兜底
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

            //爆发闪光脉冲
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

    /// <summary>硫磺海效果，IsActive声明式</summary>
    internal class OldDukeEffect : ModSystem
    {
        public static bool IsActive;
        public static int ActiveTimer;

        private int poisonWaveTimer = 0;

        //天空爆发，本端触发→Sky消费
        private static bool skyBurstPending;
        private static float skyBurstX = 0.5f;
        private static float skyBurstStrength = 1f;

        private static bool ComputeShouldBeActive() => OldDukeStorySync.IsAnyScenarioActive();

        /// <summary>客户端天空爆发，同帧取最强</summary>
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

        /// <summary>Sky消费爆发闪光</summary>
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
            //声明式推IsActive
            bool shouldBeActive = ComputeShouldBeActive();

            //变状态才发包
            if (IsActive != shouldBeActive) {
                IsActive = shouldBeActive;
                Send();
            }

            if (IsActive) {
                ActiveTimer++;
                poisonWaveTimer++;

                //氛围交着色器；这里只留玩法爆点
                if (poisonWaveTimer % 90 == 0) {
                    SpawnPoisonWave();
                }

                if (ActiveTimer % 150 == 0) {
                    SpawnSulfuricBurst();
                }

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

        private static void SpawnPoisonWave() {
            Vector2 waveCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.25f, 0.75f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.25f, 0.75f)
            );

            int waveCount = 6;
            for (int i = 0; i < waveCount; i++) {
                float angle = MathHelper.TwoPi * i / waveCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                PRTLoader.NewParticle<PRT_AcidSplash>(waveCenter, velocity, Color.White, Main.rand.NextFloat(1f, 2f)).Configure(Main.rand.Next(70, 120));
            }

            PRTLoader.NewParticle<PRT_SulfuricCore>(waveCenter, Vector2.Zero, Color.White, Main.rand.NextFloat(0.15f, 0.5f)).Configure(60);

            TriggerSkyBurst(waveCenter, 0.5f);

            if (Main.rand.NextBool(4)) {
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.3f,
                    Pitch = -0.4f,
                    MaxInstances = 3
                }, waveCenter);
            }
        }

        private static void SpawnSulfuricBurst() {
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.2f, 0.8f)
            );

            PRTLoader.NewParticle<PRT_SulfuricCore>(burstCenter, Vector2.Zero, Color.White, Main.rand.NextFloat(0.2f, 0.5f)).Configure(90);

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                PRTLoader.NewParticle<PRT_ToxicMist>(burstCenter + Main.rand.NextVector2Circular(15f, 15f), velocity, Color.White, Main.rand.NextFloat(2f, 4f)).Configure(Main.rand.Next(10, 16), Main.rand.NextFloat(0.6f, 1f));
            }

            for (int i = 0; i < 8; i++) {
                Vector2 fragmentVelocity = Main.rand.NextVector2Circular(6f, 6f);

                PRTLoader.NewParticle<PRT_AcidSplash>(burstCenter + Main.rand.NextVector2Circular(20f, 20f), fragmentVelocity, Color.White, Main.rand.NextFloat(0.5f, 1f)).Configure(Main.rand.Next(50, 100));
            }

            TriggerSkyBurst(burstCenter, 1f);

            SoundEngine.PlaySound(SoundID.Item95 with {
                Volume = 0.5f,
                Pitch = -0.3f,
                MaxInstances = 2
            }, burstCenter);

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
