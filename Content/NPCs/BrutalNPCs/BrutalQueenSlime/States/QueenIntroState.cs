using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>入场演出：天穹碎晶预兆→光柱降临→触地绽晶→屈膝礼</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.Intro, typeof(QueenSlimeStateContext))]
    internal class QueenIntroState : QueenSlimeStateBase
    {
        public override string StateName => "Intro";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.Intro;

        #region 节奏常量
        private const int OmenTime = 55;          //碎晶预兆
        private const int DescendTime = 96;       //光柱内降临
        private const int LandFrame = OmenTime + DescendTime;   //151 触地帧
        private const int BowStart = LandFrame + 18;
        private const int BowEnd = BowStart + 46;
        private const int IntroEnd = BowEnd + 30;               //~245
        private const float DescendSpeed = 3.4f;
        #endregion

        private Vector2 groundPoint;

        public QueenIntroState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.alpha = 255;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.velocity = Vector2.Zero;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //幕一 天穹预兆：三粒碎晶坠落绽裂
            if (Timer <= OmenTime) {
                UpdateOmen(context, player);
                return null;
            }

            //幕二 光柱降临
            if (Timer <= LandFrame) {
                UpdateDescend(context);
                return null;
            }

            //触地帧
            if (Timer == LandFrame + 1) {
                DoLandingBeat(context);
            }

            //幕三 屈膝礼与王冠辉光
            if (Timer < IntroEnd) {
                UpdateBow(context);
                return null;
            }

            return new QueenBallroomStepState(3);
        }

        private void UpdateOmen(QueenSlimeStateContext context, Player player) {
            NPC npc = context.Npc;
            npc.velocity = Vector2.Zero;

            if (Timer == 1) {
                //定落点并把皇后挂到高空待命
                groundPoint = QueenMotion.FindGroundBelow(player.Center + new Vector2(0f, -40f));
                npc.Bottom = groundPoint - new Vector2(0f, DescendTime * DescendSpeed);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.4f }, player.Center);
            }

            //三粒预兆碎晶，错帧坠落在落点两侧绽裂
            if (!VaultUtils.isServer && (Timer == 10 || Timer == 24 || Timer == 38)) {
                int i = Timer / 14;
                Vector2 hit = groundPoint + new Vector2((i - 1) * 150f, 0f);
                QueenMotion.CrystalShatterBurst(hit - new Vector2(0f, 8f), 0.6f, i * 0.3f);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.5f - i * 0.15f }, hit);
            }

            //落点上空微光聚拢
            if (!VaultUtils.isServer && Timer > 14) {
                float p = Timer / (float)OmenTime;
                QueenMotion.ChargeGatherFX(groundPoint - new Vector2(0f, 120f), p, 200f, p);
                Lighting.AddLight(groundPoint - new Vector2(0f, 100f), QueenMotion.HolyGold.ToVector3() * p * 0.7f);
            }
        }

        private void UpdateDescend(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            float t = (Timer - OmenTime) / (float)DescendTime;

            //匀速垂降，末端缓刹
            float speed = DescendSpeed * (t > 0.86f ? MathHelper.Lerp(1f, 0.35f, (t - 0.86f) / 0.14f) : 1f);
            npc.velocity = new Vector2(0f, speed);
            npc.alpha = (int)MathHelper.Clamp(255f * (1f - t * 2.4f), 0f, 255f);
            context.PoseCommand = 2;
            context.PrismShimmer = 0.6f * t;

            if (Timer == OmenTime + 8) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
            }

            if (VaultUtils.isServer) {
                return;
            }

            //光柱：自天而下的竖直光尘走廊
            for (int i = 0; i < 2; i++) {
                Vector2 dustPos = new Vector2(groundPoint.X + Main.rand.NextFloat(-46f, 46f),
                    npc.Center.Y + Main.rand.NextFloat(-260f, 60f));
                Dust d = Dust.NewDustPerfect(dustPos, DustID.TintableDust,
                    new Vector2(0f, Main.rand.NextFloat(1f, 3f)), 140, QueenMotion.GetQueenDustColor(), 1.3f);
                d.noGravity = true;
            }
            if (Timer % 9 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(npc.Center + Main.rand.NextVector2Circular(60f, 80f),
                    new Vector2(0f, 1.6f), Color.White, 0.9f)?
                    .Configure(QueenMotion.PrismHue(t), 22, 0.04f, 1.5f);
            }
            Lighting.AddLight(npc.Center, QueenMotion.HolyGold.ToVector3() * 1.1f);
        }

        private void DoLandingBeat(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity = Vector2.Zero;
            npc.alpha = 0;
            npc.noGravity = false;
            npc.noTileCollide = false;
            if (!VaultUtils.isClient) {
                npc.Bottom = groundPoint;
                npc.netUpdate = true;
            }
            context.PushSquash(-0.55f);

            QueenMotion.LandingRingFX(npc.Bottom, 1.4f, 0.1f);
            QueenMotion.CrystalShatterBurst(npc.Center, 1.1f, 0.55f, playSound: false);
            QueenMotion.Shake(npc.Center, 7f, 16, "QueenIntroLand");
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.9f, Pitch = 0.25f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.75f, Pitch = 0.3f }, npc.Center);
        }

        private void UpdateBow(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X = 0f;

            //屈膝礼：压身→王冠辉光→起身
            if (Timer >= BowStart && Timer < BowEnd) {
                context.PoseCommand = 3;
                float bowT = (Timer - BowStart) / (float)(BowEnd - BowStart);
                context.SetChargeState(1, QueenMotion.Bump(bowT));
                if (!VaultUtils.isServer && Timer % 5 == 0) {
                    QueenMotion.ChargeGatherFX(npc.Top - new Vector2(0f, 20f), bowT, 90f, bowT);
                }
                if (Timer == BowStart + 20) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1f, Pitch = 0.6f }, npc.Center);
                }
            }
            else if (Timer >= BowEnd) {
                context.ResetChargeState();
                context.PrismShimmer = 0.4f;
                //起身放行伤害
                if (Timer == BowEnd + 1) {
                    npc.dontTakeDamage = false;
                    context.PushSquash(0.3f);
                }
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = false;
            npc.alpha = 0;
            npc.noGravity = false;
            npc.noTileCollide = false;
        }
    }
}
