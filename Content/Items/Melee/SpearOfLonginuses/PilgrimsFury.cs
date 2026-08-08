using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    /// <summary>时停处决：光轮降临 → AT力场层层展开 → 枪影顶住震颤 → 碎裂贯穿 → 十字终结</summary>
    internal class PilgrimsFury : ModProjectile, IPrimitiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private NPC Target => Main.npc[(int)Projectile.ai[1]];
        private int Time {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        //节拍窗口
        private const int SpreadStart = 8;
        private const int SpreadEnd = 64;
        private const int CrackStart = 84;
        private const int ShatterTick = 102;
        private const int TotalTime = 120;

        private Vector2 pinDir;
        private float fieldR;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        private Vector2 FieldCenter => Projectile.Center - pinDir * (fieldR * 0.45f);

        private float SpreadT => MathHelper.Clamp((Time - SpreadStart) / (float)(SpreadEnd - SpreadStart), 0f, 1f);
        private float PressT => MathHelper.Clamp((Time - SpreadEnd) / (float)(ShatterTick - SpreadEnd), 0f, 1f);
        /// <summary>裂纹预热0~0.3，碎裂后冲向1</summary>
        private float ShatterT {
            get {
                if (Time < ShatterTick) {
                    return MathHelper.Clamp((Time - CrackStart) / (float)(ShatterTick - CrackStart), 0f, 1f) * 0.30f;
                }
                return 0.30f + MathHelper.Clamp((Time - ShatterTick) / (float)(TotalTime - ShatterTick), 0f, 1f) * 0.70f;
            }
        }

        public override void AI() {
            if (!Target.Alives()) {
                Projectile.Kill();
                return;
            }

            if (Time == 0) {
                pinDir = (Target.Center - Main.player[Projectile.owner].Center).UnitVector();
                if (pinDir == Vector2.Zero) {
                    pinDir = Vector2.UnitX;
                }
                fieldR = MathHelper.Clamp(Math.Max(Target.width, Target.height) * 0.9f, 130f, 300f);
            }

            Projectile.Center = Target.Center;
            TimeFreezeSystem.RefreshNPC<PilgrimsFury>(Target, 2);

            //歌声渐强
            if (Time % 30 == 0) {
                SoundStyle belCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 1f + Time * 0.05f, Pitch = -0.2f + Time * 0.007f };
                SoundEngine.PlaySound(belCanto, Projectile.Center);
            }

            //碎裂瞬间：玻璃碎响 + 碎面飞散
            if (Time == ShatterTick) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.9f, Pitch = -0.1f }, Projectile.Center);
                SpawnShards(22, 1f);
            }

            Time++;
        }

        /// <summary>力场平面上撒碎面，径向外抛并向来袭侧弹出</summary>
        private void SpawnShards(int count, float speedMul) {
            if (Main.dedServ) {
                return;
            }
            Vector2 perp = pinDir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < count; i++) {
                Vector2 onPlane = FieldCenter + perp * (Main.rand.NextFloat(-0.9f, 0.9f) * fieldR)
                    + pinDir * Main.rand.NextFloat(-12f, 12f);
                Vector2 radial = (onPlane - FieldCenter).UnitVector();
                if (radial == Vector2.Zero) {
                    radial = perp;
                }
                Vector2 v = (radial * Main.rand.NextFloat(2f, 7f) - pinDir * Main.rand.NextFloat(1f, 4f)) * speedMul;
                PRTLoader.NewParticle<PRT_ATShard>(onPlane, v, LonginusVFX.Amber, Main.rand.NextFloat(0.6f, 1.3f))
                    ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(-0.24f, 0.24f));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 8; i++) {
                    Projectile.NewProjectileDirect(Projectile.FromObjectGetParent(), Projectile.Center
                    , new Vector2(0, 1), ModContent.ProjectileType<Godslight>(), Projectile.damage, 0, Projectile.owner, 0, 2f + i);
                }
            }
            SoundEngine.PlaySound(SpearOfLonginus.AT, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            SpawnShards(16, 1.5f);
            //定向星芒，四臂收敛
            for (int i = 0; i < 4; i++) {
                float rot = MathHelper.PiOver2 * i;
                Vector2 vr = rot.ToRotationVector2();
                for (int j = 0; j < 20; j++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (2.5f + j * 1.7f)
                        , LonginusVFX.HolyGold, Main.rand.Next(2, 6)).Configure(false, 37);
                }
            }
            //冲击帧只对屏幕内的处决触发
            Rectangle screen = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200
                , Main.screenWidth + 400, Main.screenHeight + 400);
            if (screen.Contains(Projectile.Center.ToPoint())) {
                //LonginusImpactRender.Trigger(1f, Projectile.Center);
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!Target.Alives() || Time <= 0) {
                return;
            }
            //碎裂后力场整体退潮
            float alpha = 0.9f;
            if (Time >= ShatterTick) {
                alpha *= 1f - (Time - ShatterTick) / (float)(TotalTime - ShatterTick) * 0.55f;
            }
            LonginusVFX.DrawATField(FieldCenter, -pinDir, fieldR, SpreadT, ShatterT, alpha, 3
                , Projectile.identity * 0.313f, 0.60f);

            //审判光轮自高处降临压向目标
            float haloReveal = MathHelper.Clamp(Time / 16f, 0f, 1f);
            float descend = MathHelper.Clamp(Time / 90f, 0f, 1f);
            descend = descend * descend * (3f - 2f * descend);
            float haloR = MathHelper.Clamp(Target.width * 0.5f + 30f, 40f, 130f);
            float haloY = MathHelper.Lerp(-fieldR * 0.9f - 40f, -Target.height * 0.5f - 26f, descend);
            float pulse = MathHelper.Clamp(ShatterT * 2f, 0f, 1f) + 0.3f;
            LonginusVFX.DrawHalo(Target.Center + new Vector2(0, haloY), haloR, 0.30f, haloReveal, pulse, 0.85f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Time <= SpreadStart || !Target.Alives()) {
                return false;
            }
            Texture2D value = TextureAssets.Item[SpearOfLonginus.ID].Value;
            float appear = MathHelper.Clamp((Time - SpreadStart) / 14f, 0f, 1f);

            //枪影顶住力场，蓄势加深震颤；碎裂后贯穿冲过
            float press = PressT * 12f;
            float advance = Time >= ShatterTick ? (Time - ShatterTick) * 14f : 0f;
            float trembleAmp = 0.6f + PressT * 2.4f + (Time >= CrackStart && Time < ShatterTick ? 1.2f : 0f);
            Vector2 tremble = Main.rand.NextVector2Circular(trembleAmp, trembleAmp);
            Vector2 spearCenter = FieldCenter - pinDir * (54f - press) + pinDir * advance + tremble;

            float fade = Time >= ShatterTick ? 1f - (Time - ShatterTick) / 18f : 1f;
            if (fade <= 0f) {
                return false;
            }
            float rot = pinDir.ToRotation() + MathHelper.PiOver4;
            Color ghost = (LonginusVFX.Crimson with { A = 0 }) * (0.62f * appear * fade);
            Main.EntitySpriteDraw(value, spearCenter - Main.screenPosition, null, ghost, rot
                , value.Size() / 2, 0.9f, SpriteEffects.None, 0);
            //震颤拖影
            Main.EntitySpriteDraw(value, spearCenter - pinDir * 6f - Main.screenPosition, null, ghost * 0.4f, rot
                , value.Size() / 2, 0.9f, SpriteEffects.None, 0);
            return false;
        }

        bool IWarpDrawable.CanDrawCustom() => false;

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>碎裂窗口的屏幕涟漪</summary>
        void IWarpDrawable.Warp() {
            if (Time < ShatterTick - 6) {
                return;
            }
            float p = MathHelper.Clamp((Time - (ShatterTick - 6)) / 24f, 0f, 1f);
            NeutronWarpHelper.DrawWarp(FieldCenter, fieldR * 6f, fieldR * 6f
                , 0.5f * (1f - p * 0.6f), p, 0f, "ShockwaveRing", 0.42f);
        }
    }
}
