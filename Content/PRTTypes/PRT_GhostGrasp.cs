using CalamityOverhaul.Content.Wraiths.GhostHands;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 赋力「攥」的幽手（GHOSTHAND-PLAN §5 表 #2，唯一新建 PRT）：
    /// 30t 生命=18t 汇聚淡入（预备拍）+ 6t 急合攥握（打击拍，落 Grab 音与余烬迸散）+ 6t 消散。
    /// 绘制复用 <see cref="GhostHandDrawHelper"/> 的指节矩形装配，零新纹理
    /// </summary>
    internal class PRT_GhostGrasp : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private const int ConvergeTicks = 18;
        private const int SnapTicks = 6;
        private const int TotalTicks = 30;

        private int facing = 1;
        private int targetWho = -1;
        private bool snapped;

        public PRT_GhostGrasp Configure(int handFacing, int npcWho) {
            facing = handFacing >= 0 ? 1 : -1;
            targetWho = npcWho;
            Lifetime = TotalTicks;
            return this;
        }

        public override void Reset() {
            base.Reset();
            facing = 1;
            targetWho = -1;
            snapped = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = TotalTicks;
            }
        }

        public override void AI() {
            //汇聚期贴住目标身后(目标可能仍在动),攥握帧起冻结在攥点
            if (Time < ConvergeTicks && targetWho >= 0 && targetWho < Main.maxNPCs) {
                NPC target = Main.npc[targetWho];
                if (target.active) {
                    Position = target.Center - new Vector2(target.direction * (target.width * 0.5f + 14f), 0f);
                }
            }
            if (!snapped && Time >= ConvergeTicks) {
                snapped = true;
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.4f, Volume = 0.8f, MaxInstances = 3 }, Position);
                //目标位余烬迸散
                Vector2 burst = targetWho >= 0 && targetWho < Main.maxNPCs && Main.npc[targetWho].active
                    ? Main.npc[targetWho].Center : Position;
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(burst, Main.rand.NextVector2Circular(3f, 3f),
                        GhostHandDrawHelper.Ember, Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(true, Main.rand.Next(14, 22));
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float alpha;
            float curl;
            if (Time < ConvergeTicks) {
                float t = Time / (float)ConvergeTicks;
                alpha = t * 0.9f;
                curl = 0.12f;
            }
            else if (Time < ConvergeTicks + SnapTicks) {
                float t = (Time - ConvergeTicks) / (float)SnapTicks;
                alpha = 0.95f;
                curl = MathHelper.Lerp(0.12f, 1f, t * t);
            }
            else {
                float t = (Time - ConvergeTicks - SnapTicks) / (float)(TotalTicks - ConvergeTicks - SnapTicks);
                alpha = 0.95f * (1f - t);
                curl = 1f;
            }
            GhostHandDrawHelper.DrawHand(spriteBatch, Position - Main.screenPosition, facing,
                Time * 0.28f, curl, alpha, 0.9f, 0.8f, Time * 0.22f);
            return false;
        }
    }
}
