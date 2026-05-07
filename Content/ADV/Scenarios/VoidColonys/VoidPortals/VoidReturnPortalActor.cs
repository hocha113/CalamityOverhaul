using CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.TimeShift;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals
{
    /// <summary>
    /// 虚空聚落出生点的返回传送门Actor
    /// 仅在"现在"时代可见，切换到"过去"后渐变消失
    /// 玩家进入触发范围后启动传送演出并返回主世界
    /// </summary>
    internal class VoidReturnPortalActor : Actor
    {
        //现在时代淡入速度
        private const float FadeInStep = 0.03f;
        //过去时代或触发后的淡出速度
        private const float FadeOutStep = 0.045f;
        //触发后加速淡出的速度倍率
        private const float TriggeredFadeOutStep = 0.08f;
        //传送门视觉半径（像素）
        private const float PortalRadius = 80f;
        //玩家右键交互范围（像素）
        private const float InteractRange = 320f;

        private float visibility;
        private float effectTime;
        private float hoverStrength;
        //是否已触发传送，防止重复触发
        private bool triggered;

        public static VoidReturnPortalActor ActiveInstance { get; private set; }

        public static VoidReturnPortalActor ValidateActive() {
            if (ActiveInstance == null || !ActiveInstance.Active) {
                ActiveInstance = null;
                return null;
            }
            return ActiveInstance;
        }

        public override void OnSpawn(params object[] args) {
            Width = 2;
            Height = 2;
            DrawLayer = ActorDrawLayer.AfterTiles;
            DrawExtendMode = (int)(PortalRadius * 5);
            visibility = 0f;
            hoverStrength = 0f;
            triggered = false;
            //给动画一个随机相位，避免多次生成时视觉完全同步
            effectTime = Main.rand.NextFloat(0f, MathHelper.TwoPi);

            //若已存在旧实例则先清理
            VoidReturnPortalActor old = ValidateActive();
            if (old != null && old != this) {
                ActorLoader.KillActor(old.WhoAmI);
            }
            ActiveInstance = this;
            VoidReturnSession.Close();
        }

        public override void AI() {
            Velocity = Vector2.Zero;

            if (!VoidColony.Active) {
                if (ActiveInstance == this) ActiveInstance = null;
                ActorLoader.KillActor(WhoAmI);
                return;
            }

            effectTime += 1f / 60f;

            if (triggered) {
                //触发后加速淡出，淡尽后移除自身
                visibility = MathF.Max(0f, visibility - TriggeredFadeOutStep);
                if (visibility < 0.001f) {
                    if (ActiveInstance == this) ActiveInstance = null;
                    ActorLoader.KillActor(WhoAmI);
                }
                return;
            }

            //现在时代逼近1，过去时代逼近0
            float target = VoidTimeShiftSystem.InPast ? 0f : 1f;
            if (visibility < target) visibility = MathF.Min(target, visibility + FadeInStep);
            else if (visibility > target) visibility = MathF.Max(target, visibility - FadeOutStep);

            VoidReturnSession.Update();

            if (Main.netMode == NetmodeID.Server) return;
            //只有可见度足够时才允许交互，防止在过去时代意外触发
            if (visibility < 0.55f) {
                hoverStrength = 0f;
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                hoverStrength = 0f;
                return;
            }

            bool canInteract = !Main.gameMenu
                && !VoidReturnSession.IsOpen
                && !player.mouseInterface
                && Vector2.DistanceSquared(player.Center, Position) < InteractRange * InteractRange;

            bool mouseNear = canInteract
                && Vector2.DistanceSquared(Main.MouseWorld, Position) < PortalRadius * PortalRadius * 2.5f;

            float hoverTarget = mouseNear ? 1f : 0f;
            hoverStrength = MathHelper.Lerp(hoverStrength, hoverTarget, 0.18f);
            if (Math.Abs(hoverStrength - hoverTarget) < 0.005f) hoverStrength = hoverTarget;

            if (!mouseNear) return;

            player.CWR().DontUseItemTime = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = false;

            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                VoidReturnSession.Open(this);
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
        }

        internal void TriggerReturn() {
            if (triggered) return;
            triggered = true;
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;
            if (player.TryGetModPlayer(out VoidTransportPlayer tp)) {
                tp.StartTransport(Position, () => VoidColony.Exit());
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            float vis = visibility;
            float transBoost = VoidTimeShiftSystem.TransitionStrength * 0.35f;
            float totalVis = vis + transBoost;
            if (totalVis < 0.001f) return false;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D diffuse = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) return false;

            Vector2 screenPos = Position - Main.screenPosition;
            float pulse = 0.7f + 0.3f * MathF.Sin(effectTime * 2.1f);
            Vector2 origin = glow.Size() * 0.5f;

            //外层蓝色漫射光晕
            float outerScale = (PortalRadius * 2.6f) / glow.Width;
            Color outerCol = new Color(0.15f, 0.5f, 1.0f, 0f) * (totalVis * 0.55f);
            spriteBatch.Draw(glow, screenPos, null, outerCol, 0f, origin, outerScale, SpriteEffects.None, 0f);

            //内核亮白蓝光
            float innerScale = (PortalRadius * 0.85f) / glow.Width;
            Color innerCol = new Color(0.6f, 0.85f, 1.0f, 0f) * (totalVis * pulse * 1.15f);
            spriteBatch.Draw(glow, screenPos, null, innerCol, 0f, origin, innerScale, SpriteEffects.None, 0f);

            //扩散圆环缓慢旋转
            if (diffuse != null) {
                origin = diffuse.Size() * 0.5f;
                float ringScale = (PortalRadius * 2.4f) / diffuse.Width;
                float ringPulse = 0.45f + 0.55f * MathF.Sin(effectTime * 1.4f + MathHelper.Pi);
                Color ringCol = new Color(0.1f, 0.4f, 0.9f, 0f) * (totalVis * ringPulse * 0.7f);
                spriteBatch.Draw(diffuse, screenPos, null, ringCol, effectTime * 0.12f, origin, ringScale, SpriteEffects.None, 0f);
            }

            //中心十字耀斑
            if (star != null) {
                origin = star.Size() * 0.5f;
                float starScale = (PortalRadius * 1.35f) / star.Width;
                Color starCol = new Color(0.45f, 0.72f, 1.0f, 0f) * (totalVis * pulse * 0.5f);
                spriteBatch.Draw(star, screenPos, null, starCol, effectTime * 0.32f, origin, starScale, SpriteEffects.None, 0f);
                //45度叠一层增加光芒数量
                spriteBatch.Draw(star, screenPos, null, starCol * 0.65f, effectTime * 0.32f + MathHelper.PiOver4,
                    origin, starScale * 0.8f, SpriteEffects.None, 0f);
            }

            //悬停时绘制外圈扩张光圈提示交互
            if (hoverStrength > 0.01f && !VoidReturnSession.IsOpen && glow != null) {
                origin = glow.Size() * 0.5f;
                float hoverScale = (PortalRadius * 3.2f) / glow.Width;
                Color hoverCol = new Color(0.35f, 0.75f, 1.0f, 0f) * (totalVis * hoverStrength * 0.65f);
                spriteBatch.Draw(glow, screenPos, null, hoverCol, 0f, origin, hoverScale, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
