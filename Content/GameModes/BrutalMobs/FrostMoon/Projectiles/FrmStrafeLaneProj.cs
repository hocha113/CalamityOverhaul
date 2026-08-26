using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 精灵直升机扫射航线：ai[0]=锚NPC索引 ai[1]=锚NPC类型 ai[2]=锁定方向+10（0=追踪中）。
    /// 追踪期航线随机体瞄向目标，锁定帧后起点与方向双冻结（预告航线即承诺），
    /// 扫射期航线常亮作参照，机炮弹横向散布被 <see cref="LaneHalfWidth"/> 钳制在画出的航道内。
    /// 全程无判定（威胁只在机炮弹本身）
    /// </summary>
    internal class FrmStrafeLaneProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>航线预告帧（≥30 契约；末段为锁定段）</summary>
        internal const int TelegraphFrames = 46;
        /// <summary>锁定帧（白热窄闪宣告承诺）</summary>
        internal const int LockFrames = 18;
        /// <summary>扫射帧：航线长 ÷ 扫掠实速（提速补偿相消后恒为 StrafeSpeed，见 FrmSiegeNPC 注释）</summary>
        internal const int RunFrames = 90;
        /// <summary>航线全长（像素）</summary>
        internal const float LaneLength = 900f;
        /// <summary>航道半宽：机炮弹生成散布上限与两条边界线共用此常量</summary>
        internal const float LaneHalfWidth = 64f;
        private const int FadeFrames = 14;
        private const float LaneCoreWidth = 30f;

        private static readonly Color LaneWarn = new Color(150, 226, 255, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int AnchorType => (int)Projectile.ai[1];
        private bool Locked => Projectile.ai[2] != 0f;
        private int TotalLife => TelegraphFrames + RunFrames + FadeFrames;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        private bool InRun => Elapsed >= TelegraphFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 960;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入端：ai[2] 已非零=服务端早已锁定，相位快进到锁定段起点，不重放追踪期
                if (Locked) {
                    Projectile.timeLeft = RunFrames + FadeFrames + LockFrames;
                }
            }

            if (Locked) {
                //权威锁定：方向与起点冻结（起点=锁定时刻已停更的自身位置）
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else {
                //追踪期：跟随锚机体（索引+类型双校验，防槽位复用），瞄向其目标
                NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
                if (!anchor.Alives() || anchor.type != AnchorType) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = anchor.Center;
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }

            int elapsed = Elapsed;
            if (elapsed == TelegraphFrames - LockFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
            }
            if (elapsed == TelegraphFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, LaneWarn.R / 255f * 0.14f, LaneWarn.G / 255f * 0.14f, LaneWarn.B / 255f * 0.14f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            float strength;
            bool lockFlash = !InRun && elapsed >= TelegraphFrames - LockFrames;
            if (InRun) {
                //扫射期航线常亮作参照，尾段随退场淡出
                strength = 0.5f * MathHelper.Clamp((TotalLife - elapsed) / (float)FadeFrames, 0f, 1f);
                if (strength > 0.5f) {
                    strength = 0.5f;
                }
            }
            else {
                strength = fadeIn * (lockFlash ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();

            //航道边界线：±LaneHalfWidth 与机炮弹散布共用同一常量（可见宽度=真实威胁宽度）
            Main.EntitySpriteDraw(tex, drawPos + perp * LaneHalfWidth, null, LaneWarn * (0.32f * strength), Projectile.rotation,
                origin, new Vector2(scaleX, 6f / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos - perp * LaneHalfWidth, null, LaneWarn * (0.32f * strength), Projectile.rotation,
                origin, new Vector2(scaleX, 6f / tex.Height), SpriteEffects.None, 0);

            if (lockFlash) {
                //锁定段：白热窄闪宣告承诺
                float lockT = MathHelper.Clamp((elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.7f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth + 22f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, new Color(255, 250, 235, 0) * (0.8f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 12f) / tex.Height), SpriteEffects.None, 0);
            }
            else {
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.28f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneHalfWidth * 2f / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
