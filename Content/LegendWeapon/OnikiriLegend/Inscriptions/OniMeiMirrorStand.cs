using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 鏡樋「镜写」：疾走终点立一枚纸镜。<br/>
    /// 你之后的第一记刀命中时，立像同步复刻同一记斩击朝它自己的正面挥出，然后碎。<br/>
    /// 复刻是"再来一刀"，落点由立像所在决定——所以疾走停在哪、面朝哪，是有讲究的。<br/>
    /// ai[0]=立像朝向(±1) ai[1]=基础武器伤害
    /// </summary>
    internal class OniMeiMirrorStand : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RiseFrames = 8;
        private const int ShatterFrames = 16;
        private const float StandHalfWidth = 22f;
        private const float StandHalfHeight = 34f;

        private static readonly Color GlassBody = new(206, 208, 214);
        private static readonly Color GlassRim = new(255, 240, 226);

        private int timer;
        private bool shattered;
        private int shatterTimer;
        private float swayPhase;

        private int Facing => Projectile.ai[0] >= 0f ? 1 : -1;
        private int BaseWeaponDamage => Math.Max(1, (int)Projectile.ai[1]);
        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = (int)(StandHalfWidth * 2f);
            Projectile.height = (int)(StandHalfHeight * 2f);
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = OniMeiCombat.MirrorStandLifeTicks;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端立像；同时只留一枚，新的顶掉旧的</summary>
        internal static bool TryPlace(Player player, Vector2 at, int facing, int baseWeaponDamage,
            IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return false;
            }
            int type = ModContent.ProjectileType<OniMeiMirrorStand>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    proj.Kill();
                }
            }
            Projectile spawned = Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiMirrorStand"), at, Vector2.Zero,
                type, 0, 0f, player.whoAmI,
                ai0: facing >= 0 ? 1f : -1f, ai1: Math.Max(1, baseWeaponDamage));
            return spawned.active;
        }

        /// <summary>
        /// 你的刀落下了，镜子跟着落一刀。<br/>
        /// 只认在场那一枚，复刻完即碎；返回是否真的复刻了
        /// </summary>
        internal static bool TryEcho(Player player, float aim, float knockback, float sizeMul) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return false;
            }
            int type = ModContent.ProjectileType<OniMeiMirrorStand>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != type
                    || proj.ModProjectile is not OniMeiMirrorStand stand || stand.shattered
                    || stand.timer < RiseFrames) {
                    continue;
                }
                stand.Echo(aim, knockback, sizeMul);
                return true;
            }
            return false;
        }

        private void Echo(float aim, float knockback, float sizeMul) {
            shattered = true;
            shatterTimer = 0;
            Projectile.netUpdate = true;

            //镜里那一刀朝立像自己的正面挥，不是照抄玩家的角度——
            //所以"把镜子摆在哪、朝哪"才是这枚铭真正在玩的东西
            float mirrored = Facing > 0 ? 0f : MathHelper.Pi;
            //玩家那一刀的上下取向仍然保留，只把左右换成立像的朝向
            float pitch = MathHelper.WrapAngle(aim);
            if (MathF.Abs(pitch) > MathHelper.PiOver2) {
                pitch = MathHelper.WrapAngle(MathHelper.Pi - pitch);
            }
            float echoAim = MathHelper.WrapAngle(mirrored + (Facing > 0 ? pitch : -pitch));

            int damage = Math.Max(1, (int)(BaseWeaponDamage * OniMeiCombat.MirrorEchoDamageMul));
            CrimsonRendCleave.Fire(Owner, Projectile.Center, echoAim, damage, knockback * 0.4f,
                sizeMul * 0.85f, flip: Facing, Projectile.GetSource_FromAI(), CleaveStyle.MirrorEcho);
            PlayEchoCue();
        }

        public override void AI() {
            timer++;
            swayPhase += 0.06f;
            if (shattered && ++shatterTimer >= ShatterFrames) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.26f, 0.24f, 0.28f));
            if (!Main.dedServ && !shattered && timer % 12 == 0) {
                //镜面反光：一线冷白沿纸缘扫过，读作"这是面镜子不是纸片"
                PRTLoader.NewParticle<PRT_CrimsonSpark>(
                    Projectile.Center + Main.rand.NextVector2Circular(StandHalfWidth, StandHalfHeight),
                    -Vector2.UnitY * 0.4f, GlassRim * 0.7f, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(14, 22), affectedByGravity: false);
            }
        }

        private void PlayEchoCue() {
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.55f, Volume = 0.45f }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            Owner.CWR()?.GetScreenShake(1.2f);
            //碎镜：片状碎屑向外崩，带一点转动，不做成雾
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(-0.2f, 0.2f);
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(Projectile.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 6.5f),
                    i % 3 == 0 ? GlassRim : GlassBody, Main.rand.NextFloat(0.18f, 0.34f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(shattered);
            writer.Write((short)shatterTimer);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            shattered = reader.ReadBoolean();
            shatterTimer = reader.ReadInt16();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) {
                return false;
            }
            float fade = MathHelper.Clamp(timer / (float)RiseFrames, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            if (shattered) {
                fade *= 1f - shatterTimer / (float)ShatterFrames;
            }
            if (fade <= 0.01f) {
                return false;
            }

            Vector2 center = Projectile.Center - Main.screenPosition;
            float rise = (1f - MathHelper.Clamp(timer / (float)RiseFrames, 0f, 1f)) * 14f;
            center.Y += rise;
            float sway = MathF.Sin(swayPhase) * 0.035f;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);
            //镜身：暗底 + 冷白面 + 一道斜掠高光
            Main.EntitySpriteDraw(pixel, center + new Vector2(2f, 3f), src,
                new Color(14, 10, 16) * (fade * 0.6f), sway, half,
                new Vector2(StandHalfWidth * 2.1f, StandHalfHeight * 2.1f), SpriteEffects.None);
            Main.EntitySpriteDraw(pixel, center, src, GlassBody * (fade * 0.72f), sway, half,
                new Vector2(StandHalfWidth * 2f, StandHalfHeight * 2f), SpriteEffects.None);
            Main.EntitySpriteDraw(pixel, center, src, GlassRim * (fade * 0.42f),
                sway + 0.7f, half, new Vector2(StandHalfWidth * 0.5f, StandHalfHeight * 2.4f),
                SpriteEffects.None);

            //镜中人：玩家自己的刀影，朝向与立像一致
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 size = blade.Size();
            Vector2 origin = size * OniBladePose.HiltUV;
            Main.EntitySpriteDraw(blade, center, null, new Color(40, 26, 34) * (fade * 0.75f),
                Facing > 0 ? -0.9f : 0.9f, origin, 0.55f,
                Facing > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
