using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 千手：终结定格期自玩家背后浮出的一只持刀鬼手。<br/>
    /// 六只错拍挥出，各自朝不同角度落一记断斩——终结那一停里同框多刀。<br/>
    /// 手本身不结算伤害，斩由 <see cref="CrimsonRendCleave"/> 承担，
    /// 所以它不会偷偷多打，看到几刀就是几刀。<br/>
    /// ai[0]=绕身角度 ai[1]=基础武器伤害 ai[2]=起手延迟(帧)
    /// </summary>
    internal class OniMeiSenjuArm : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>浮现帧数</summary>
        private const int RiseFrames = 8;
        /// <summary>抬手蓄势帧数</summary>
        private const int WindupFrames = 9;
        /// <summary>挥出到收回的帧数</summary>
        private const int SwingFrames = 12;
        private const int FadeFrames = 8;
        /// <summary>手浮在离身多远</summary>
        private const float ArmRadius = 66f;
        /// <summary>挥砍的角幅</summary>
        private const float SwingSpan = 2.0f;

        private static readonly Color ArmInk = new(26, 12, 18);
        private static readonly Color ArmRim = new(196, 42, 36);

        private int timer;
        private bool swung;

        private float Angle => Projectile.ai[0];
        private int BaseWeaponDamage => Math.Max(1, (int)Projectile.ai[1]);
        private int Delay => (int)Projectile.ai[2];
        private Player Owner => Main.player[Projectile.owner];
        private int Local => timer - Delay;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端一次浮出全部六手，错拍挥出</summary>
        internal static void FireVolley(Player player, int baseWeaponDamage, IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return;
            }
            int count = OniMeiCombat.SenjuArmCount;
            for (int i = 0; i < count; i++) {
                //上半周散开，读作"自背后长出来"而不是围成一圈
                float angle = MathHelper.Lerp(-MathHelper.Pi * 0.92f, -MathHelper.Pi * 0.08f,
                    count <= 1 ? 0.5f : i / (count - 1f));
                Projectile.NewProjectile(
                    source ?? player.GetSource_Misc("CWR_OniMeiSenjuArm"),
                    player.Center, Vector2.Zero, ModContent.ProjectileType<OniMeiSenjuArm>(),
                    0, 0f, player.whoAmI,
                    ai0: angle, ai1: Math.Max(1, baseWeaponDamage), ai2: i * 5);
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            timer++;
            Projectile.Center = Owner.Center + Angle.ToRotationVector2() * ArmRadius;

            int local = Local;
            if (local < 0) {
                return;
            }
            if (local == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.20f, Volume = 0.24f },
                    Projectile.Center);
            }
            if (local == RiseFrames + WindupFrames && !swung) {
                swung = true;
                Swing();
            }
            if (local >= RiseFrames + WindupFrames + SwingFrames + FadeFrames) {
                Projectile.Kill();
                return;
            }
            if (!Main.dedServ && local < RiseFrames) {
                //浮现：墨自身后涌起聚成手，不是贴图淡入
                PRTLoader.NewParticle<PRT_OniInkDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 16f),
                    (Projectile.Center - Owner.Center).SafeNormalize(Vector2.UnitX) * 0.8f,
                    ArmInk, Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(Main.rand.Next(12, 20));
            }
        }

        private void Swing() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int damage = Math.Max(1, (int)(BaseWeaponDamage * OniMeiCombat.SenjuArmDamageMul));
                //每只手朝自己所在的方向落刀：六个角度覆盖，不是同一刀画六遍
                float aim = Angle;
                CrimsonRendCleave.Fire(Owner, Projectile.Center + aim.ToRotationVector2() * 40f,
                    aim, damage, 3f, scale: 0.78f,
                    flip: Projectile.identity % 2 == 0 ? 1 : -1,
                    Projectile.GetSource_FromAI(), CleaveStyle.Plain);
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.KatanaSwing with {
                Pitch = 0.35f + Projectile.identity % 5 * 0.06f,
                Volume = 0.34f,
                MaxInstances = 4,
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            int local = Local;
            if (Main.dedServ || local < 0) {
                return false;
            }
            float rise = MathHelper.Clamp(local / (float)RiseFrames, 0f, 1f);
            int swingLocal = local - RiseFrames - WindupFrames;
            float fade = 1f;
            if (swingLocal > SwingFrames) {
                fade = MathHelper.Clamp(1f - (swingLocal - SwingFrames) / (float)FadeFrames, 0f, 1f);
            }
            float alpha = rise * fade;
            if (alpha <= 0.01f) {
                return false;
            }

            //挥砍角：蓄势时向后拉，挥出时扫过 SwingSpan
            float swingT = swingLocal <= 0
                ? MathHelper.Clamp((local - RiseFrames) / (float)WindupFrames, 0f, 1f) * -0.35f
                : MathHelper.Clamp(swingLocal / (float)SwingFrames, 0f, 1f);
            float bladeAngle = Angle + MathHelper.Lerp(-SwingSpan * 0.5f, SwingSpan * 0.5f,
                MathHelper.Clamp(swingT, 0f, 1f)) + (swingT < 0f ? swingT : 0f);

            Vector2 shoulder = Owner.Center - Main.screenPosition;
            Vector2 hand = Projectile.Center - Main.screenPosition;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);

            //臂：自身后伸出的一条墨色瘦长肢，末端略收
            Vector2 limb = hand - shoulder;
            float limbLen = limb.Length();
            if (limbLen > 2f) {
                Main.EntitySpriteDraw(pixel, shoulder, src, ArmInk * (alpha * 0.92f),
                    limb.ToRotation(), new Vector2(0f, 0.5f),
                    new Vector2(limbLen, 8.5f), SpriteEffects.None);
                Main.EntitySpriteDraw(pixel, shoulder, src, ArmRim * (alpha * 0.35f),
                    limb.ToRotation(), new Vector2(0f, 0.5f),
                    new Vector2(limbLen, 3.2f), SpriteEffects.None);
            }
            //腕：一小块实心，给刀一个抓手
            Main.EntitySpriteDraw(pixel, hand, src, ArmInk * (alpha * 0.95f),
                bladeAngle, new Vector2(0.5f), new Vector2(11f), SpriteEffects.None);

            //刀：与本体同一套支点数学，尺寸压小，读作"分身的刀"
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 size = blade.Size();
            Vector2 origin = size * OniBladePose.HiltUV;
            Vector2 tip = size * OniBladePose.TipUV;
            float spriteAngle = (tip - origin).ToRotation();
            Main.EntitySpriteDraw(blade, hand, null, Color.White * (alpha * 0.88f),
                bladeAngle - spriteAngle, origin, 0.72f, SpriteEffects.None);
            return false;
        }
    }
}
