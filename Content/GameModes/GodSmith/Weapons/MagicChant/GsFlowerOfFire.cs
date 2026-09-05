using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 烈火之花重铸：燃拍。正拍火球首次落地时炸开三瓣扇形火舌（各 0.5 倍，
    /// 共鸣 4 层起多一瓣）；层数每 2 层给火球多一次弹跳；满层强化「盛焰花田」：
    /// 该发火球在首次落地或命中处原地化开 2.5s 火田（0.3 倍每 0.25s 一跳）。
    /// 材质身份：燃焰
    /// </summary>
    internal class GsFlowerOfFire : GsChantScheme
    {
        public override int TargetItemID => ItemID.FlowerofFire;

        protected override string GsDescFallback =>
            "Reforged: on-beat fireballs burst into fans of flame tongues on first landing;\nat full resonance the next fireball blooms into a burning field where it lands";
        protected override float BaseDamageMult => 1.08f;

        /// <summary>形态：扇形火舌</summary>
        private const float FormTongue = 10f;

        /// <summary>落地检测状态（端本地；生成裁决只认 owner 端）</summary>
        private class BounceState
        {
            public float PrevVelY;
            public bool Primed;
            public bool Spent;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            //层数每 2 层多一次弹跳（火球的反弹次数走 penetrate 计数）
            if (router.MarkData is FormOnBeat or FormEmpower && proj.penetrate > 0) {
                proj.penetrate += (int)(router.MarkData2 / 2f);
            }
            if (router.MarkData == FormTongue) {
                proj.timeLeft = Math.Min(proj.timeLeft, 34);
                proj.scale *= 0.7f;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //首次落地检测：竖直速度由下坠翻转为上抛即反弹
            if (router.MarkData is FormOnBeat or FormEmpower) {
                BounceState state = router.GetOrCreateState<BounceState>();
                if (state.Primed && !state.Spent && state.PrevVelY > 0f && proj.velocity.Y < 0f) {
                    state.Spent = true;
                    OnFirstLanding(proj, router);
                }
                state.PrevVelY = proj.velocity.Y;
                state.Primed = true;
            }
            //火舌形态：重力下坠成舌弧
            if (router.MarkData == FormTongue) {
                proj.velocity.Y += 0.2f;
            }
        }

        /// <summary>首次落地：正拍炸火舌，强化咏唱化火田（owner 裁决，弹幕过线全端可见）</summary>
        private void OnFirstLanding(Projectile proj, GodSmithProjRouter router) {
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            if (router.MarkData == FormEmpower) {
                //盛焰花田：火球化开为火田，本体退场
                Projectile.NewProjectile(proj.GetSource_FromThis(),
                    proj.Center, Vector2.Zero, ModContent.ProjectileType<GsChantFlameFieldProj>(),
                    Math.Max(1, (int)(proj.damage * 0.3f)), 1f, proj.owner);
                proj.Kill();
                return;
            }
            //燃拍火舌：三瓣扇形上抛，4 层起多一瓣
            int tongues = router.MarkData2 >= 4f ? 4 : 3;
            int tongueDamage = Math.Max(1, (int)(proj.damage * 0.5f));
            for (int i = 0; i < tongues; i++) {
                float ang = -MathHelper.PiOver2 + MathHelper.ToRadians(-20f + 40f * i / (tongues - 1));
                Vector2 vel = ang.ToRotationVector2() * 6f;
                QueueForm(Main.player[proj.owner], FormTongue);
                Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, vel,
                    proj.type, tongueDamage, proj.knockBack * 0.4f, proj.owner);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //强化火球命中敌人也直接开田（不必等落地）
            if (proj.IsOwnedByLocalPlayer() && router.MarkData == FormEmpower) {
                Projectile.NewProjectile(proj.GetSource_FromThis(),
                    target.Center, Vector2.Zero, ModContent.ProjectileType<GsChantFlameFieldProj>(),
                    Math.Max(1, (int)(proj.damage * 0.3f)), 1f, proj.owner);
                proj.Kill();
            }
        }
    }
}
