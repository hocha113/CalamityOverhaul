using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 腓力·引导(席位4)：圣光引导。
    /// 周期性为主人祝圣：一段时间内主人的弹幕受圣光牵引，缓缓折向敌人。
    /// 引导状态写在弹幕ai[0](帧数)随弹幕同步，各端转向一致
    /// </summary>
    internal class Philip : BaseDisciple
    {
        public override int Seat => 4;

        /// <summary>引导剩余帧(ai[0]，同步)</summary>
        public ref float GuidanceTime => ref Projectile.ai[0];

        public bool GuidanceActive => GuidanceTime > 0f;

        private const int GuidanceDuration = 240;

        public override void AI() {
            base.AI();
            if (IsMartyring) {
                return;
            }
            if (GuidanceTime > 0f) {
                GuidanceTime--;
                haloFlare = Math.Max(haloFlare, 0.4f);
                //祝圣期间：腓力与主人之间牵一缕光
                if (!Main.dedServ && Main.rand.NextBool(5)) {
                    float t = Main.rand.NextFloat();
                    Vector2 pos = Vector2.Lerp(Projectile.Center, Owner.Center, t);
                    PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, Def.AccentColor, 0.18f)?.Configure(12, 0.65f);
                }
            }
        }

        protected override bool TryCast() {
            //有敌人且引导未在进行时才施放
            if (GuidanceActive) {
                return false;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile)
                    && Vector2.Distance(npc.Center, Owner.Center) < 700f) {
                    return true;
                }
            }
            return false;
        }

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.35f }, Projectile.Center);
            if (Projectile.IsOwnedByLocalPlayer()) {
                GuidanceTime = GuidanceDuration;
                Projectile.netUpdate = true;
            }
        }
    }

    /// <summary>
    /// 圣光引导的全局接线：腓力祝圣期间，主人的伤害性弹幕
    /// 向就近敌人缓缓折转(曲率限幅，各端按同步的引导状态一致执行)
    /// </summary>
    internal class ElysiumGuidanceGlobalProj : GlobalProjectile
    {
        public override void PostAI(Projectile projectile) {
            if (!projectile.friendly || projectile.damage <= 0 || projectile.velocity == Vector2.Zero
                || projectile.owner < 0 || projectile.owner >= Main.maxPlayers
                || projectile.ModProjectile is BaseDisciple) {
                return;
            }
            //手持/召唤/哨兵类弹幕不掰
            if (projectile.aiStyle == 19 || projectile.minion || projectile.sentry || projectile.hide) {
                return;
            }

            Player owner = Main.player[projectile.owner];
            if (!owner.active || !owner.TryGetModPlayer(out ElysiumPlayer ep)
                || !ep.TryGetDisciple(4, out BaseDisciple disciple)
                || disciple is not Philip philip || !philip.GuidanceActive) {
                return;
            }

            //就近索敌，轻微折向
            int target = -1;
            float closest = 420f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, projectile.Center);
                if (dist < closest) {
                    closest = dist;
                    target = i;
                }
            }
            if (target < 0) {
                return;
            }

            Vector2 toTarget = Main.npc[target].Center - projectile.Center;
            float desired = toTarget.ToRotation();
            float current = projectile.velocity.ToRotation();
            float turn = MathHelper.Clamp(MathHelper.WrapAngle(desired - current), -0.024f, 0.024f);
            projectile.velocity = projectile.velocity.RotatedBy(turn);

            if (!Main.dedServ && Main.rand.NextBool(11)) {
                PRTLoader.NewParticle<PRT_Light>(projectile.Center, Vector2.Zero
                    , new Color(255, 240, 190), 0.15f)?.Configure(8, 0.6f);
            }
        }
    }
}
