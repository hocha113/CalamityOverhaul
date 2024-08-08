using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal abstract class BaseRapiers : BaseHeldProj
    {
        #region Data
        public int maxTimeLeft;

        public int stabIndex;

        public int maxStabNum = 5;

        public int overHitModeing = 0;

        public bool Fading;

        public bool CanUse;

        private bool onInitializedBool = false;

        protected float SkialithVarSpeedMode = 6;

        protected float PremanentToSkialthRot = 0;

        protected Vector2 OffsetPos;

        protected Vector2 PermanentOffset = Vector2.Zero;

        protected Vector2 StabVec;

        protected Vector2 IntermediateTransPos = new Vector2(75, 0);

        protected List<NPC> HitNPCs = [];

        protected List<SkialithStruct> SkialithEntitys = [];

        protected Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.Full;

        protected SoundStyle? ShurikenOut = null;

        protected float StabbingSpread = 0.45f;

        protected int StabAmplitudeMin = 60;

        protected int StabAmplitudeMax = 170;

        protected Vector2 drawOrig = Vector2.Zero;

        public override string Texture => CWRConstant.Placeholder3;

        public virtual Texture2D TextureValue => CWRUtils.GetT2DValue(Texture);

        public Item Item {
            get {
                Item itemValue = Owner.ActiveItem();
                if (itemValue == null) {
                    itemValue = new Item();
                }
                return itemValue;
            }
        }

        public virtual string GlowPath => CWRConstant.Placeholder;

        public virtual Texture2D GlowValue => CWRUtils.GetT2DValue(GlowPath);
        #endregion

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.Size = new Vector2(16);
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 1;
            SetRapiers();
        }

        public virtual void SetRapiers() {

        }

        public virtual void Initialized() {

        }

        public virtual void ExtraShoot() {

        }

        protected void SetProjTimeInAttakSpeed() {
            Projectile.timeLeft = (int)(Item.useAnimation * (1f / Owner.GetTotalAttackSpeed(DamageClass.Melee)));
            maxTimeLeft = Projectile.timeLeft;
        }

        public float Ease(float x) => (float)Math.Sqrt(1.0 - Math.Pow(x - 1.0, 2));

        public virtual void PreInOwnerUpdate() {

        }

        public virtual void PostInOwnerUpdate() {

        }
        /// <summary>
        /// 更新挥刺的行为
        /// </summary>
        /// <param name="stabTimer"></param>
        private void UpdateFading(int stabTimer) {
            if (!Fading) {
                float totalTime = maxTimeLeft * 0.2f;
                float totalTimeOver2 = totalTime * 0.5f;
                if (stabTimer < totalTime) {
                    if (stabTimer == 1) {
                        Projectile.velocity = UnitToMouseV.RotatedByRandom(StabbingSpread);
                        StabVec = new Vector2(Main.rand.NextFloat(StabAmplitudeMin, StabAmplitudeMax), 0);
                        HitNPCs.Clear();
                    }

                    if (stabTimer < totalTimeOver2) {
                        float lerper = stabTimer / totalTimeOver2;
                        //刺入的矫正值
                        OffsetPos = Vector2.Lerp(IntermediateTransPos, StabVec, Ease(lerper)).RotatedBy(Projectile.rotation - MathHelper.PiOver2);
                    }
                    else {
                        if (!CanUse) {
                            AddSkialithEntity();
                            if (Projectile.IsOwnedByLocalPlayer()) {
                                ExtraShoot();
                            }
                            SoundEngine.PlaySound(ShurikenOut.Value with { MaxInstances = 6 }, Projectile.Center);
                            CanUse = true;
                        }

                        float lerper = (stabTimer - totalTimeOver2) / totalTimeOver2;
                        //刺出的矫正值
                        OffsetPos = Vector2.Lerp(StabVec, IntermediateTransPos, Ease(lerper)).RotatedBy(Projectile.rotation - MathHelper.PiOver2);
                    }
                }
                else {
                    CanUse = false;
                    stabIndex++;
                    Projectile.timeLeft = maxTimeLeft;

                    stretch = (Player.CompositeArmStretchAmount)Main.rand.Next(4);
                }

                if (!Owner.channel && stabIndex > maxStabNum && Projectile.timeLeft == maxTimeLeft) {
                    Projectile.timeLeft = 5;
                    Fading = true;
                }
            }
        }

        public override void AI() {
            if (!onInitializedBool) {
                if (drawOrig == Vector2.Zero) {
                    drawOrig = new Vector2(0, TextureValue.Height);
                }
                if (!ShurikenOut.HasValue) {
                    ShurikenOut = Item.UseSound;
                    if (!ShurikenOut.HasValue) {
                        ShurikenOut = CWRSound.ShurikenOut;
                    }
                }
                SetProjTimeInAttakSpeed();
                Initialized();
                onInitializedBool = true;
            }

            SetHeld();
            PreInOwnerUpdate();

            Owner.itemTime = Owner.itemAnimation = 2;//锁死玩家的物品使用时间
            Owner.direction = Math.Sign(Projectile.velocity.X);//强行设置玩家朝向
            Projectile.velocity = Projectile.velocity.UnitVector();//弹幕速度归一化，因为后续需要使用速度做其他的用处
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            //最大存在周期时间减去当前剩余时间得到使用时间
            UpdateFading(maxTimeLeft - Projectile.timeLeft);
            UpdateSkialith();
            Projectile.Center = Owner.GetPlayerStabilityCenter() + OffsetPos + PermanentOffset + Projectile.velocity * PremanentToSkialthRot;
            Owner.SetCompositeArmFront(true, stretch, Projectile.rotation - MathHelper.Pi);

            PostInOwnerUpdate();
        }
        /// <summary>
        /// 更新残影实体
        /// </summary>
        private void UpdateSkialith() {
            for (int i = 0; i < SkialithEntitys.Count; i++) {
                SkialithStruct skialith = SkialithEntitys[i];
                skialith.time--;
                //skialith.pos += Owner.velocity;//不因为与玩家相对静止，这不是一个好主意，它并不会带来更好的视觉体验
                skialith.pos += skialith.ver * SkialithVarSpeedMode;
                SkialithEntitys[i] = new SkialithStruct(skialith.pos, skialith.ver, skialith.rot, skialith.time);
                if (SkialithEntitys[i].time <= 0) {
                    SkialithEntitys.RemoveAt(i);
                    continue;
                }
                ExtraUpdateSkialithEntity(skialith);
            }
        }
        /// <summary>
        /// 载入残影实体
        /// </summary>
        public virtual void AddSkialithEntity() {
            SkialithEntitys.Add(new SkialithStruct(Projectile.Center, Projectile.velocity, Projectile.rotation, 15));
        }
        /// <summary>
        /// 一个额外的更新残影的函数，可以用于自定义更新残影的速度变化量等元素
        /// </summary>
        /// <param name="skialith"></param>
        public virtual void ExtraUpdateSkialithEntity(SkialithStruct skialith) {

        }

        public virtual void HitPlayerSoundEffect(NPC target, NPC.HitInfo hit) {
            float sincrit = 0;
            if (hit.Crit) {
                sincrit += 0.2f;
            }
            (float min, float max) pot = (-0.1f + +sincrit, 0.1f + +sincrit);
            if (CWRLoad.NPCValue.TheofSteel[target.type]) {
                SoundEngine.PlaySound(CWRSound.HitTheSteel with { MaxInstances = 3, PitchRange = pot, Volume = 0.5f }, Projectile.Center);
            }
            else {
                SoundStyle soundonFlesh = Main.rand.NextBool() ? CWRSound.HitTheFlesh_1 : CWRSound.HitTheFlesh_2;
                SoundEngine.PlaySound(soundonFlesh with { MaxInstances = 3, PitchRange = pot, Volume = 0.5f }, Projectile.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (NPCID.Sets.ProjectileNPC[target.type]) {
                return;
            }

            HitNPCs.Add(target);
            //Owner.CWR().SetScreenShake(4);
            HitPlayerSoundEffect(target, hit);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Owner.Center, Projectile.Center + Projectile.velocity.UnitVector() * overHitModeing)
                ? true
                : null;
        }

        public override bool? CanHitNPC(NPC target) {
            return HitNPCs.Contains(target) ? false : null;
        }

        public virtual void Draw1(Texture2D tex, Vector2 imgsOrig, Vector2 off, float fade, SkialithStruct afterImage, ref Color lightColor) {
            Main.spriteBatch.Draw(tex, imgsOrig + off, null, Color.White * fade * 0.3f
                , afterImage.rot - MathHelper.PiOver4, drawOrig, Projectile.scale, SpriteEffects.None, 0);
        }

        public virtual void Draw2(Texture2D tex, Vector2 imgsOrig, Vector2 off, float opacity, SkialithStruct afterImage, ref Color lightColor) {
            Main.spriteBatch.Draw(tex, imgsOrig + off, null, Color.White * opacity * 0.5f
                , Projectile.rotation - MathHelper.PiOver4, drawOrig, Projectile.scale, SpriteEffects.None, 0);
        }

        public virtual void Draw3(Texture2D tex, Vector2 off, float fade, Color lightColor) {
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition + off
                , null, lightColor * fade, Projectile.rotation - MathHelper.PiOver4, drawOrig, Projectile.scale, SpriteEffects.None, 0);
        }

        public virtual void SetEffect1(Effect effect) {
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.1f);
            effect.Parameters["power"].SetValue(0.2f);
            effect.Parameters["offset"].SetValue(new Vector2(Main.screenPosition.X / Main.screenWidth * 0.5f, 0));
            effect.Parameters["speed"].SetValue(10f);
        }

        public virtual void SetEffect2(Effect effect, SkialithStruct afterImage, float opacity, float fade) {
            effect.Parameters["opacity"].SetValue(opacity * fade);
            effect.Parameters["drawColor"].SetValue(Color.Lerp(new Color(50, 0, 150, 0), new Color(200, 0, 0, 0), 1f - afterImage.time / 15f).ToVector4());
        }

        public virtual void SetEffect3(Effect effect, SkialithStruct afterImage, float opacity, float fade) {
            effect.Parameters["drawColor"].SetValue((new Color(255, 255, 255, 0) * 0.25f).ToVector4());
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Texture2D glow = GlowValue;
            Vector2 drawOffetLmbs = new Vector2(0, 109).RotatedBy(Projectile.rotation);
            float transparency = 1f;
            if (Projectile.timeLeft <= 5 && Fading) {
                transparency = Projectile.timeLeft / 5f;
            }

            Effect effect = Terraria.Graphics.Effects.Filters.Scene["CWRMod:twistColoringShader"].GetShader().Shader;
            SetEffect1(effect);
            for (int i = 0; i < SkialithEntitys.Count; i++) {
                SkialithStruct skialith = SkialithEntitys[i];
                Vector2 imgsOrig = skialith.pos - Main.screenPosition;
                float opacity = MathHelper.Lerp(0.5f, 0f, 1f - skialith.time / 15f);
                SetEffect2(effect, skialith, opacity, transparency);
                effect.CurrentTechnique.Passes[0].Apply();
                Draw1(glow, imgsOrig, drawOffetLmbs, transparency, skialith, ref lightColor);
                SetEffect3(effect, skialith, opacity, transparency);
                effect.CurrentTechnique.Passes[0].Apply();
                Draw2(glow, imgsOrig, drawOffetLmbs, opacity, skialith, ref lightColor);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(default, default, Main.DefaultSamplerState, default, RasterizerState.CullNone, default, Main.GameViewMatrix.TransformationMatrix);
            Draw3(tex, drawOffetLmbs, transparency, lightColor);
            Draw3(glow, drawOffetLmbs, transparency, Color.White);
            return false;
        }
    }
}
