using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>诅咒黑暗领域：视界压缩，诅咒烛环收拢，头颅在黑暗中游猎瞬影</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.CurseDomain, typeof(SkeletronStateContext))]
    internal class SkeletronCurseDomainState : SkeletronStateBase
    {
        public override string StateName => "CurseDomain";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.CurseDomain;

        internal const int RingSpawn = 34;
        internal const int RingLife = 300;      //与 SkeletronCurseWisp 寿命对齐
        internal const int Duration = 388;

        private Vector2 anchor;
        private bool anchorSet;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            anchorSet = false;
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            //视界压缩
            float domain = MathHelper.Clamp(Timer / 46f, 0f, 0.88f);
            if (Timer > RingSpawn + RingLife) {
                domain = MathHelper.Lerp(0.88f, 0f, (Timer - RingSpawn - RingLife) / (float)(Duration - RingSpawn - RingLife));
            }
            SkeletronScreenEffects.RequestDomain(domain);

            if (Timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.9f, Pitch = -0.8f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.7f, Pitch = -0.9f }, npc.Center);
            }

            //布环（服务端）
            if (Timer == RingSpawn) {
                anchor = context.Target.Center;
                anchorSet = true;
                if (!VaultUtils.isClient) {
                    int damage = SkullDamage(context);
                    int slots = 13;
                    float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < slots; i++) {
                        //三个缺口给穿行留路
                        if (i == 3 || i == 7 || i == 11) {
                            continue;
                        }
                        float angle = baseAngle + MathHelper.TwoPi * i / slots;
                        Vector2 pos = anchor + angle.ToRotationVector2() * SkeletronCurseWisp.StartRadius;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<SkeletronCurseWisp>(), damage, 0f, Main.myPlayer,
                            angle, anchor.X, anchor.Y);
                    }
                    npc.netUpdate = true;
                }
            }

            UpdateStalk(context, npc);

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                npc.alpha = 0;
                return new SkeletronHubState();
            }
            return null;
        }

        /// <summary>黑暗中游猎：三次瞬影换位，每次凝形补一记三连颅火</summary>
        private void UpdateStalk(SkeletronStateContext context, NPC npc) {
            if (Timer < RingSpawn) {
                //先撤到高位
                Vector2 rise = context.Target.Center + new Vector2(0f, -420f);
                npc.velocity = (rise - npc.Center) * 0.04f;
                SettleRotation(npc, 0.2f);
                return;
            }

            int stalkPhase = (Timer - RingSpawn) % 92;

            if (stalkPhase < 14) {
                //散形
                npc.damage = 0;
                npc.velocity *= 0.85f;
                npc.alpha = (int)MathHelper.Lerp(0f, 255f, stalkPhase / 14f);
                context.EyeFlame = 1f - stalkPhase / 14f;
                if (!VaultUtils.isServer && stalkPhase % 3 == 0) {
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(36f, 36f),
                        Main.rand.NextVector2CircularEdge(3f, 3f),
                        SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1.3f, 2f))?.Configure(Main.rand.Next(18, 30));
                }
                if (stalkPhase == 13 && !VaultUtils.isClient && anchorSet) {
                    //瞬移到环缘随机方位
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    npc.Center = anchor + a.ToRotationVector2() * 470f;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
            }
            else if (stalkPhase < 26) {
                //凝形
                npc.damage = 0;
                float t = (stalkPhase - 14) / 12f;
                npc.alpha = (int)MathHelper.Lerp(255f, 0f, t);
                context.EyeFlame = t * 1.4f;
            }
            else if (stalkPhase == 26) {
                //三连颅火
                npc.alpha = 0;
                npc.damage = npc.defDamage;
                if (!VaultUtils.isClient
                    && Collision.CanHitLine(npc.Center, 1, 1, context.Target.position, context.Target.width, context.Target.height)) {
                    int damage = SkullDamage(context);
                    for (int i = -1; i <= 1; i++) {
                        Vector2 vel = DirectionToTarget(context).RotatedBy(i * 0.2f) * 7.2f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel * 5f, vel,
                            ModContent.ProjectileType<SkeletronCursedSkull>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    }
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.5f }, npc.Center);
                }
            }
            else {
                //缓压逼近
                npc.damage = npc.defDamage;
                Vector2 toward = (context.Target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                npc.velocity = Vector2.Lerp(npc.velocity, toward * 3.4f, 0.05f);
                LeanByVelocity(npc);
                context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 1.1f, 0.06f);
            }
        }

        public override void OnExit(SkeletronStateContext context) {
            base.OnExit(context);
            context.Npc.alpha = 0;
        }
    }
}
