using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·钉头锤】钉头锤重铸：铸铁钉锤。签名行为：①满转速命中砸裂护甲，数秒半防
    /// ②破甲一击带低沉铛声 ③击退侧重，铸铁分量压得实
    /// </summary>
    internal class GsMace : GsFlailScheme
    {
        public override int TargetItemID => ItemID.Mace;

        protected override int FlailProjType => ModContent.ProjectileType<GsMaceHead>();

        protected override string GsDescFallback =>
            "Reforged: a fully charged strike cracks the target's armor, halving its defense for a few seconds\nHeavier knockback backs up every blow";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;

        //击退侧重：铸铁分量真实可感
        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.25f;
    }

    /// <summary>
    /// 钉头锤锤头。族默认链体参数（HeadSize 30）；满转命中挂破甲并升级命中音，
    /// 未满转不破甲
    /// </summary>
    internal class GsMaceHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.Mace;
        public override int VanillaProjID => ProjectileID.Mace;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain41;

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //破甲重击：满转掷出的一击砸裂护甲（半防 4 秒），未满转不给
            if (LaunchCharge >= 0.99f && State == StateLaunch) {
                target.AddBuff(BuffID.BrokenArmor, 240);
            }
        }

        protected override void PlayHitSound(NPC target, float charge) {
            base.PlayHitSound(target, charge);
            //破甲一击的升级反馈：低沉铛声
            if (charge < 0.99f || State != StateLaunch) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit42 with { Volume = 0.8f, Pitch = -0.5f }, target.Center);
        }
    }
}
