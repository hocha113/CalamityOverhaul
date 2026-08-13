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
    /// <summary>入场演出：蚀土之兆→破土贯天→高空回身入战</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.Intro, typeof(EowStateContext))]
    internal class EowIntroState : EowStateBase
    {
        public override string StateName => "Intro";
        public override EowStateIndex StateIndex => EowStateIndex.Intro;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int OmenTime = 72;
        private const int BreachFrame = OmenTime + 1;
        private const int ArcEnd = 168;
        private const int IntroEnd = 226;
        private const float BreachSpeed = 43f;
        #endregion

        private Vector2 breachPoint;
        private bool apexRoared;
        private bool rediveFired;

        public EowIntroState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            apexRoared = false;
            rediveFired = false;

            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.alpha = 255;
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Tick();

            //幕一 蚀土之兆
            if (Timer <= OmenTime) {
                UpdateOmen(context);
                return null;
            }

            //破土帧
            if (Timer == BreachFrame) {
                DoBreach(context);
            }

            //幕二 冲天弧线
            if (Timer <= ArcEnd) {
                UpdateSkyArc(context);
                return null;
            }

            //幕三 回身入战
            if (Timer < IntroEnd) {
                UpdateReturnDive(context);
                return null;
            }

            return new EowWeaveState();
        }

        #region 幕一 蚀土之兆
        private void UpdateOmen(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.velocity = Vector2.Zero;
            npc.damage = 0;
            npc.dontTakeDamage = true;

            if (Timer == 1) {
                //头蛰伏在玩家侧下方地底
                breachPoint = EowMotionFX.FindGroundBelow(player.Center + new Vector2(player.velocity.X * 8f, 0f));
                npc.Center = breachPoint + new Vector2(0f, 1500f);
                npc.netUpdate = true;
                //深土闷吼
                EowMotionFX.PlayRoar(player.Center, -0.85f, 0.6f);
            }

            //预兆盘(服务端一次)
            if (Timer == 3 && !VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint, Vector2.Zero,
                    ModContent.ProjectileType<EowBreachOmen>(), 0, 0f, Main.myPlayer, OmenTime - 4, 0f);
            }

            //地表隆隆爬升
            float t = Timer / (float)OmenTime;
            float ramp = t * t * t;
            if (!VaultUtils.isServer) {
                if (Timer % 12 == 0) {
                    EowMotionFX.CameraPunch(breachPoint, 1f + ramp * 4f, 14, "EowIntroRumble");
                }
                if (Timer % 13 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with {
                        Volume = 0.45f + ramp * 0.55f,
                        Pitch = -0.6f + ramp * 0.5f,
                        MaxInstances = 3
                    }, breachPoint);
                }
                //末段静默一拍：粒子由预兆盘负责，收声蓄势
                if (t > 0.85f && Timer % 3 == 0) {
                    Lighting.AddLight(breachPoint, EowMotionFX.AcidGreen.ToVector3() * ramp);
                }
            }
        }
        #endregion

        #region 破土
        private void DoBreach(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //自地底垂直贯出，微偏向玩家
            int side = Math.Sign(player.Center.X - breachPoint.X);
            if (side == 0) {
                side = 1;
            }
            npc.Center = breachPoint + new Vector2(0f, 620f);
            npc.velocity = (-Vector2.UnitY).RotatedBy(side * 0.16f) * BreachSpeed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            npc.alpha = 0;
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            npc.netUpdate = true;

            //生成体节链+统一血池(服务端)
            if (!VaultUtils.isClient) {
                EowHeadAI.SpawnBodySegments(npc, context.IsDeathMode);
            }

            EowMotionFX.SpawnBreachBlast(breachPoint, 1.9f, -Vector2.UnitY);
            EowMotionFX.CameraPunch(breachPoint, 10f, 20, "EowIntroBreach", -Vector2.UnitY);
            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.15f, Volume = 1.2f }, player.Center);
        }
        #endregion

        #region 幕二 冲天弧线
        private void UpdateSkyArc(EowStateContext context) {
            NPC npc = context.Npc;

            npc.damage = npc.defDamage;
            context.MawGlow = 0.6f;

            //重力弧线：升力耗尽后拱身越顶
            npc.velocity.Y += 0.42f;
            npc.velocity.X *= 0.996f;
            if (npc.velocity.Length() > BreachSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(-Vector2.UnitY) * BreachSpeed;
            }
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //顶点嘶吼+酸沫飞散
            if (!apexRoared && npc.velocity.Y > -3f) {
                apexRoared = true;
                EowMotionFX.PlayRoar(npc.Center, 0.15f, 1.05f);
                EowMotionFX.SpawnAcidBurst(npc.Center, 1.6f, -Vector2.UnitY * 2f);
                EowMotionFX.CameraPunch(npc.Center, 4.5f, 12, "EowIntroApex");
            }

            //身上洒落酸滴(纯表现)
            if (!VaultUtils.isServer && Timer % 4 == 0 && context.Segments.Count > 8) {
                NPC seg = context.Segments[Main.rand.Next(context.Segments.Count / 2)];
                if (seg.Alives() && EowMotionFX.OnScreen(seg.Center)) {
                    EowMotionFX.SpawnSegmentSpeedSpray(seg, 0.9f);
                }
            }
        }
        #endregion

        #region 幕三 回身入战
        private void UpdateReturnDive(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = npc.defDamage;
            context.SkipDefaultMovement = false;
            context.SlitherStrength = 0.8f;
            context.AccelRate = 0.09f;

            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            SetMovement(context, player.Center + new Vector2(side * 520f, -320f), 24f, 1.3f);

            //穿地小尘爆(一次)
            if (!rediveFired && npc.Center.Y > breachPoint.Y - 40f && npc.velocity.Y > 0f) {
                rediveFired = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, breachPoint.Y), 1.1f);
            }
        }
        #endregion

        public override void OnExit(EowStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.AccelRate = 0.07f;
            context.Npc.damage = context.Npc.defDamage;
            context.Npc.dontTakeDamage = false;
        }
    }
}
