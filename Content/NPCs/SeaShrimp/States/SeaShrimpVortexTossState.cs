using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 甩尾涡旋（P2+）：卷尾蓄势 → 横扫甩出 3 个行走巨龙卷
    /// （生成即定速定向不追踪=缺口保证，落位间距 ≥MiniVortexGap 声明式）。
    /// 龙卷 30f 生长期无伤（生长即预告），行军向玩家所在侧；
    /// 玩家悬空/搭台（脚下实地深过最大落差）时龙卷悬空生成随空行军
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.VortexToss, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpVortexTossState : SeaShrimpStateBase
    {
        public override string StateName => "VortexToss";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.VortexToss;

        private const int WindupEnd = 30;
        private const int Total = 70;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            if (t < WindupEnd) {
                //卷尾蓄势：迟滞后卷 + 尾扇收拢
                float w = t / (float)WindupEnd;
                float snap = MathF.Pow(w, 6f);
                ctx.SpineCurl = -(0.25f + 0.55f * snap);
                ctx.TailFlare = 0.3f;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.7f);
                ctx.WaveGain = 0.3f;
                if (t == 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = -0.45f, MaxInstances = 2 }, npc.Center);
                }
                return null;
            }

            if (t == WindupEnd) {
                //甩尾帧：尾扇横扫 + 三涡落位（间距=声明缺口，行军向玩家侧）
                ctx.SpineCurl = 0.4f;
                ctx.TailFlare = 1f;
                ctx.AfterimageStrength = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item86 with { Volume = 0.8f, Pitch = 0.05f, MaxInstances = 2 }, npc.Center);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.85f, Pitch = -0.2f, MaxInstances = 2 }, npc.Center);
                    ShakeNearby(npc.Center, 4f);
                    //甩尾水花弧：从尾扇甩向落位方向
                    Vector2 tailPos = ctx.Owner.Skeleton.Nodes[4].Pos;
                    EverdeepVFX.SplashBurst(tailPos, -ctx.Owner.Skeleton.Nodes[4].Forward * 9f, 1.1f);
                }
                if (!VaultUtils.isClient) {
                    float dir = MathF.Sign(ctx.Target.Center.X - npc.Center.X);
                    if (dir == 0f) {
                        dir = 1f;
                    }
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.VortexDamage);
                    for (int i = 0; i < 3; i++) {
                        //落位间距 ≥ MiniVortexGap：三涡沿玩家方向排开，行军同向。
                        //落差阀：从玩家上方扫地，实地深过最大落差 → 柱底悬空到玩家下方定距
                        float spawnX = npc.Center.X + dir * (SeaShrimpDirector.MiniVortexGap * (i + 1) + 60f);
                        float groundY = FindGroundY(new Vector2(spawnX, ctx.Target.Center.Y - 200f));
                        float spawnY = groundY - ctx.Target.Center.Y > SeaShrimpDirector.GroundAttackMaxDrop
                            ? ctx.Target.Center.Y + SeaShrimpDirector.AirSpawnBelow
                            : groundY;
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            new Vector2(spawnX, spawnY), Vector2.Zero,
                            ModContent.ProjectileType<SeaShrimpMiniVortex>(), damage, 2f,
                            Main.myPlayer, SeaShrimpDirector.MiniVortexHeight + i % 2 * 120f,
                            dir * SeaShrimpDirector.MiniVortexSpeed);
                    }
                }
            }

            if (t > WindupEnd && t < WindupEnd + 14) {
                //甩后余势：尾扇回摆
                ctx.SpineCurl = 0.4f * (1f - (t - WindupEnd) / 14f);
                ctx.TailFlare = 1f;
                return null;
            }

            if (t >= Total) {
                return EndAttack(ctx, 56);
            }
            return null;
        }
    }
}
