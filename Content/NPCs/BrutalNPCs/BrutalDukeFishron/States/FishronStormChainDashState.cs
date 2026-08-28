using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 风暴连突：隐入雨幕→环位重现→短预告→直线贯穿，循环数次。
    /// 每一冲仍是可预读直线+水迹（预告更短但恒定），瞬移只发生在冲刺之间
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.StormChainDash, typeof(FishronStateContext))]
    internal class FishronStormChainDashState : FishronStateBase
    {
        public override string StateName => "StormChainDash";
        public override FishronStateIndex StateIndex => FishronStateIndex.StormChainDash;

        //末相二压：预告 18→13 帧（-30%），遁走/拖刹空拍 10→7 帧（实测全砍读作零间隔，回填一口喘息），
        //冲速 56→64.4（+15%），贯穿 16→14 帧保住原冲程 ~900px——每轮 44→34 帧
        private const int VanishEnd = 3;
        private const int TelegraphEnd = 16;
        private const int DashEnd = 30;
        private const int RepLength = 34;
        private const float DashSpeed = 64.4f;

        public FishronStormChainDashState() {
        }

        //每轮冲刺方向，预告锁定帧冻结
        private Vector2 frozenDashDir;

        private static int MaxReps(FishronStateContext ctx) => ctx.IsAsuraMode ? 5 : 4;

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            int t = (int)Timer % RepLength;
            if (Timer >= RepLength * MaxReps(context)) {
                return new FishronHoverState();
            }

            //相位a：化雨遁走，雾中消隐，服务端择位重投
            if (t < VanishEnd) {
                frozenDashDir = Vector2.Zero;
                //遁走仅 3 帧，淡出一步到位，消隐深度不打折；雾每帧都放，盖住急促的消隐
                npc.alpha = Math.Min(npc.alpha + 115, 220);
                npc.velocity *= 0.85f;
                if (!VaultUtils.isServer) {
                    FishronMotionFX.SpawnMist(npc.Center, Vector2.Zero, 1f, 2);
                }
                if (t == VanishEnd - 1 && !VaultUtils.isClient) {
                    //环位重现：绕玩家半径 560 随机方位（服务端裁决，netUpdate 广播）
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    npc.Center = player.Center + angle.ToRotationVector2() * 560f;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;

                    //落位即亮短预告（13 帧短于线体锁定窗 14：出线即锁死，整段预告都是承诺）；
                    //服务端在此同帧冻结冲向，线与冲刺严格同向
                    frozenDashDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        frozenDashDir,
                        ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, player.whoAmI, FishronTelegraph.PackParams(0, TelegraphEnd - VanishEnd));
                }
                return null;
            }

            //相位b：雨中显形+后撤蓄势
            if (t < TelegraphEnd) {
                npc.alpha = Math.Max(npc.alpha - 26, 60);
                float progress = (t - VanishEnd) / (float)(TelegraphEnd - VanishEnd);
                //预告全程锁线：远端没赶上服务端冻结帧就在显形首帧补冻，此后绝不再变
                if (frozenDashDir == Vector2.Zero) {
                    frozenDashDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                }
                context.SetChargeState(3, progress);
                context.DashDirection = frozenDashDir;
                context.FrameCommand = 1;

                npc.velocity = Vector2.Lerp(npc.velocity, -frozenDashDir * (1f + progress * 5f), 0.25f);
                FaceBody(npc, npc.Center + frozenDashDir * 100f, 0.3f);

                if (t == VanishEnd && !VaultUtils.isServer) {
                    FishronMotionFX.SpawnMist(npc.Center, Vector2.Zero, 1.1f, 3);
                    SoundEngine.PlaySound(SoundID.NPCHit14 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 4 }, npc.Center);
                }
                return null;
            }

            //起冲帧
            if (t == TelegraphEnd) {
                Vector2 dir = frozenDashDir == Vector2.Zero
                    ? (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) : frozenDashDir;
                npc.velocity = dir * DashSpeed;
                npc.alpha = 30;
                npc.netUpdate = true;
                FishronMotionFX.SpawnDashBurst(npc.Center, dir, 0.95f);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.85f, Pitch = 0.3f, MaxInstances = 4 }, npc.Center);
            }

            //相位c：直线贯穿
            if (t < DashEnd) {
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    FishronMotionFX.SpawnSprayCone(npc.Center, -npc.velocity.SafeNormalize(Vector2.UnitY),
                        1, 3f, 8f, 0.5f, 0.85f);
                }
                return null;
            }

            //相位d：残速拖刹，准备下一轮
            npc.velocity *= 0.86f;
            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.alpha = 0;
        }
    }
}
