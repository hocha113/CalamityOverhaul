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
    /// 间歇泉行军（P1+）：单螯高举 → 砸地 → 6 根间歇泉沿地面向玩家方向行军
    /// （逐根错帧 9f，每根自带 26f 预告——预告即本体）。
    /// 行军线砸地即锁（承诺）；声明式缺口 = GeyserStep 步距 + 有限根数
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.GeyserMarch, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpGeyserMarchState : SeaShrimpStateBase
    {
        public override string StateName => "GeyserMarch";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.GeyserMarch;

        private const int RaiseEnd = 24;
        private const int SlamFrame = 30;
        private const int Total = 78;
        private const int BaseOmen = 26;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            Vector2 down = ctx.Owner.Skeleton.HeadDown;
            float groundY = FindGroundY(npc.Center);
            Vector2 slamPoint = new(npc.Center.X, groundY);

            if (t < RaiseEnd) {
                //举螯蓄势：近螯高举过顶
                float w = t / (float)RaiseEnd;
                ctx.Claws[0] = new ClawDirective {
                    Mode = ClawMode.Hold,
                    Target = npc.Center - down * (100f + w * 120f) + ctx.Owner.Skeleton.Lateral(0) * 40f,
                    Spring = 0.3f,
                    Damping = 0.7f,
                    ClawOpen = w * 0.6f,
                };
                ctx.SpineCurl = -0.18f * w;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.8f);
                if (t == 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, npc.Center);
                }
                return null;
            }

            if (t == SlamFrame) {
                //砸地帧：行军线在此锁定（承诺）；泉眼一次性布满，逐根错帧自演
                ctx.Owner.Skeleton.Arms[0].Impulse(down * 44f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = -0.4f, MaxInstances = 2 }, slamPoint);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 2 }, slamPoint);
                    ShakeNearby(npc.Center, 4.5f);
                    ctx.AddRing(slamPoint + new Vector2(0f, -4f), 200f, 22, 0.4f);
                    EverdeepVFX.SplashBurst(slamPoint, Vector2.UnitY * 9f, 0.9f);
                }
                if (!VaultUtils.isClient) {
                    float dir = MathF.Sign(ctx.Target.Center.X - npc.Center.X);
                    if (dir == 0f) {
                        dir = 1f;
                    }
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.GeyserDamage);
                    for (int i = 0; i < SeaShrimpDirector.GeyserCount; i++) {
                        float spoutX = npc.Center.X + dir * SeaShrimpDirector.GeyserStep * (i + 1);
                        float spoutGroundY = FindGroundY(new Vector2(spoutX, npc.Center.Y - 200f));
                        float height = 230f + i % 3 * 28f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            new Vector2(spoutX, spoutGroundY), Vector2.Zero,
                            ModContent.ProjectileType<SeaShrimpGeyserSpout>(), damage, 2f,
                            Main.myPlayer, BaseOmen + i * SeaShrimpDirector.GeyserStagger, height);
                    }
                }
            }

            if (t >= SlamFrame && t <= SlamFrame + 12) {
                //砸地持位
                ctx.Claws[0] = new ClawDirective {
                    Mode = ClawMode.Strike,
                    Target = slamPoint,
                    Spring = 0.55f,
                    Damping = 0.85f,
                    ClawOpen = 0f,
                };
            }

            if (t >= Total) {
                return EndAttack(ctx, 48);
            }
            return null;
        }
    }
}
