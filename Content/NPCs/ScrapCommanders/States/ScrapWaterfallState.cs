using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 废钢瀑布：五道天坠柱预警（虚线柱 + 落点尘圈）→ 磁力把屏幕外的废料
    /// 拉成一排排砸下来，柱间缝隙是活路。统帅在高位持场，磁场外掷位读法
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.Waterfall, typeof(ScrapStateContext))]
    internal class ScrapWaterfallState : ScrapStateBase
    {
        public override string StateName => "Waterfall";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.Waterfall;

        //==================== 时序 ====================

        private const int RainBeat = 50;
        private const int RainEnd = 108;
        private const int StateEnd = 126;
        private const int ColumnCount = 5;
        /// <summary>每柱两件，错拍落体</summary>
        private const int SpawnGap = 6;

        private readonly float[] columnX = new float[ColumnCount];
        private readonly float[] columnGroundY = new float[ColumnCount];
        private float skyY;
        /// <summary>已落的最高件号（单调闩）</summary>
        private int lastDrop = -1;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            if (t == 0) {
                if (ctx.Owner.TargetInvalid()) {
                    return EndAttack(ctx, 45);
                }
                //五柱锚在玩家当下的位置：躲法是横向换缝
                for (int i = 0; i < ColumnCount; i++) {
                    columnX[i] = ctx.Target.Center.X + (i - ColumnCount / 2) * 150f;
                    //悬空兜底：柱底不超过玩家脚下一段
                    columnGroundY[i] = System.MathF.Min(
                        FindGroundY(new Vector2(columnX[i], ctx.Target.Center.Y - 40f)),
                        ctx.Target.Center.Y + 360f);
                }
                skyY = ctx.Target.Center.Y - 640f;
                owner.EnsureMagnetFieldProj();
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.5f, Pitch = 0.05f, MaxInstances = 1 }, npc.Center);
            }

            //统帅撤到高位持场
            Vector2 anchor = ctx.Target.Center + new Vector2(0f, -330f);
            GlideToward(ctx, anchor, 0.035f, 9f, 0.08f);
            LeanByVelocity(npc, 0.08f);
            ctx.MagnetGlow = MathHelper.Clamp(t / 30f, 0f, 0.85f);
            ctx.MagnetPull = -1f;

            if (t < RainBeat) {
                //==================== 天坠柱预警 ====================
                float alpha = MathHelper.Clamp((t - 6) / (float)(RainBeat - 14), 0f, 1f);
                for (int i = 0; i < ColumnCount; i++) {
                    ctx.AddTelegraph(new Vector2(columnX[i], skyY), Vector2.UnitY,
                        columnGroundY[i] - skyY, alpha * 0.85f, 0.5f);
                    //落点尘圈
                    if (!Main.dedServ && t % 6 == i) {
                        Dust dust = Dust.NewDustPerfect(
                            new Vector2(columnX[i] + Main.rand.NextFloat(-20f, 20f), columnGroundY[i] - 4f),
                            DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                            130, default, Main.rand.NextFloat(0.8f, 1.2f));
                        dust.noGravity = true;
                    }
                }
                if (t % 16 == 4) {
                    SoundEngine.PlaySound(SoundID.Item15 with {
                        Volume = 0.32f,
                        Pitch = -0.5f + t / (float)RainBeat * 0.7f,
                        MaxInstances = 2
                    }, npc.Center);
                }
                Timer++;
                return null;
            }

            if (t < RainEnd) {
                //==================== 落体雨 ====================
                int drop = (t - RainBeat) / SpawnGap;
                int totalDrops = ColumnCount * 2;
                if ((t - RainBeat) % SpawnGap == 0 && drop < totalDrops && lastDrop < drop) {
                    lastDrop = drop;
                    //列序洗牌：0,3,1,4,2 的错列节奏，不从一头平推
                    int col = drop * 3 % ColumnCount;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with {
                        Volume = 0.4f,
                        Pitch = 0.3f,
                        MaxInstances = 3
                    }, new Vector2(columnX[col], skyY + 200f));
                    if (!VaultUtils.isClient) {
                        int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.GroundSawDamage);
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            new Vector2(columnX[col] + Main.rand.NextFloat(-26f, 26f), skyY),
                            new Vector2(0f, 9f),
                            ModContent.ProjectileType<ScrapDebris>(), damage, 3f,
                            Main.myPlayer, -1f);
                    }
                }
                //残余柱标（渐隐）
                float fade = MathHelper.Clamp((RainEnd - t) / 30f, 0f, 0.5f);
                for (int i = 0; i < ColumnCount; i++) {
                    ctx.AddTelegraph(new Vector2(columnX[i], skyY), Vector2.UnitY,
                        columnGroundY[i] - skyY, fade, 0.4f);
                }
                Timer++;
                return null;
            }

            //==================== 收势 ====================
            npc.velocity *= 0.92f;
            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 85);
            }
            return null;
        }
    }
}
