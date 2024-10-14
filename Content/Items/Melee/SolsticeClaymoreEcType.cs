using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 季节长剑
    /// </summary>
    internal class SolsticeClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "SolsticeClaymore";
        public override void SetDefaults() {
            Item.SetItemCopySD<SolsticeClaymore>();
            Item.SetKnifeHeld<SolsticeClaymoreHeld>();
        }
    }

    internal class EXSolsticeBeam : SolsticeBeam
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "SolsticeBeam";
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.extraUpdates = 3;
            Projectile.Calamity().allProjectilesHome = true;
        }
    }

    internal class SolsticeClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<SolsticeClaymore>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "SolsticeClaymore_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5;
            ShootSpeed = 11;
        }

        public override void Shoot() {
            for (int i = 0; i < 3; i++) {
                Vector2 spanPos = ShootSpanPos + new Vector2(Projectile.spriteDirection * Length, Main.rand.Next(-166, 100));
                Vector2 ver = spanPos.To(InMousePos + UnitToMouseV * 360).UnitVector() * ShootSpeed;
                int type = ModContent.ProjectileType<EXSolsticeBeam>();
                Projectile.NewProjectile(Source, spanPos, ver, type, Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override bool PreInOwnerUpdate() {
            int dustType = Main.dayTime ?
            Utils.SelectRandom(Main.rand, new int[] {
            6,
            259,
            158
            }) :
            Utils.SelectRandom(Main.rand, new int[] {
            173,
            27,
            234
            });
            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType);
                Main.dust[dust].noGravity = true;
            }
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dayTime) {
                target.AddBuff(BuffID.Daybreak, 300);
            }
            else {
                target.AddBuff(ModContent.BuffType<Nightwither>(), 300);
            }

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (!Main.dayTime) {
                target.AddBuff(ModContent.BuffType<Nightwither>(), 300);
            }
        }
    }
}
