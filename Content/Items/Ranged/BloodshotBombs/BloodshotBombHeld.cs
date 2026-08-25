using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.BloodshotBombs
{
    /// <summary>
    /// 手中的泣血瞳雷:选中武器即点燃引线
    /// ai[0]=引线已燃帧数，ai[1]=掏弹回充帧数
    /// 左键随时掷出，引线烧尽仍不撒手则在手中炸开并伤及持有者
    /// </summary>
    internal class BloodshotBombHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "BloodshotBombProj";

        /// <summary>四帧序列各自的引线烧点相对贴图中心的偏移(朝右时)</summary>
        internal static readonly Vector2[] FuseTipOffset = new Vector2[] {
            new Vector2(10f, -8.5f), new Vector2(5f, -7.5f),
            new Vector2(3f, -4.5f), new Vector2(6f, -2.5f) };

        private ref float FuseTime => ref Projectile.ai[0];
        private ref float RearmTimer => ref Projectile.ai[1];
        /// <summary>各端本地维护的上一档位，-1 表示引线尚未跨过任何档</summary>
        private int lastTier = -1;

        private int TargetItemID => ModContent.ItemType<BloodshotBomb>();

        public override void SetStaticDefaults() => Main.projFrames[Projectile.type] = 4;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead || Item.type != TargetItemID) {
                Projectile.Kill();//收起武器等于掐灭引线
                return;
            }

            StickToOwner();

            //掏弹间隙:引线未点燃
            if (RearmTimer > 0) {
                if (--RearmTimer <= 0) {
                    lastTier = -1;
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = 0.6f }, Projectile.Center);
                }
                return;
            }

            FuseTime++;
            int tier = BloodshotBomb.GetTier(FuseTime);
            if (tier != lastTier) {
                if (lastTier >= 0) {
                    TierUpEffect(tier);
                }
                lastTier = tier;
            }

            UpdateFuseVFX();

            //引线烧尽:在手中炸开
            if (FuseTime >= BloodshotBomb.FuseMaxTime) {
                SelfDetonate();
                return;
            }

            if (Projectile.IsOwnedByLocalPlayer() && DownLeft && !Owner.mouseInterface) {
                ThrowBomb(tier);
            }
        }

        private void StickToOwner() {
            Owner.heldProj = Projectile.whoAmI;
            float bob = MathF.Sin(Main.GameUpdateCount * 0.06f) * 1.2f;
            Projectile.Center = Owner.MountedCenter
                + new Vector2(Owner.direction * 11f, (-10f + bob) * Owner.gravDir);
            //前臂托举炸弹
            float holdAngle = Owner.direction == 1 ? -0.62f : -MathHelper.Pi + 0.62f;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , holdAngle - MathHelper.PiOver2);
            Projectile.timeLeft = 2;
        }

        internal int GetFuseFrame() =>
            Math.Clamp((int)(FuseTime / (BloodshotBomb.FuseMaxTime / 4f)), 0, 3);

        private Vector2 GetFuseTipWorld() {
            Vector2 off = FuseTipOffset[GetFuseFrame()];
            return Projectile.Center + new Vector2(off.X * Owner.direction, off.Y * Owner.gravDir);
        }

        private void UpdateFuseVFX() {
            float redness = FuseTime / BloodshotBomb.FuseMaxTime;
            Lighting.AddLight(Projectile.Center, 0.35f + redness * 0.6f
                , 0.22f * (1f - redness), 0.1f * (1f - redness));

            //烧点火星
            Vector2 tip = GetFuseTipWorld();
            if (Main.GameUpdateCount % 4 == 0) {
                Vector2 vel = new Vector2(0, -1f).RotatedByRandom(0.9f) * Main.rand.NextFloat(0.5f, 1.4f);
                PRTLoader.NewParticle<PRT_Spark>(tip, vel
                    , Color.Lerp(new Color(255, 190, 80), new Color(255, 90, 40), Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, 14, Owner);
            }

            //末段狂闪警告
            if (FuseTime >= BloodshotBomb.FuseMaxTime - BloodshotBomb.WarnTime
                && (int)FuseTime % 15 == 0) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 1f }, Projectile.Center);
            }
        }

        /// <summary>跨档演出:红环 + 火星迸溅 + 逐档升调的提示音</summary>
        private void TierUpEffect(int tier) {
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = -0.25f + tier * 0.35f }
                , Projectile.Center);
            Vector2 tip = GetFuseTipWorld();
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(tip, VaultUtils.RandVr(1.5f, 4f)
                    , new Color(255, 70 + tier * 30, 45), 0.55f)?.Configure(true, 20, Owner);
            }
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero
                , new Color(255, 70, 70), 1f)?.Configure(0.12f, 0.45f + tier * 0.18f, 12);
        }

        private void ThrowBomb(int tier) {
            int damage = (int)(Owner.GetWeaponDamage(Item) * BloodshotBomb.TierDamageMul[tier]);
            Vector2 vel = UnitToMouseV * Item.shootSpeed;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), Projectile.Center, vel
                , ModContent.ProjectileType<BloodshotBombThrown>(), damage, Item.knockBack
                , Owner.whoAmI, tier, FuseTime / BloodshotBomb.FuseMaxTime);
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f }, Projectile.Center);
            int faceDir = Math.Sign(ToMouse.X);
            if (faceDir != 0) {
                Owner.direction = faceDir;//出手瞬间面向准星
            }
            FuseTime = 0;
            RearmTimer = BloodshotBomb.RearmTime;
            lastTier = -1;
            Projectile.netUpdate = true;
        }

        /// <summary>引线烧尽:以三档威力原地炸开，演出各端自行播放，结算只在持有端</summary>
        private void SelfDetonate() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int damage = (int)(Owner.GetWeaponDamage(Item) * BloodshotBomb.TierDamageMul[2]);
                //ai[2]=1 表示原地即爆，血肉照常迸出
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<BloodshotBombThrown>(), damage, Item.knockBack
                    , Owner.whoAmI, 2f, 1f, 1f);
                PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                    ModContent.GetInstance<BloodshotBomb>().GetLocalization("SelfBoom").ToNetworkText(Owner.name));
                Owner.Hurt(reason, Item.damage * 2, 0);
                FuseTime = 0;
                RearmTimer = BloodshotBomb.RearmTime;
                Projectile.netUpdate = true;
            }
            lastTier = -1;
        }

        public override void OnKill(int timeLeft) {
            //拿着燃了一半的炸弹换武器:掐灭引线的一缕青烟
            if (FuseTime > 0 && RearmTimer <= 0) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(GetFuseTipWorld(), new Vector2(0, -0.7f).RotatedByRandom(0.4f)
                        , new Color(120, 110, 105), 0.08f)?.Configure(22, 0.4f, 0.02f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = tex.Height / Main.projFrames[Projectile.type];
            Rectangle rect = new Rectangle(0, GetFuseFrame() * frameHeight, tex.Width, frameHeight);
            Vector2 origin = rect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset;
            int dir = Owner.direction * (int)Owner.gravDir;
            SpriteEffects effects = dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            bool rearming = RearmTimer > 0;
            float redness = rearming ? 0f : FuseTime / BloodshotBomb.FuseMaxTime;
            bool warning = !rearming && FuseTime >= BloodshotBomb.FuseMaxTime - BloodshotBomb.WarnTime;

            //心跳脉动，越接近爆炸越急
            float pulseFreq = 0.07f + redness * 0.18f + (warning ? 0.25f : 0f);
            float pulse = MathF.Sin(FuseTime * pulseFreq);
            float scale = Projectile.scale * (1f + pulse * (0.02f + redness * 0.06f));
            if (rearming) {
                //掏出新弹:从掌心弹性放大
                float t = 1f - RearmTimer / (float)BloodshotBomb.RearmTime;
                scale *= 0.35f + 0.65f * (1f - MathF.Pow(1f - t, 3f));
            }

            //越烧越红:压掉绿蓝通道并抬起自发光红
            Color body = lightColor;
            if (redness > 0f) {
                body.R = (byte)Math.Max(body.R, (int)(110 + 145 * redness));
                body.G = (byte)(body.G * (1f - 0.66f * redness));
                body.B = (byte)(body.B * (1f - 0.72f * redness));
            }
            if (warning && FuseTime % 8 < 4) {
                body = Color.Lerp(body, Color.White, 0.45f);
            }

            //充血辉光垫底(A=0 加色)
            if (redness > 0.05f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float glowStrength = 0.22f + redness * 0.55f + (warning ? 0.25f : 0f) + pulse * 0.08f;
                Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 28, 18, 0) * glowStrength
                    , 0f, glow.Size() / 2f, 0.5f + redness * 0.3f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, drawPos, rect, body, Projectile.rotation, origin, scale, effects, 0);
            return false;
        }
    }
}
