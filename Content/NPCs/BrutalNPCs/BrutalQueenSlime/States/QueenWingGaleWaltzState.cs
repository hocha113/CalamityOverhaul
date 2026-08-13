using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>翼压风场：侧翼就位→振翅蓄势→掠空滑翔，身后铺设推挤风道+尾向羽晶</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.WingGaleWaltz, typeof(QueenSlimeStateContext))]
    internal class QueenWingGaleWaltzState : QueenSlimeStateBase
    {
        public override string StateName => "WingGaleWaltz";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.WingGaleWaltz;

        private const int SetupTime = 44;
        private const int PoiseTime = 22;
        private const int GlideTime = 58;
        private const int PassLength = SetupTime + PoiseTime + GlideTime;//124

        private int PassCount(QueenSlimeStateContext ctx) => ctx.IsDeathMode ? 3 : 2;

        private int currentPass = -1;
        private Vector2 glideDir;
        private Vector2 glideStart;
        private bool laneSpawned;

        public QueenWingGaleWaltzState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            currentPass = -1;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            int pass = Timer / PassLength;
            int t = Timer % PassLength;

            if (pass >= PassCount(context)) {
                DisableContactDamage(npc);
                if (t >= 24 && !VaultUtils.isClient) {
                    return new QueenAerialBalletState();
                }
                return null;
            }

            //新趟初始化：确定滑翔线(高度对齐玩家，方向左右交替+末趟斜线)
            if (pass != currentPass) {
                currentPass = pass;
                laneSpawned = false;
                int side = npc.Center.X < player.Center.X ? -1 : 1;
                float diag = pass == PassCount(context) - 1 ? 0.22f : 0f;
                glideDir = new Vector2(-side, diag * (player.Center.Y > npc.Center.Y ? 1f : -1f)).SafeNormalize(Vector2.UnitX);
                glideStart = player.Center + new Vector2(side * 620f, -40f - pass * 60f);
            }

            if (t < SetupTime) {
                //就位
                DisableContactDamage(npc);
                float p = t / (float)SetupTime;
                QueenMotion.SpringHover(npc, glideStart, 0.02f + p * 0.014f, 0.11f, 26f);
                QueenMotion.FlightLean(npc);
                context.PoseCommand = 5;
                FaceTarget(npc, player.Center);
            }
            else if (t < SetupTime + PoiseTime) {
                //振翅蓄势：定身+翼光渐盛，末3帧全静(可读前摇)
                DisableContactDamage(npc);
                float p = (t - SetupTime) / (float)PoiseTime;
                npc.velocity *= 0.78f;
                context.PoseCommand = 5;
                context.WingFlapBoost = 1.5f;
                context.SetChargeState(1, p);
                //蓄势末拍反向后仰
                Vector2 pullback = -glideDir * QueenMotion.LateSnap(p, 6) * 2.2f;
                npc.velocity += pullback;

                if (t == SetupTime + 4) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.75f, Pitch = 0.3f }, npc.Center);
                }
            }
            else {
                //滑翔掠空
                int gt = t - SetupTime - PoiseTime;
                if (gt == 0) {
                    npc.velocity = glideDir * (context.IsDeathMode ? 24f : 21f);
                    if (!VaultUtils.isClient) {
                        npc.netUpdate = true;
                    }
                    context.PushSquash(0.5f);
                    SoundEngine.PlaySound(SoundID.Item160 with { Volume = 0.8f, Pitch = 0.25f }, npc.Center);
                }

                EnableContactDamageIfFast(npc, 14f);
                context.PoseCommand = 5;
                context.WingFlapBoost = 1.6f;
                context.AfterimageBoost = Math.Max(context.AfterimageBoost, 0.9f);
                QueenMotion.FlightLean(npc, 0.045f, 0.5f);

                //滑到中段铺设风道(服务端一次)
                if (!laneSpawned && gt == GlideTime / 2 && !VaultUtils.isClient) {
                    laneSpawned = true;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<QueenGaleFieldProj>(), 0, 0f, Main.myPlayer,
                        glideDir.ToRotation(), 0f, currentPass * 0.31f);

                    //尾向羽晶扇(4发朝身后扩散)
                    for (int i = 0; i < 4; i++) {
                        float spread = MathHelper.Lerp(-0.5f, 0.5f, i / 3f);
                        Vector2 vel = (-glideDir).RotatedBy(spread) * 7.5f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                            ModContent.ProjectileType<QueenShardProj>(), QueenShardProj.ShardDamage, 0f, Main.myPlayer,
                            (int)QueenShardProj.Mode.Shard, 0f, i * 0.22f);
                    }
                }

                //滑翔期羽光尘
                if (!VaultUtils.isServer && gt % 2 == 0) {
                    Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(50f, 30f),
                        DustID.TintableDust, -glideDir * Main.rand.NextFloat(2f, 5f), 130,
                        QueenMotion.GetQueenDustColor(), 1.5f);
                    d.noGravity = true;
                }
            }

            return null;
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}
