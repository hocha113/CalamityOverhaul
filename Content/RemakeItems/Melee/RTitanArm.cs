using CalamityOverhaul.Content.MeleeModify.Core;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTitanArm : CWRItemOverride
    {
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.UseSound = null;
            Item.useTime = 100;
            Item.SetKnifeHeld<TitanArmHeld>();
        }
    }

    internal class TitanArmHeld : BaseKnife
    {
        private List<NPC> onHitNpcs = [];
        public override void SetKnifeProperty() {
            canDrawSlashTrail = false;
            drawTrailCount = 10;
            drawTrailTopWidth = 50;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 66;
        }

        public override bool PreInOwner() {
            if (!DownLeft && !onSound) {
                Projectile.Kill();
            }

            ExecuteAdaptiveSwing(phase0SwingSpeed: -0.1f, phase1SwingSpeed: 5.2f, phase2SwingSpeed: 3f, phase2MeleeSizeIncrement: 0, drawSlash: false);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_AstralInfectionDebuff, 300);
            if (!onHitNpcs.Contains(target)) {
                onHitNpcs.Add(target);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_AstralInfectionDebuff, 300);
        }
    }
}
