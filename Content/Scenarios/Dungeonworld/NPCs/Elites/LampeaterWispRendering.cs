using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 噬灯魂屏幕效果登记表（纯客户端表现层）：本体 AI 每帧上报吞光域槽位，
    /// 咬中/死亡当帧触发脉冲。所有输入源于各端同步的 ai 状态与本地前进沿观察，
    /// 光照变化不写 tile，各端读同一份 ai 推同一份画面——权威在服务器的状态，不在演出。
    /// 槽位=场上单型上限 2（导演预算），第三只起只丢屏幕层不丢本体绘制。
    /// </summary>
    internal static class LampeaterScreenEffects
    {
        internal const int MaxWisps = 2;

        internal struct WispSlot
        {
            public int Owner;
            public int Ttl;
            public Vector2 World;
            public float RadiusPx;
            public float Strength;
            public float Inhale;
            public float Ember;
            public readonly bool Active => Ttl > 0;
        }

        internal struct RingFx
        {
            public bool Active;
            public Vector2 World;
            public float MaxRadiusPx;
            public int Age;
            public int Life;
        }

        internal static readonly WispSlot[] Wisps = new WispSlot[MaxWisps];
        internal static readonly RingFx[] Pulses = new RingFx[MaxWisps];
        internal static readonly RingFx[] Bursts = new RingFx[MaxWisps];

        /// <summary>咬中坍缩一拍的帧长（快收，ease-in 在 shader 侧）</summary>
        private const int PulseLife = 24;
        /// <summary>死亡释放环帧长</summary>
        private const int BurstLife = 46;

        internal static bool HasAny {
            get {
                for (int i = 0; i < MaxWisps; i++) {
                    if (Wisps[i].Active || Pulses[i].Active || Bursts[i].Active) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>本体 AI 每帧上报（whoAmI 稳定占槽，断报 3 帧自动腾出）</summary>
        internal static void ReportWisp(int owner, Vector2 world, float radiusPx,
            float strength, float inhale, float ember) {
            int free = -1;
            for (int i = 0; i < MaxWisps; i++) {
                if (Wisps[i].Active && Wisps[i].Owner == owner) {
                    free = i;
                    break;
                }
                if (free < 0 && !Wisps[i].Active) {
                    free = i;
                }
            }
            if (free < 0) {
                return;
            }
            Wisps[free] = new WispSlot {
                Owner = owner,
                Ttl = 3,
                World = world,
                RadiusPx = radiusPx,
                Strength = strength,
                Inhale = inhale,
                Ember = ember,
            };
        }

        internal static void TriggerPulse(Vector2 world, float radiusPx)
            => TriggerRing(Pulses, world, radiusPx, PulseLife);

        internal static void TriggerBurst(Vector2 world, float radiusPx)
            => TriggerRing(Bursts, world, radiusPx, BurstLife);

        private static void TriggerRing(RingFx[] rings, Vector2 world, float radiusPx, int life) {
            int pick = 0;
            for (int i = 0; i < rings.Length; i++) {
                if (!rings[i].Active) {
                    pick = i;
                    break;
                }
                if (rings[i].Age > rings[pick].Age) {
                    pick = i;
                }
            }
            rings[pick] = new RingFx { Active = true, World = world, MaxRadiusPx = radiusPx, Age = 0, Life = life };
        }

        internal static void Update() {
            for (int i = 0; i < MaxWisps; i++) {
                if (Wisps[i].Ttl > 0) {
                    Wisps[i].Ttl--;
                }
                AgeRing(ref Pulses[i]);
                AgeRing(ref Bursts[i]);
            }
        }

        private static void AgeRing(ref RingFx ring) {
            if (!ring.Active) {
                return;
            }
            if (++ring.Age >= ring.Life) {
                ring.Active = false;
            }
        }

        internal static void Clear() {
            Array.Clear(Wisps);
            Array.Clear(Pulses);
            Array.Clear(Bursts);
        }
    }

    /// <summary>退世界清空静态登记表：残留的世界坐标环/域换档后会画在错误位置（最长 46 帧）</summary>
    internal class LampeaterScreenEffectsSystem : Terraria.ModLoader.ModSystem
    {
        public override void OnWorldUnload() => LampeaterScreenEffects.Clear();
    }

    /// <summary>
    /// 噬灯魂吞光域全屏后效（LampeaterVeil.fx）：妖火周身一圈光被吃掉的暗带 +
    /// 亮部被径向拽入的拉痕 + 咬中坍缩收环 + 死亡释放金环。
    /// 拷屏乘法压暗（屏幕后效是唯一能物理变暗的层，VFX.md 暗层陷阱的正解），
    /// screenTarget ping-pong 与 SkeletronScreenRender 同款合同。
    /// 权重 1.680：C2 分配频段 1.680–1.689 首位。
    /// </summary>
    internal class LampeaterVeilRender : RenderHandle
    {
        public override float Weight => 1.680f;

        public override bool CanLoad() => DungeonworldEliteGate.Enabled;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            LampeaterScreenEffects.Update();

            if (!LampeaterScreenEffects.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.LampeaterVeil?.Value;
            if (shader == null || CWRAsset.PerlinNoise?.Value == null) {
                return;
            }

            Span<Vector4> wisps = stackalloc Vector4[LampeaterScreenEffects.MaxWisps];
            Vector2 inhalePair = Vector2.Zero;
            Vector2 emberPair = Vector2.Zero;
            for (int i = 0; i < LampeaterScreenEffects.MaxWisps; i++) {
                ref readonly var w = ref LampeaterScreenEffects.Wisps[i];
                if (!w.Active) {
                    wisps[i] = Vector4.Zero;
                    continue;
                }
                Vector2 uv = WorldToScreenUV(w.World);
                wisps[i] = new Vector4(uv.X, uv.Y, PixelsToHeightNorm(w.RadiusPx), w.Strength);
                if (i == 0) {
                    inhalePair.X = w.Inhale;
                    emberPair.X = w.Ember;
                }
                else {
                    inhalePair.Y = w.Inhale;
                    emberPair.Y = w.Ember;
                }
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uWisp0"]?.SetValue(wisps[0]);
            shader.Parameters["uWisp1"]?.SetValue(wisps[1]);
            shader.Parameters["uInhalePair"]?.SetValue(inhalePair);
            shader.Parameters["uEmberPair"]?.SetValue(emberPair);
            shader.Parameters["uPulse0"]?.SetValue(RingParam(in LampeaterScreenEffects.Pulses[0]));
            shader.Parameters["uPulse1"]?.SetValue(RingParam(in LampeaterScreenEffects.Pulses[1]));
            shader.Parameters["uBurst0"]?.SetValue(RingParam(in LampeaterScreenEffects.Bursts[0]));
            shader.Parameters["uBurst1"]?.SetValue(RingParam(in LampeaterScreenEffects.Bursts[1]));
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图（合同同 SkeletronScreenRender）
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //拷屏再回写
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        private static Vector4 RingParam(in LampeaterScreenEffects.RingFx ring) {
            if (!ring.Active || ring.Life <= 0) {
                return Vector4.Zero;
            }
            Vector2 uv = WorldToScreenUV(ring.World);
            float progress = MathHelper.Clamp(ring.Age / (float)ring.Life, 0.001f, 0.998f);
            return new Vector4(uv.X, uv.Y, PixelsToHeightNorm(ring.MaxRadiusPx), progress);
        }

        /// <summary>世界→归一化uv（含Zoom）</summary>
        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }

        /// <summary>像素→屏高归一化</summary>
        private static float PixelsToHeightNorm(float pixels) {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            if (zoomY <= 0f) {
                zoomY = 1f;
            }
            return pixels * zoomY / Main.screenHeight;
        }
    }
}
