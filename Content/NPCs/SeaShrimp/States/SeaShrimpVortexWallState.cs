using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 双渊柱封场（P2 进场事件 / 蜕壳后 P3 刷新）：昂身怒吼 → 双螯先后砸地两拍
    /// （震屏+贴地冲击环+溅泉）→ 场心两侧 ±ArenaHalfWidth 各升起一根封场巨龙卷
    /// （生长期即预告）→ 收势回 Hub。OnEnter 时 Phase==1 则本状态承担 P2 转场
    /// （置 Phase+清弹，镜像蜕壳态的做法）；Phase≥3 为刷新变体（跳过转场件）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.VortexWall, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpVortexWallState : SeaShrimpStateBase
    {
        public override string StateName => "VortexWall";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.VortexWall;

        private const int RearEnd = 30;
        private const int SlamA = 38;
        private const int SlamB = 54;
        private const int SummonFrame = 62;
        private const int WatchEnd = 138;
        private const int Total = 166;

        public override void OnEnter(SeaShrimpStateContext ctx) {
            base.OnEnter(ctx);
            //P3 刷新变体：蜕壳演出刚放完,跳过昂身直接进砸地(不再叠一整段大前摇)
            if (ctx.Phase >= 3) {
                Timer = RearEnd - 6;
            }
        }

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);
            npc.dontTakeDamage = t < WatchEnd;

            Vector2 down = ctx.Owner.Skeleton.HeadDown;
            float groundY = FindGroundY(npc.Center);

            if (t < RearEnd) {
                //昂身蓄势：双螯高举，怒吼渐起
                float w = t / (float)RearEnd;
                Vector2 up = -down;
                for (int a = 0; a < 2; a++) {
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Hold,
                        Target = npc.Center + up * (90f + w * 110f) + ctx.Owner.Skeleton.Lateral(a) * 70f,
                        Spring = 0.28f,
                        Damping = 0.7f,
                        ClawOpen = w,
                    };
                }
                ctx.SpineCurl = -0.3f * w;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w);
                if (t == 6 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
                }
                return null;
            }

            //双螯先后砸地两拍
            if (t == SlamA || t == SlamB) {
                int arm = t == SlamA ? 0 : 1;
                Vector2 slamPoint = new(npc.Center.X + (arm == 0 ? 70f : -70f), groundY);
                ctx.Owner.Skeleton.Arms[arm].Impulse(down * 46f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.45f, MaxInstances = 2 }, slamPoint);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = -0.25f, MaxInstances = 2 }, slamPoint);
                    ShakeNearby(npc.Center, arm == 0 ? 5f : 7f);
                    ctx.AddRing(slamPoint + new Vector2(0f, -4f), arm == 0 ? 240f : 320f, 26, 0.4f);
                    EverdeepVFX.SplashBurst(slamPoint, Vector2.UnitY * 11f, 1.1f);
                }
            }
            if (t >= SlamA && t <= SlamB + 10) {
                int arm = t < SlamB ? 0 : 1;
                ctx.Claws[arm] = new ClawDirective {
                    Mode = ClawMode.Strike,
                    Target = new Vector2(npc.Center.X + (arm == 0 ? 70f : -70f), groundY),
                    Spring = 0.55f,
                    Damping = 0.85f,
                    ClawOpen = 0f,
                };
            }

            if (t == SummonFrame) {
                //召唤帧：本状态承担 P2 转场（刷新变体跳过），随后双柱破土
                float centerX = npc.Center.X;
                ctx.ArenaActive = true;
                ctx.ArenaCenterX = centerX;
                if (!VaultUtils.isClient) {
                    if (ctx.Phase <= 1) {
                        ctx.Phase = 2;
                        npc.netUpdate = true;
                        SeaShrimpBoss.ClearHostileProjectiles();
                    }
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.VortexDamage);
                    for (int s = -1; s <= 1; s += 2) {
                        float wallX = centerX + s * SeaShrimpDirector.ArenaHalfWidth;
                        float wallGroundY = FindGroundY(new Vector2(wallX, npc.Center.Y - 200f));
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            new Vector2(wallX, wallGroundY), Vector2.Zero,
                            ModContent.ProjectileType<SeaShrimpVortexWall>(), damage, 3f,
                            Main.myPlayer, SeaShrimpDirector.VortexWallHeight, npc.whoAmI);
                    }
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
                    ShakeNearby(npc.Center, 6f, 2200f);
                }
            }

            if (t > SummonFrame && t < WatchEnd) {
                //观柱拍：双柱升起，低鸣与远震持续（封场宣告）
                ctx.CrystalGlow = 1f;
                if (!Main.dedServ && t % 16 == 0) {
                    ShakeNearby(npc.Center, 1.6f, 2400f);
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 2 }, npc.Center);
                }
                return null;
            }

            if (t >= Total) {
                return EndAttack(ctx, 46);
            }
            return null;
        }
    }
}
