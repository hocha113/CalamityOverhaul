using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.NightPack.Projectiles
{
    /// <summary>
    /// 俯冲预告线：ai[0]=锚NPC索引 ai[1]=风味(0恶魔眼/1洞穴蝙蝠) ai[2]=锁定方向+10（0=未锁定）。
    /// 追踪期直读目标方向，锁定帧后方向冻结（预告即承诺）；服务端在锁定帧写 ai[2] 作权威纠偏。
    /// 突进期保留为淡出余痕兼判定窗载体（蝙蝠命中黑暗减益据此判窗），永不造成伤害
    /// </summary>
    internal class NightDiveOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>恶魔眼：预告总帧（≥30 帧契约）/末段锁定帧/突进窗帧</summary>
        internal const int EyeTelegraphFrames = 36;
        internal const int EyeLockFrames = 16;
        internal const int EyeStrikeFrames = 30;
        internal const float EyeLaneLength = 560f;

        /// <summary>洞穴蝙蝠：预告总帧/锁定帧/突进窗帧</summary>
        internal const int BatTelegraphFrames = 32;
        internal const int BatLockFrames = 14;
        internal const int BatStrikeFrames = 26;
        internal const float BatLaneLength = 380f;

        /// <summary>预告线芯宽与柔光宽，画宽于怪体判定，把原版 AI 突进期的残余转向也包进警示范围</summary>
        private const float LaneCoreWidth = 26f;
        private const float LaneGlowWidth = 64f;

        private static readonly Color EyeWarn = new Color(255, 64, 84, 0);
        private static readonly Color BatWarn = new Color(222, 196, 138, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private bool IsBat => Projectile.ai[1] == 1f;
        private int TelegraphFrames => IsBat ? BatTelegraphFrames : EyeTelegraphFrames;
        private int LockFrames => IsBat ? BatLockFrames : EyeLockFrames;
        private int StrikeFrames => IsBat ? BatStrikeFrames : EyeStrikeFrames;
        private float LaneLength => IsBat ? BatLaneLength : EyeLaneLength;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= TelegraphFrames;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;

        /// <summary>受害端判定：该蝙蝠/恶魔眼当前是否处于俯冲突进窗</summary>
        internal static bool IsStrikeWindowFor(int npcIndex) {
            int type = ModContent.ProjectileType<NightDiveOmen>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && (int)proj.ai[0] == npcIndex
                    && proj.ModProjectile is NightDiveOmen omen && omen.InStrike) {
                    return true;
                }
            }
            return false;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EyeTelegraphFrames + EyeStrikeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧按风味套定总时长，各端由同步的 ai[1] 确定性得到相同值
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TelegraphFrames + StrikeFrames;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入玩家：首帧 ai[2] 已非零 = 服务端早过锁定帧的同步证据，本地相位快进到锁定起点，
                //不重放整段追踪期（方向本就走 ai[2] 权威分支，此处只对齐相位与判定窗）
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = StrikeFrames + LockFrames;
                }
            }

            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives()) {
                //锚定怪没了：俯冲不会发生（或已中断），预告随之消散
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Projectile.ai[2] != 0f) {
                //服务端已写入权威锁定方向
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //追踪期：直读目标方向（无插值，各端从同步数据确定性推得）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers) {
                    Player player = Main.player[target];
                    if (player.Alives()) {
                        Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
                    }
                }
            }
            //锁定后 rotation 冻结在最后追踪值，等待/无需 ai[2] 纠偏

            if (Elapsed == TelegraphFrames - LockFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.42f, Pitch = -0.3f }, Projectile.Center);
            }
            if (Elapsed == TelegraphFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = IsBat ? 0.55f : 0.1f }, Projectile.Center);
            }

            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            Color warn = IsBat ? BatWarn : EyeWarn;
            Lighting.AddLight(Projectile.Center, warn.R / 255f * 0.16f, warn.G / 255f * 0.16f, warn.B / 255f * 0.16f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                //突进期余痕：可见窗与判定窗同一实体
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.55f);
            }
            //暮雾联动（只读 Woodsong 信号）：浓雾里预告线更亮，萤火沿线指路
            float fog = Ambience.Woodsong.WoodsongAmbience.FogStrength;
            if (!InStrike && fog > 0.15f) {
                strength = Math.Min(1f, strength * (1f + fog * 0.4f));
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            Color warn = IsBat ? BatWarn : EyeWarn;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (!Locked || InStrike) {
                //追踪期/余痕期：细芯 + 宽柔光
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪，宣告轨迹已承诺
                float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Color core = new Color(255, 244, 224, 0) * (0.85f * flash * strength);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 20f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 8f) / tex.Height), SpriteEffects.None, 0);
            }

            //雾夜萤火沿线指路（纯表现，各端本地按自身雾浓度绘制）
            if (!InStrike && fog > 0.15f) {
                Texture2D glowDot = CWRAsset.SoftGlow.Value;
                Color firefly = new Color(186, 240, 120, 0);
                Vector2 along = Projectile.rotation.ToRotationVector2();
                Vector2 side = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
                for (int i = 1; i <= 3; i++) {
                    float t = i / 4f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + i * 2.1f + Projectile.identity);
                    Vector2 p = drawPos + along * (LaneLength * t)
                        + side * (10f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + i * 1.7f));
                    Main.EntitySpriteDraw(glowDot, p, null, firefly * (fog * 0.6f * strength), 0f,
                        glowDot.Size() / 2f, 0.045f, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
