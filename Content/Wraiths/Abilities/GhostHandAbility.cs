using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    /// <summary>
    /// 焦黑枯手借力「攥」：背后探出血影鬼手，攥住近旁敌人压制其行动。<br/>
    /// 射程内无可攥之物为空唤，犯戒；鬼手仍会探出、寻不见而缩回
    /// </summary>
    internal sealed class GhostHandAbility : WraithAbility
    {
        /// <summary>索敌半径，与臂展（6×52px）对齐</summary>
        internal const float GrabRange = 300f;

        public override int CooldownTicks => 60 * 45;
        public override float ErosionCost => 0.06f;
        public override float MasteryWear => 0.012f;
        public override float TabooPenalty => 0.05f;

        /// <summary>可攥之敌，Cast/弹体索敌同源；boss 攥不动</summary>
        internal static bool IsValidTarget(NPC npc, Vector2 center)
            => npc.CanBeChasedBy() && !npc.boss
               && Vector2.DistanceSquared(npc.Center, center) < GrabRange * GrabRange;

        public override WraithCastResult Cast(WraithAbilityContext ctx) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (IsValidTarget(npc, ctx.Player.Center)) {
                    return WraithCastResult.Success;
                }
            }
            return WraithCastResult.Taboo;
        }

        public override void ExecuteWorld(Player caster, Vector2 aim, float mastery) {
            int projType = ModContent.ProjectileType<GhostHandProj>();
            if (caster.ownedProjectileCounts[projType] > 0) {
                return;
            }
            Projectile.NewProjectile(caster.GetSource_Misc("CWRWraith_GhostHand"), caster.Center
                , Vector2.Zero, projType, 0, 0f, caster.whoAmI, 0f, 0f, mastery);
        }

        public override void PlayWorldFx(Player caster, Vector2 aim) {
            if (Main.dedServ) {
                return;
            }

            //血色烟雾自背后肩点涌出，鬼手将从这里探出
            Vector2 anchor = caster.Center + new Vector2(-caster.direction * 28f, -8f);
            for (int i = 0; i < 10; i++) {
                Vector2 pos = anchor + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-16f, 10f));
                Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f) - caster.direction * 0.4f
                    , Main.rand.NextFloat(-1.3f, -0.4f));
                Color c = Color.Lerp(new Color(96, 12, 18), new Color(150, 22, 30), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel, c, Main.rand.NextFloat(0.09f, 0.15f))
                    ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(0.4f, 0.65f)
                        , Main.rand.NextFloat(-0.02f, 0.02f));
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.85f, Volume = 0.4f }, caster.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.7f, Volume = 0.3f }, caster.Center);
        }
    }
}
