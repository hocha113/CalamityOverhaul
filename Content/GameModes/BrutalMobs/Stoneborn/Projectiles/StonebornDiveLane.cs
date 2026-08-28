using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 花岗岩精·弧光俯冲标线：ai[0]=锚NPC索引 ai[1]=锚NPC类型 ai[2]=锁定方向+10（0=未锁定）。
    /// 锁定语义三段：追踪（直读目标方向）→ 锁定帧后冻结（预告即承诺）→ 突进余痕。
    /// 本包独立实现，不跨包引用（与夜行包的俯冲线只做形状镜像）。
    /// 服务端在锁定帧写 ai[2] 作权威纠偏；锚体死亡即消散（击杀=有效反制）。永不参与伤害
    /// </summary>
    internal class StonebornDiveLane : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>预告总帧（任务契约 ≥32，档位一律不缩短）</summary>
        internal const int TelegraphFrames = 32;
        /// <summary>末段锁定帧（进入即冻结方向）</summary>
        internal const int LockFrames = 12;
        /// <summary>突进窗帧（=俯冲包络全长，余痕与判窗同长）</summary>
        internal const int StrikeFrames = 28;
        /// <summary>标线长度</summary>
        private const float LaneLength = 480f;
        /// <summary>芯宽与柔光宽：画宽于怪体，把原版悬浮 AI 的残余漂移包进警示范围</summary>
        private const float LaneCoreWidth = 22f;
        private const float LaneGlowWidth = 56f;
        /// <summary>电弧抖动横踢数量（确定性，不吃 Main.rand）</summary>
        private const int ArcTickCount = 3;

        private int AnchorIndex => (int)Projectile.ai[0];
        private int AnchorType => (int)Projectile.ai[1];
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= TelegraphFrames;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 640;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + StrikeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害（接触微伤由残电实体承担）</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TelegraphFrames + StrikeFrames;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入玩家：首帧 ai[2] 已非零=服务端早过锁定帧，本地相位快进到锁定起点，
                //不重放整段追踪期（方向本就走 ai[2] 权威分支，此处只对齐相位与判窗）
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = StrikeFrames + LockFrames;
                }
            }

            if (!AnchorIndex.TryGetNPC(out NPC anchor) || !anchor.Alives() || anchor.type != AnchorType) {
                //锚定怪没了：俯冲不会发生（或已中断），标线随之消散
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Projectile.ai[2] != 0f) {
                //服务端已写入权威锁定方向
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //追踪期：直读目标方向（各端从同步数据确定性推得，无插值）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }
            //锁定后 rotation 冻结在最后追踪值，等待/无需 ai[2] 纠偏

            if (!VaultUtils.isServer) {
                if (Elapsed == TelegraphFrames - LockFrames) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.45f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
                }
                if (Elapsed == TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { Volume = 0.6f, Pitch = 0.25f, MaxInstances = 4 }, Projectile.Center);
                }
                //沿线电尘（≤2 粒/帧）
                if (!InStrike && Main.rand.NextBool(2)) {
                    float along = Main.rand.NextFloat(30f, LaneLength * 0.7f);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + Projectile.rotation.ToRotationVector2() * along,
                        DustID.Electric, Vector2.Zero, 110, default, 0.7f);
                    dust.noGravity = true;
                    dust.velocity = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2()
                        * MathF.Sin(along * 0.11f + Main.GlobalTimeWrappedHourly * 8f) * 0.9f;
                }
            }
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.16f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                //突进期余痕：可见窗与突进窗同一实体
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            Color warnCore = GraniteMarbleVFX.GraniteSpark with { A = 0 };
            Color warnDeep = GraniteMarbleVFX.GraniteDeep with { A = 0 };
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);

            if (!Locked || InStrike) {
                //追踪期/余痕期：细芯 + 宽柔光
                Main.EntitySpriteDraw(tex, drawPos, null, warnCore * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warnDeep * (0.32f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白蓝窄闪，宣告轨迹已承诺
                float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Color hot = new Color(230, 246, 255, 0) * (0.85f * flash * strength);
                Main.EntitySpriteDraw(tex, drawPos, null, warnCore * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 16f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, hot, Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 6f) / tex.Height), SpriteEffects.None, 0);
            }

            //电弧横踢：沿线三处垂直小闪（确定性相位，读作电弧在线上爬）
            if (!InStrike) {
                Vector2 along = Projectile.rotation.ToRotationVector2();
                Vector2 side = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
                for (int i = 1; i <= ArcTickCount; i++) {
                    float t = i / (ArcTickCount + 1f)
                        + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.3f + i * 2.4f + Projectile.identity);
                    Vector2 p = drawPos + along * (LaneLength * t);
                    float tick = MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + i * 1.9f + Projectile.identity * 0.7f);
                    Main.EntitySpriteDraw(glow, p + side * tick * 6f, null, warnCore * (0.5f * strength * MathF.Abs(tick)),
                        Projectile.rotation + MathHelper.PiOver2, glow.Size() / 2f,
                        new Vector2(0.5f, 0.12f), SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
