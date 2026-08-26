using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 蛾怪三连俯冲预兆：ai[0]=Mothron索引 ai[1]=段序(0/1/2) ai[2]=锁定方向+10（0=未锁定）。
    /// 每段独立锁定（预告≥40帧，小Boss条款），追踪期直读目标方向，
    /// 锁定帧后冻结并由服务端写 ai[2] 权威纠偏（预告即承诺）；
    /// 俯冲期保留为余痕，可见窗覆盖整个俯冲执行窗。永不造成伤害
    /// </summary>
    internal class EclMothronDiveOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>首段/后续段预告帧（小Boss签名技契约 ≥40）</summary>
        internal const int TelegraphFirst = 46;
        internal const int TelegraphNext = 42;
        /// <summary>预告末段锁定帧</summary>
        internal const int LockFrames = 14;
        /// <summary>俯冲执行窗帧（与 EclMothronNPC 的俯冲相位同源）</summary>
        internal const int StrikeFrames = 30;
        /// <summary>警示线长度（大于理论行程，包住俯冲期残余转向）</summary>
        internal const float LaneLength = 640f;

        private const float CoreWidth = 32f;
        private const float GlowWidth = 88f;

        private static readonly Color DuskGold = new Color(255, 172, 60, 0);
        private static readonly Color EclipseRed = new Color(240, 66, 44, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int Segment => (int)Projectile.ai[1];
        private bool Locked => Projectile.ai[2] != 0f;
        internal int TelegraphFrames => Segment == 0 ? TelegraphFirst : TelegraphNext;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= TelegraphFrames;
        private bool InLockPhase => !InStrike && Elapsed >= TelegraphFrames - LockFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFirst + StrikeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TelegraphFrames + StrikeFrames;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入端：ai[2] 非零=已过锁定帧，相位快进到锁定段起点
                if (Locked) {
                    Projectile.timeLeft = StrikeFrames + LockFrames;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.55f }, Projectile.Center);
                }
            }

            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != NPCID.Mothron) {
                //蛾怪没了：俯冲不会发生，预兆消散
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Locked) {
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!InLockPhase) {
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }

            if (Elapsed == TelegraphFrames - LockFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = -0.2f }, Projectile.Center);
            }
            if (Elapsed == TelegraphFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.42f, Pitch = 0.5f }, Projectile.Center);
            }

            //俯冲执行窗镜像戳：护巢狂怒的巡航推进据此豁免（实体已同步，各端一致），
            //已承诺的俯冲弧线不吃狂怒推进——速度阈值兜不住档3（14/1.8≈7.78 低于阈值 8）
            if (InStrike && anchor.TryGetGlobalNPC(out EclMothronNPC mothron)) {
                mothron.StampDive();
            }

            //预告期沿线渗出警示尘（预算：至多 1 粒/帧）
            if (!InStrike && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat(0.1f, 0.6f);
                Dust seep = Dust.NewDustPerfect(Projectile.Center + Projectile.rotation.ToRotationVector2() * LaneLength * along,
                    DustID.Torch, Vector2.Zero, 160, default, 0.8f);
                seep.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.24f, 0.14f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.25f;
            }
            else {
                strength = fadeIn * (Locked || InLockPhase ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D line = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, line.Height / 2f);
            float scaleX = LaneLength / line.Width;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity);

            if (!Locked && !InLockPhase || InStrike) {
                //追踪期/余痕期：暮金宽光 + 蚀红细芯
                Main.EntitySpriteDraw(line, drawPos, null, DuskGold * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, GlowWidth / line.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(line, drawPos, null, EclipseRed * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, CoreWidth / line.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪宣告承诺，段序点标出当前是三连中的第几段
                float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Main.EntitySpriteDraw(line, drawPos, null, EclipseRed * (0.7f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (GlowWidth + 22f) / line.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 246, 226, 0) * (0.9f * flash * strength),
                    Projectile.rotation, origin, new Vector2(scaleX, (CoreWidth - 10f) / line.Height), SpriteEffects.None, 0);
            }

            //段序读法：线源上方点亮 1-3 个小点（第几段=第几个点亮）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            for (int i = 0; i < 3; i++) {
                bool lit = i <= Segment;
                Vector2 dot = drawPos + new Vector2(-16f + 16f * i, -30f);
                Main.EntitySpriteDraw(glow, dot, null,
                    (lit ? EclipseRed : DuskGold) * (strength * (lit ? 0.8f : 0.25f)), 0f,
                    glow.Size() / 2f, lit ? 0.17f : 0.11f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
