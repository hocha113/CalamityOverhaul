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
    /// 弦月合拢（四臂对位版）：上对双手各持一道弧光死光沿天体弧线相向合拢，
    /// 逃生楔口随缓动曲线移动（快→慢→快的呼吸）；下对双手反相开弧
    /// 自底部中央向两侧扫离（先封底后让位），与上对合拢形成"下开上合"的对位呼吸；
    /// 头部向楔口滴弹逼走位。上对单手退化为单弧+对侧扫描束，核心裸露版由真眼补顶弧
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
                    //四手处向心星流蓄势
                    MLordPartsStatus parts = context.Parts;
                    for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                        if (parts.HandAlive(slot) && parts.HandIndex(slot) >= 0) {
                            MLordScreenFX.ConvergeStreak(Main.npc[parts.HandIndex(slot)].Center, 260f, Timer / (float)WindupEnd);
                        }
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

        /// <summary>放出合拢弧组：上对相向合拢，下对自底开弧扫离（对位呼吸）</summary>
        private void FireArcs(MLordContext context) {
            MLordPartsStatus parts = context.Parts;
            int damage = ScaleDamage(context, MLordDirector.ArcRayDamage);
            int arcType = ModContent.ProjectileType<MLordArcRayProj>();
            bool upLeft = parts.HandAlive(0) && parts.HandIndex(0) >= 0;
            bool upRight = parts.HandAlive(1) && parts.HandIndex(1) >= 0;

            //上左弧：自左上扫向正下再略过；上右镜像。两弧尖端夹出移动楔口
            if (upLeft) {
                NPC hand = Main.npc[parts.HandIndex(0)];
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, hand.whoAmI, MathHelper.Pi + 0.55f, 2.15f);
            }
            if (upRight) {
                NPC hand = Main.npc[parts.HandIndex(1)];
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, hand.whoAmI, -0.55f, -2.15f);
            }

            //下对开弧：起于底部中央、向本侧扫离，先封底逼跳，后半程让位给上对楔口
            if (parts.HandAlive(2) && parts.HandIndex(2) >= 0) {
                NPC hand = Main.npc[parts.HandIndex(2)];
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, hand.whoAmI, MathHelper.PiOver2 + 0.22f, 1.15f);
            }
            if (parts.HandAlive(3) && parts.HandIndex(3) >= 0) {
                NPC hand = Main.npc[parts.HandIndex(3)];
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, hand.whoAmI, MathHelper.PiOver2 - 0.22f, -1.15f);
            }

            //上对单手：对侧由头补一记扫描束封边
            if (upLeft ^ upRight) {
                NPC origin = context.Parts.Head >= 0 ? Main.npc[context.Parts.Head] : context.Npc;
                float sideAngle = upLeft ? MathHelper.PiOver2 - 0.9f : MathHelper.PiOver2 + 0.9f;
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordScanRayProj>(), ScaleDamage(context, MLordDirector.ScanRayDamage),
                    0f, Main.myPlayer, origin.whoAmI, sideAngle, 46);
            }

            //核心裸露：第一只真眼自上方补顶弧
            if (context.CoreExposed) {
                int[] eyes = new int[MLordFacts.MaxFreeEyes];
                int eyeCount = MLordFacts.ScanFreeEyes(context.Npc, eyes);
                if (eyeCount > 0) {
                    NPC eye = Main.npc[eyes[0]];
                    Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, Vector2.Zero,
                        arcType, damage, 0f, Main.myPlayer, eye.whoAmI, -MathHelper.PiOver2 - 0.8f, 1.6f);
                }
            }

            //无任何手（极端情况）：核心自射对开双弧
            if (!parts.AnyHandAlive && !context.CoreExposed) {
                NPC npc = context.Npc;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, npc.whoAmI, MathHelper.Pi + 0.55f, 2.15f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, npc.whoAmI, -0.55f, -2.15f);
            }
        }
    }
}
