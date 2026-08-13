using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>三段猛扑：手风琴压缩蓄势→爆发直扑→硬刹回弹，节间距全程呼吸</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.LungeFlurry, typeof(EowStateContext))]
    internal class EowLungeFlurryState : EowStateBase
    {
        public override string StateName => "LungeFlurry";
        public override EowStateIndex StateIndex => EowStateIndex.LungeFlurry;

        #region 节奏常量
        private const int CoilTime = 26;
        private const int TravelTime = 20;
        private const int BrakeTime = 18;
        private const int LungeLength = CoilTime + TravelTime + BrakeTime;
        #endregion

        private int LungeCount(EowStateContext ctx) => ctx.IsPhase2 ? 4 : 3;
        private float LungeSpeed(EowStateContext ctx) => (ctx.IsPhase2 ? 62f : 55f) + (ctx.IsDeathMode ? 5f : 0f);

        private Vector2 lungeDir;

        public EowLungeFlurryState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
        }

        public override IEowState OnUpdate(EowStateContext context) {
            Tick();

            int total = LungeCount(context) * LungeLength;
            if (Timer > total) {
                return new EowWeaveState();
            }

            int inLunge = (Timer - 1) % LungeLength;
            UpdateLunge(context, inLunge);
            return null;
        }

        private void UpdateLunge(EowStateContext context, int inLunge) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //蓄势盘紧：反向漂移+体节压缩(肌肉收拢)
            if (inLunge < CoilTime) {
                context.SkipDefaultMovement = true;
                float coilT = inLunge / (float)CoilTime;

                //蓄势起帧：权威端生成导引线(蓄势全程可见，锁向前跟踪瞄准)
                if (inLunge == 0 && !VaultUtils.isClient) {
                    Terraria.Projectile.NewProjectile(npc.GetSource_FromAI(),
                        EowSpitBarrageState.MouthPos(npc),
                        (player.Center - npc.Center).SafeNormalize(Vector2.UnitX),
                        ModContent.ProjectileType<EowLungeOmen>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, CoilTime);
                }

                //压缩曲线t²，末3帧完全定住(前寂)
                context.Compression = MathHelper.Lerp(1f, 0.6f, coilT * coilT);
                Vector2 away = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
                if (inLunge < CoilTime - 3) {
                    //后撤蓄势+缓慢瞄准
                    npc.velocity = Vector2.Lerp(npc.velocity, away * 4.5f, 0.2f);
                    Vector2 aim = player.Center + player.velocity * 14f;
                    float targetRot = (aim - npc.Center).ToRotation() + MathHelper.PiOver2;
                    npc.rotation = npc.rotation.AngleLerp(targetRot, 0.16f);
                }
                else {
                    //全静帧起点锁向：预告即承诺，起扑不再重瞄
                    if (inLunge == CoilTime - 3) {
                        Vector2 aim = player.Center + player.velocity * 14f;
                        lungeDir = (aim - npc.Center).SafeNormalize(Vector2.UnitY);
                        npc.rotation = lungeDir.ToRotation() + MathHelper.PiOver2;
                        if (!VaultUtils.isClient) {
                            EowLungeOmen.Lock(npc.whoAmI, lungeDir);
                        }
                    }
                    npc.velocity *= 0.55f;
                }

                context.MawGlow = coilT;
                npc.damage = 0;

                if (inLunge == CoilTime - 12) {
                    SoundEngine.PlaySound(SoundID.Zombie13 with { Pitch = -0.45f, Volume = 0.85f, MaxInstances = 3 }, npc.Center);
                }
                return;
            }

            //爆发帧：一帧全速+释放压缩，方向沿用锁向帧(与导引线同源)
            if (inLunge == CoilTime) {
                context.SkipDefaultMovement = true;
                if (lungeDir == Vector2.Zero) {
                    //兜底：状态中途重建(如客户端中途加入)未经历锁向帧
                    Vector2 aim = player.Center + player.velocity * 14f;
                    lungeDir = (aim - npc.Center).SafeNormalize(Vector2.UnitY);
                }
                npc.velocity = lungeDir * LungeSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;

                EowMotionFX.PlayRoar(npc.Center, 0.25f, 0.9f);
                EowMotionFX.SpawnAcidBurst(EowSpitBarrageState.MouthPos(npc), 1.4f, lungeDir * 6f);
                EowMotionFX.CameraPunch(npc.Center, 5f, 11, "EowLunge", lungeDir);
                return;
            }

            //突进段：直线微加速，压缩弹回过冲(1→1.12)
            if (inLunge < CoilTime + TravelTime) {
                context.SkipDefaultMovement = true;
                float travelT = (inLunge - CoilTime) / (float)TravelTime;
                context.Compression = MathHelper.Lerp(0.6f, 1.12f, (float)Math.Sqrt(travelT));
                npc.velocity *= 1.014f;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.damage = npc.defDamage;
                context.MawGlow = 1f - travelT * 0.5f;
                return;
            }

            //硬刹回正：间距回稳，重新接管寻的
            context.SkipDefaultMovement = false;
            float brakeT = (inLunge - CoilTime - TravelTime) / (float)BrakeTime;
            context.Compression = MathHelper.Lerp(1.12f, 1f, brakeT);
            SetMovement(context, player.Center + lungeDir * 500f, 17f, 1.1f);
            context.SlitherStrength = 0.5f;
            npc.damage = npc.defDamage;

            if (inLunge == CoilTime + TravelTime + 1) {
                //刹车酸沫
                EowMotionFX.SpawnAcidBurst(npc.Center, 0.8f, -lungeDir * 3f);
            }
        }

        public override void OnExit(EowStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.damage = context.Npc.defDamage;
            //蜕皮/死亡演出中途打断时清掉未耗尽的导引线(正常结束时已自然消亡，无副作用)
            EowLungeOmen.ClearFor(context.Npc.whoAmI);
        }
    }
}
