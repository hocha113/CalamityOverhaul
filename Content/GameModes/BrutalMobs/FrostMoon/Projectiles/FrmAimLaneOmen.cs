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
    /// 攻城瞄准线（三风味共用）：ai[0]=锚NPC索引 ai[1]=风味×1000+锚NPC类型 ai[2]=锁定方向+10（0=追踪中）。
    /// 风味 0=胡桃夹子跳弹瞄准 / 1=常绿尖叫怪松针速射 / 2=圣诞坦克雪橇冲压（仅水平）。
    /// 追踪期跟随锚体瞄向目标；锁定帧起点与方向双冻结（预告即承诺），
    /// 执行期保留为淡出参照。全程无判定（威胁在其后发出的弹体/冲压本体）
    /// </summary>
    internal class FrmAimLaneOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        internal const int StyleNut = 0;
        internal const int StyleNeedle = 1;
        internal const int StyleRam = 2;

        /// <summary>胡桃夹子：追踪帧/锁定帧（合计 34 ≥30 契约）/执行余痕帧/线长</summary>
        internal const int NutTrackFrames = 14;
        internal const int NutLockFrames = 20;
        internal const int NutLingerFrames = 12;
        internal const float NutLaneLength = 480f;

        /// <summary>松针速射：追踪帧/锁定帧（合计 48 ≥40 小Boss契约）/执行余痕帧（=速射窗）/线长</summary>
        internal const int NeedleTrackFrames = 18;
        internal const int NeedleLockFrames = 30;
        internal const int NeedleLingerFrames = 50;
        internal const float NeedleLaneLength = 780f;

        /// <summary>雪橇冲压：追踪帧/锁定帧（合计 48 ≥40 小Boss契约）/执行余痕帧（=冲压窗）/线长（=冲压距离）</summary>
        internal const int RamTrackFrames = 16;
        internal const int RamLockFrames = 32;
        internal const int RamLingerFrames = 56;
        internal const float RamLaneLength = 620f;

        private static readonly Color NutWarn = new Color(214, 170, 96, 0);
        private static readonly Color NeedleWarn = new Color(120, 214, 130, 0);
        private static readonly Color RamWarn = new Color(255, 84, 64, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int Style => (int)Projectile.ai[1] / 1000;
        private int AnchorType => (int)Projectile.ai[1] % 1000;
        private bool AuthLocked => Projectile.ai[2] != 0f;

        private int TrackFrames => Style switch { StyleNeedle => NeedleTrackFrames, StyleRam => RamTrackFrames, _ => NutTrackFrames };
        private int LockFrames => Style switch { StyleNeedle => NeedleLockFrames, StyleRam => RamLockFrames, _ => NutLockFrames };
        private int LingerFrames => Style switch { StyleNeedle => NeedleLingerFrames, StyleRam => RamLingerFrames, _ => NutLingerFrames };
        private float LaneLength => Style switch { StyleNeedle => NeedleLaneLength, StyleRam => RamLaneLength, _ => NutLaneLength };
        private float CoreWidth => Style switch { StyleNeedle => 44f, StyleRam => 40f, _ => 22f };
        private float GlowWidth => Style switch { StyleNeedle => 90f, StyleRam => 96f, _ => 52f };
        private Color Warn => Style switch { StyleNeedle => NeedleWarn, StyleRam => RamWarn, _ => NutWarn };

        private int TelegraphFrames => TrackFrames + LockFrames;
        private int TotalLife => TelegraphFrames + LingerFrames;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        private bool InLinger => Elapsed >= TelegraphFrames;
        private bool PhaseLocked => Elapsed >= TrackFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 840;

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
                //迟入端：ai[2] 非零=服务端早已锁定，相位快进到锁定段，不重放追踪期
                if (AuthLocked) {
                    Projectile.timeLeft = LockFrames + LingerFrames;
                }
            }

            if (AuthLocked) {
                //权威锁定方向；起点=锁定时刻停更的自身位置
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!PhaseLocked) {
                //追踪期：跟随锚体（索引+类型双校验，防槽位复用）
                NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
                if (!anchor.Alives() || anchor.type != AnchorType) {
                    //锚体没了：攻击不会发生，预告随之消散
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = anchor.Center;
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    float aim = (Main.player[target].Center - Projectile.Center).ToRotation();
                    //冲压风味只承诺水平方向
                    Projectile.rotation = Style == StyleRam
                        ? (Math.Cos(aim) >= 0 ? 0f : MathHelper.Pi) : aim;
                }
            }
            //本地锁定后 rotation 冻结在最后追踪值，等待 ai[2] 权威纠偏

            int elapsed = Elapsed;
            if (elapsed == TrackFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.42f, Pitch = -0.3f, MaxInstances = 5 }, Projectile.Center);
            }
            if (elapsed == TelegraphFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = Style == StyleRam ? -0.4f : 0.2f, MaxInstances = 5 }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, Warn.R / 255f * 0.15f, Warn.G / 255f * 0.15f, Warn.B / 255f * 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            float strength;
            if (InLinger) {
                strength = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)LingerFrames, 0f, 1f) * 0.24f;
            }
            else {
                strength = fadeIn * (PhaseLocked ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //追踪期锚在活体上：补 gfxOffY（上坡步进补偿），锁定后位置已冻结不再补
            if (!PhaseLocked && !AuthLocked && AnchorIndex.TryGetNPC(out NPC anchor)
                && anchor.Alives() && anchor.type == AnchorType) {
                drawPos.Y += anchor.gfxOffY;
            }
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            Color warn = Warn;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (!PhaseLocked || InLinger) {
                //追踪期/余痕期：细芯 + 宽柔光
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, CoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, GlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定段：白热窄闪，宣告轨迹已承诺
                float lockT = MathHelper.Clamp((elapsed - TrackFrames) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (GlowWidth + 18f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, new Color(255, 246, 228, 0) * (0.85f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (CoreWidth - 8f) / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
