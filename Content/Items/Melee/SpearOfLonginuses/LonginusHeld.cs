using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    internal class LonginusHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item + "Melee/Longinus";

        private NPC markNPC;
        private float markReveal;
        private float levelFlash;
        private float sheathSpin;
        private bool fullCharge;
        private Vector2[] shaftPoints;

        /// <summary>光之翼开关，走 proj ai 同步给远端</summary>
        public bool WingsOn => Projectile.ai[1] > 0.5f;
        /// <summary>本地平滑展开度 0~1，各端由 <see cref="WingsOn"/> 目标自行推进</summary>
        private float wingOpen;
        /// <summary>展开瞬间背后 AT 薄膜闪现</summary>
        private float wingFlash;
        /// <summary>展开瞬间从爆心扩张衰减的大光环</summary>
        private float unfurlRing;
        private bool lastWingsOn;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => false;

        /// <summary>在场帧戳：AI 与绘制各盖一次（时停中 AI 停摆靠绘制维持），光之翼层据此跳过空表扫描</summary>
        internal static ActivityStamp PresenceStamp;

        //全客户端至多一个被标记的 NPC（单一写方），换目标只清上一个，免去每帧全表清扫
        private static int signedNpcIndex = -1;
        private static int signedNpcType = -1;

        private bool isProj() {
            int throwType = ModContent.ProjectileType<LonginusThrow>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == throwType && p.Center.To(Owner.Center).LengthSquared() < 9000) {
                    return false;
                }
            }
            return true;
        }

        public override void AI() {
            if (Item.type != SpearOfLonginus.ID || !isProj()) {
                Projectile.Kill();
                return;
            }
            PresenceStamp.Stamp();
            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            fullCharge = Owner.HeldItem?.ModItem is SpearOfLonginus held && held.ChargeGrade >= SpearOfLonginus.MaxChargeGrade;
            if (Projectile.IsOwnedByLocalPlayer()) {
                StickToOwner();
                Charge();
                SpawnHolyCross();
            }
            NPC npc = Projectile.Center.FindClosestNPC(1900);
            if (npc != null) {
                //换目标时只清上一个（带槽位复用校验），效果等价旧的全表清扫
                if (signedNpcIndex >= 0 && signedNpcIndex != npc.whoAmI) {
                    NPC old = Main.npc[signedNpcIndex];
                    if (old.active && old.type == signedNpcType) {
                        old.CWR().LonginusSign = false;
                    }
                }
                npc.CWR().LonginusSign = true;
                signedNpcIndex = npc.whoAmI;
                signedNpcType = npc.type;
            }
            //标记光轮显隐，换目标重新显现
            if (npc != markNPC) {
                markNPC = npc;
                markReveal = 0f;
            }
            if (markNPC != null && markNPC.active) {
                markReveal = MathHelper.Clamp(markReveal + 0.06f, 0f, 1f);
            }
            if (levelFlash > 0f) {
                levelFlash -= 0.04f;
            }
            UpdateWings();
            //能量鞘缠绕滚动，充能越高越快
            float gradeProg = Owner.HeldItem?.ModItem is SpearOfLonginus sol
                ? sol.ChargeGrade / (float)SpearOfLonginus.MaxChargeGrade : 0f;
            sheathSpin += 0.14f + gradeProg * 0.12f;
            Projectile.ai[0]++;
        }

        /// <summary>右键切换光之翼，仅 owner 端调用，走 ai[1]+netUpdate 同步</summary>
        public void ToggleWings() {
            Projectile.ai[1] = WingsOn ? 0f : 1f;
            Projectile.netUpdate = true;
        }

        /// <summary>各端自行推进展开度；ai[1] 边沿触发音效与展开演出，远端同样可见可闻</summary>
        private void UpdateWings() {
            if (WingsOn != lastWingsOn) {
                lastWingsOn = WingsOn;
                if (WingsOn) {
                    wingFlash = 1f;
                    unfurlRing = 1f;
                    SoundStyle open = "CalamityMod/Sounds/Item/HeavenlyGaleFire".GetSound();
                    open.Volume = 0.55f;
                    open.Pitch = 0.35f;
                    SoundEngine.PlaySound(open, Projectile.Center);
                    //AT 音轻量版垫底，宗教感主题音
                    SoundEngine.PlaySound(SpearOfLonginus.AT with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                }
                else {
                    SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.4f, Pitch = -0.35f }, Projectile.Center);
                }
            }
            //展开 13 帧收回 9 帧；窜长→扇开的分段在几何侧由 openT 拆解
            wingOpen = MathHelper.Clamp(wingOpen + (WingsOn ? 1f / 13f : -1f / 9f), 0f, 1f);
            if (wingFlash > 0f) {
                wingFlash -= 0.055f;
            }
            if (unfurlRing > 0f) {
                unfurlRing -= 0.05f;
            }
        }

        /// <summary>由 <see cref="LonginusWingsRender"/> 在玩家身后图层调用，画双侧光之翼与爆心涌泉</summary>
        public void DrawWingsLayer() {
            if (wingOpen <= 0.01f) {
                return;
            }
            float gravDir = Owner.gravDir;
            Vector2 anchor = Owner.MountedCenter + new Vector2(0, -8f * gravDir);

            //展开瞬间背后闪一层 AT 薄膜呼应主题
            if (wingFlash > 0.01f) {
                float t = 1f - wingFlash;
                LonginusVFX.DrawATField(anchor, new Vector2(0, -gravDir), 170f
                    , MathHelper.Clamp(t * 2.4f, 0f, 1f), 0f, wingFlash * 0.5f, 1
                    , Projectile.whoAmI * 0.173f, 0.9f);
            }
            //爆发瞬间扩张衰减的大光环
            if (unfurlRing > 0.01f) {
                float rt = 1f - unfurlRing;
                LonginusVFX.DrawHalo(anchor, 70f + rt * 210f, 0.92f, 1f, 0.2f
                    , unfurlRing * 0.55f);
            }

            LonginusWings.Draw(Owner, wingOpen, 390f, 0.85f);

            //爆心涌泉：向心光丝 + 光轮脉动，撑起"所有光带从这一点涌出"
            float breathe = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
            LonginusVFX.DrawChargeIntake(anchor, 88f + wingOpen * 24f, 0.5f + wingOpen * 0.5f
                , wingOpen >= 0.99f ? 0.7f : 0f, wingOpen * 0.9f, Projectile.whoAmI * 0.293f);
            LonginusVFX.DrawHalo(anchor, 22f + wingOpen * 10f, 0.9f, wingOpen, breathe
                , wingOpen * 0.85f, LonginusVFX.HolyGold);
        }

        /// 持续注入圣神能量，满条转一次立场充能(最高 <see cref="SpearOfLonginus.MaxChargeGrade"/> 层)
        /// 与盗贼潜伏解耦，仅依赖持枪时间
        public void Charge() {
            SpearOfLonginus longinus = (SpearOfLonginus)Owner.HeldItem.ModItem;

            //满层停累积
            if (longinus.ChargeGrade >= SpearOfLonginus.MaxChargeGrade) {
                longinus.HolyEnergy = SpearOfLonginus.HolyEnergyMax;
                return;
            }

            //每帧累积一次能量；玩家持枪不动时也会涨
            //充能可视化走 LonginusCharge 吸入场，不再喷星屑粒子

            longinus.HolyEnergy++;
            //光之翼展开期间力量解放，充能速度翻倍
            if (WingsOn) {
                longinus.HolyEnergy++;
            }

            //能量满升层
            if (longinus.HolyEnergy >= SpearOfLonginus.HolyEnergyMax) {
                longinus.HolyEnergy = 0;
                longinus.ChargeGrade++;
                if (longinus.ChargeGrade > SpearOfLonginus.MaxChargeGrade) {
                    longinus.ChargeGrade = SpearOfLonginus.MaxChargeGrade;
                }

                SoundStyle lightningStrikeSound = "CalamityMod/Sounds/Custom/HeavenlyGaleLightningStrike".GetSound();
                lightningStrikeSound.Volume = 0.25f;
                SoundEngine.PlaySound(lightningStrikeSound, Projectile.Center);
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/HeavenlyGaleFire".GetSound(), Projectile.Center);

                //升层演出：枪前展开一层AT力场薄膜
                levelFlash = 1f;
            }
        }

        /// <summary>维持身下HolyCross，绘制能量条与立场</summary>
        private void SpawnHolyCross() {
            if (Owner.CountProjectilesOfID<HolyCross>() == 0) {
                Projectile.NewProjectile(Owner.GetSource_FromThis(), Owner.Center, Vector2.Zero
                    , ModContent.ProjectileType<HolyCross>(), 0, 0, Owner.whoAmI);
            }
        }

        public void StickToOwner() {
            Owner.heldProj = Projectile.whoAmI;
            Projectile.rotation = ToMouseA;
            Owner.direction = Math.Sign(ToMouse.X);

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

            Projectile.Center = Owner.GetPlayerStabilityCenter() + UnitToMouseV * 70;
            //满层微震颤
            if (fullCharge) {
                Projectile.Center += Main.rand.NextVector2Circular(1.3f, 1.3f);
            }
            Projectile.timeLeft = 2;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            //枪身双螺旋能量鞘 + 枪尖吸入场，随充能进度增强
            if (Owner.HeldItem?.ModItem is SpearOfLonginus sp) {
                float fillProg = sp.ChargeGrade >= SpearOfLonginus.MaxChargeGrade
                    ? 1f : sp.HolyEnergy / (float)SpearOfLonginus.HolyEnergyMax;
                float gradeProg = sp.ChargeGrade / (float)SpearOfLonginus.MaxChargeGrade;

                shaftPoints ??= new Vector2[14];
                Vector2 tip = Projectile.Center + Projectile.velocity * 66f;
                Vector2 tail = Projectile.Center - Projectile.velocity * 38f;
                for (int i = 0; i < shaftPoints.Length; i++) {
                    shaftPoints[i] = Vector2.Lerp(tip, tail, i / (shaftPoints.Length - 1f));
                }
                LonginusVFX.DrawHelixTrail(shaftPoints, shaftPoints.Length, 4.5f + gradeProg * 2.5f
                    , 7f + gradeProg * 3f, sheathSpin, 0f, 0.30f + gradeProg * 0.45f
                    , 0.15f + gradeProg * 0.45f, 3.1f);

                LonginusVFX.DrawChargeIntake(tip, 54f + gradeProg * 16f
                    , fullCharge ? 1f : 0.30f + fillProg * 0.70f, fullCharge ? 1f : 0f
                    , 0.85f, Projectile.whoAmI * 0.137f);
            }

            //标记目标头顶的倾斜光轮
            if (markNPC != null && markNPC.active && markReveal > 0.01f) {
                float haloR = MathHelper.Clamp(markNPC.width * 0.5f + 26f, 34f, 120f);
                Vector2 haloPos = markNPC.Top + new Vector2(0, -30f - haloR * 0.18f);
                float breathe = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.1f);
                LonginusVFX.DrawHalo(haloPos, haloR, 0.30f, markReveal, breathe * 0.5f, 0.75f);
            }
            //升层立场薄膜闪现
            if (levelFlash > 0.01f && Owner.HeldItem?.ModItem is SpearOfLonginus longinus) {
                float t = 1f - levelFlash;
                LonginusVFX.DrawATField(Projectile.Center + Projectile.velocity * 60f, Projectile.velocity
                    , 110f + longinus.ChargeGrade * 10f, MathHelper.Clamp(t * 2.6f, 0f, 1f), 0f
                    , levelFlash * 0.65f, 1, Projectile.whoAmI * 0.211f, 0.55f);
            }
            //满层枪尖小光轮
            if (fullCharge) {
                Vector2 tip = Projectile.Center + Projectile.velocity * 66f;
                float breathe = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5.3f);
                LonginusVFX.DrawHalo(tip, 24f, 0.85f, 1f, breathe, 0.8f, LonginusVFX.HolyGold);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            PresenceStamp.Stamp();
            Texture2D value = TextureAssets.Item[SpearOfLonginus.ID].Value;
            int dir = Owner.direction * (int)Owner.gravDir;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4 + (dir > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale * 0.9f, dir > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
