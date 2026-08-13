using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 弦月合拢：双手各持一道弧光死光，沿缓动天体弧线相向合拢，
    /// 逃生楔口随缓动曲线移动（快→慢→快的呼吸）；头部向楔口滴弹逼走位。
    /// 单手存活退化为单弧+对侧扫描束，核心裸露版由真眼补第三弧
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.CrescentClose, typeof(MLordContext))]
    internal class MLordCrescentCloseState : MLordStateBase
    {
        public override string StateName => "CrescentClose";
        public override MLordStateIndex StateIndex => MLordStateIndex.CrescentClose;

        internal const int WindupEnd = 70;
        internal const int RayLife = MLordArcRayProj.TotalLife;

        private int stateLength;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            stateLength = WindupEnd + RayLife + Frames(context, 42);
            //裸露期真眼锚定站桩，第三弧才不会被环绕运动甩成乱扫
            if (!VaultUtils.isClient && context.CoreExposed) {
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Anchor;
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie93 with { Volume = 0.85f, Pitch = -0.4f }, context.Npc.Center);
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Solo;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //核心稳住高位做支点
            HoverTo(npc, target.Center + new Vector2(0f, -150f), 5f, 0.04f);
            npc.velocity *= 0.95f;
            UpdateLean(context);

            if (Timer < WindupEnd) {
                context.SetChargeState(Timer / (float)WindupEnd);
                if (!VaultUtils.isServer) {
                    //双手处向心星流蓄势
                    MLordPartsStatus parts = context.Parts;
                    if (parts.LeftHandAlive && parts.LeftHand >= 0) {
                        MLordScreenFX.ConvergeStreak(Main.npc[parts.LeftHand].Center, 260f, Timer / (float)WindupEnd);
                    }
                    if (parts.RightHandAlive && parts.RightHand >= 0) {
                        MLordScreenFX.ConvergeStreak(Main.npc[parts.RightHand].Center, 260f, Timer / (float)WindupEnd);
                    }
                }
            }

            if (Timer == WindupEnd && !VaultUtils.isClient) {
                FireArcs(context);
            }

            //弧光存续期头部向楔口滴弹，禁止在缺口里蹲桩
            if (!VaultUtils.isClient && Timer > WindupEnd && Timer < WindupEnd + RayLife
                && (Timer - WindupEnd) % Frames(context, 40) == 12) {
                NPC origin = context.Parts.Head >= 0 ? Main.npc[context.Parts.Head] : npc;
                Vector2 aim = (target.Center - origin.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center + aim * 40f, aim * 6.2f,
                    ProjectileID.PhantasmalBolt, ScaleDamage(context, MLordDirector.BoltDamage), 0f, Main.myPlayer);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>放出合拢弧组</summary>
        private void FireArcs(MLordContext context) {
            MLordPartsStatus parts = context.Parts;
            int damage = ScaleDamage(context, MLordDirector.ArcRayDamage);
            int arcType = ModContent.ProjectileType<MLordArcRayProj>();
            bool left = parts.LeftHandAlive && parts.LeftHand >= 0;
            bool right = parts.RightHandAlive && parts.RightHand >= 0;

            //左弧：自左上扫向正下再略过；右弧镜像。两弧尖端夹出移动楔口
            if (left) {
                NPC hand = Main.npc[parts.LeftHand];
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, hand.whoAmI, MathHelper.Pi + 0.55f, 2.15f);
            }
            if (right) {
                NPC hand = Main.npc[parts.RightHand];
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, hand.whoAmI, -0.55f, -2.15f);
            }

            //单手：对侧由头补一记扫描束封边
            if (left ^ right) {
                NPC origin = context.Parts.Head >= 0 ? Main.npc[context.Parts.Head] : context.Npc;
                float sideAngle = left ? MathHelper.PiOver2 - 0.9f : MathHelper.PiOver2 + 0.9f;
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordScanRayProj>(), ScaleDamage(context, MLordDirector.ScanRayDamage),
                    0f, Main.myPlayer, origin.whoAmI, sideAngle, 46);
            }

            //核心裸露：第一只真眼自上方补第三段短弧
            if (context.CoreExposed) {
                int[] eyes = new int[3];
                int eyeCount = MLordFacts.ScanFreeEyes(context.Npc, eyes);
                if (eyeCount > 0) {
                    NPC eye = Main.npc[eyes[0]];
                    Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, Vector2.Zero,
                        arcType, damage, 0f, Main.myPlayer, eye.whoAmI, -MathHelper.PiOver2 - 0.8f, 1.6f);
                }
            }

            //无任何执行者（极端情况）：核心自射对开双弧
            if (!left && !right && !context.CoreExposed) {
                NPC npc = context.Npc;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, npc.whoAmI, MathHelper.Pi + 0.55f, 2.15f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, npc.whoAmI, -0.55f, -2.15f);
            }
        }
    }
}
