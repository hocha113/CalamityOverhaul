using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 坠星祷(P1 起):司祭仰祷,天穹按拍标定数列落点,星矢沿预告柱错拍坠下,滚动数波成星雨<br/>
    /// 公平阀:落点出手拍锁定不追踪;巷心恒等距(LaneSpacing),巷内抖动有界(LaneJitterX),<br/>
    /// 最窄走廊 = LaneSpacing - 2*LaneJitterX - 2*HitHalf ≈ 142px 恒可穿行;<br/>
    /// 每矢自带预告柱(CultistFallingStar 声明),横移一巷即可避
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Starfall, typeof(CultistStateContext))]
    internal class CultistStarfallState : CultistStateBase
    {
        public override string StateName => "CultistStarfall";
        public override CultistStateIndex StateIndex => CultistStateIndex.Starfall;

        /// <summary>首拍与波间隔:相邻两波过玩家高度差约半秒,一波歇一波压</summary>
        private const int FirstBeat = 24;
        private const int BeatGap = 36;
        /// <summary>声明巷距(px):相邻坠道巷心恒等距</summary>
        private const float LaneSpacing = 260f;
        /// <summary>巷内落点抖动上限(px):散排用,走廊下限见类注释</summary>
        private const float LaneJitterX = 39f;
        /// <summary>标高抖动(px):纯纵向散排,不改巷几何</summary>
        private const float SkyJitterY = 64f;
        /// <summary>落点高度(玩家上方 px)</summary>
        private const float SkyHeight = 760f;
        /// <summary>绝对超时兜底(大于最晚自然收手 252)</summary>
        private const int Timeout = 320;

        private static int LaneCount(CultistStateContext context) =>
            context.Phase >= 4 || context.IsDeathMode ? 6 : 5;

        /// <summary>波数:后期/死亡模式加一波</summary>
        private static int WaveCount(CultistStateContext context) =>
            context.Phase >= 4 || context.IsDeathMode ? 5 : 4;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 13);
            FaceTarget(npc, player.Center);
            context.PushAura(0.8f, CultistMotion.PhaseCore(context.Phase));
            context.OrreryGlow = MathHelper.Max(context.OrreryGlow, 0.5f);

            //高位仰祷:星是从天上求下来的
            Vector2 hover = player.Center + new Vector2(npc.Center.X < player.Center.X ? -380f : 380f, -340f)
                + CultistMotion.BreathingOffset(seed: 17.1f, 8f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 16f);

            int waves = WaveCount(context);
            int lastBeat = FirstBeat + (waves - 1) * BeatGap;

            //起祷音+祷文上涌
            if (Timer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.85f, Pitch = -0.35f }, npc.Center);
            }
            if (Timer % 9 == 0 && Timer < lastBeat) {
                CultistMotion.RuneBurst(npc.Center + new Vector2(0f, -36f),
                    CultistMotion.PhaseCore(context.Phase), 1, 4f);
            }

            //滚动落拍:每拍天穹标定一梳落点,锁定当拍玩家位不追踪
            for (int wave = 0; wave < waves; wave++) {
                if (Timer != FirstBeat + wave * BeatGap) {
                    continue;
                }
                CultistScreenFX.PushFlash(0.18f);
                CultistMotion.Shake(npc.Center, 3f, 7);
                context.ScalePulse = 1.09f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.9f, Pitch = 0.15f + wave * 0.06f }, npc.Center);
                }
                if (VaultUtils.isClient) {
                    continue;
                }
                //整梳落巷:奇数波错半巷+整梳随机相移,连拍不踩同一组坠道;
                //巷内每星再抖 X/标高,ai[2] 种子派生错拍落速,散排不成行
                int lanes = LaneCount(context);
                float combShift = (wave % 2 == 1 ? LaneSpacing * 0.5f : 0f)
                    + Main.rand.NextFloat(-0.25f, 0.25f) * LaneSpacing;
                for (int lane = 0; lane < lanes; lane++) {
                    float offsetX = (lane - (lanes - 1) * 0.5f) * LaneSpacing + combShift
                        + Main.rand.NextFloat(-LaneJitterX, LaneJitterX);
                    Vector2 pos = new(player.Center.X + offsetX,
                        player.Center.Y - SkyHeight + Main.rand.NextFloat(-SkyJitterY, SkyJitterY));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<CultistFallingStar>(), 42, 0f, Main.myPlayer,
                        npc.whoAmI, context.Phase, Main.rand.NextFloat());
                }
                npc.netUpdate = true;
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:末波坠过玩家高度即收(残星自巡自灭),或超时兜底
            if (Timer >= lastBeat + 84) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }
    }
}
