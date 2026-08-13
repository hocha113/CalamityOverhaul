using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 引力坍缩：核心升空开井，引力井牵引玩家并透镜扭曲空间，
    /// 四手按对角序轮换向井投掷星球、被弹射成椭圆弹道；井崩解放出环形幻影眼
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.GravityCollapse, typeof(MLordContext))]
    internal class MLordGravityCollapseState : MLordStateBase
    {
        public override string StateName => "GravityCollapse";
        public override MLordStateIndex StateIndex => MLordStateIndex.GravityCollapse;

        internal const int WindupEnd = 60;
        internal const int WellLife = MLordGravityWellProj.TotalLife;

        private int stateLength;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            stateLength = WindupEnd + WellLife + Frames(context, 40);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie97 with { Volume = 0.9f, Pitch = -0.5f }, context.Npc.Center);
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //核心升到高位，胸腔缝隙漏光
            HoverTo(npc, target.Center + new Vector2(0f, -520f), 6.5f, 0.05f);
            UpdateLean(context);
            context.HeartExposure = MathHelper.Max(context.HeartExposure, MathHelper.Clamp(Timer / (float)WindupEnd, 0f, 1f) * 0.5f);

            if (Timer < WindupEnd) {
                context.SetChargeState(Timer / (float)WindupEnd);
                if (!VaultUtils.isServer) {
                    MLordScreenFX.ConvergeStreak(npc.Center, 420f, Timer / (float)WindupEnd);
                    MLordScreenEffects.PushGravityDim(npc.Center, Timer / (float)WindupEnd * 0.3f);
                }
            }

            if (!VaultUtils.isClient) {
                RunServer(context);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        private void RunServer(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //开井：锚点取核心与玩家之间
            if (Timer == WindupEnd) {
                Vector2 anchor = npc.Center + (target.Center - npc.Center) * 0.62f;
                context.Owner.ai[MLordAiSlots.OvAnchorX] = anchor.X;
                context.Owner.ai[MLordAiSlots.OvAnchorY] = anchor.Y;
                npc.netUpdate = true;
                Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                    ModContent.ProjectileType<MLordGravityWellProj>(), 0, 0f, Main.myPlayer,
                    ScaleDamage(context, MLordDirector.EyeDamage));
            }

            //四波切向星球投入井轨
            int volleyInterval = Frames(context, 46);
            for (int wave = 0; wave < 4; wave++) {
                if (Timer != WindupEnd + 24 + wave * volleyInterval) {
                    continue;
                }
                Vector2 well = new(context.Owner.ai[MLordAiSlots.OvAnchorX], context.Owner.ai[MLordAiSlots.OvAnchorY]);
                SpawnOrbitVolley(context, well, wave);
            }
        }

        /// <summary>自存活手向井掷出切向星球：四波按对角序轮换投手（上左→下右→上右→下左），缺手就近补位、全缺退核心</summary>
        private void SpawnOrbitVolley(MLordContext context, Vector2 well, int wave) {
            MLordPartsStatus parts = context.Parts;
            int damage = ScaleDamage(context, MLordDirector.OrbDamage);
            int orbType = ModContent.ProjectileType<MLordOrbProj>();
            int count = context.CoreExposed ? 4 : 3;

            Span<int> throwOrder = stackalloc int[] { 0, 3, 1, 2 };
            int handIndex = parts.FirstAliveHand(throwOrder[wave % throwOrder.Length]);
            NPC origin = handIndex >= 0 ? Main.npc[handIndex] : context.Npc;

            Vector2 toWell = (well - origin.Center).SafeNormalize(Vector2.UnitY);
            Vector2 tangent = toWell.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < count; i++) {
                float mix = MathHelper.Lerp(-0.4f, 0.4f, count <= 1 ? 0.5f : i / (float)(count - 1));
                Vector2 vel = (toWell * 6.5f + tangent * (8f + mix * 5f)).RotatedBy(mix * 0.2f);
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center, vel,
                    orbType, damage, 0f, Main.myPlayer, origin.whoAmI, 1f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 4 }, origin.Center);
            }
        }
    }
}
