using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove.Projectiles
{
    /// <summary>
    /// 散射预兆：ai[0]=宿主NPC索引 ai[1]=锁定瞄角（999=未锁定，跟踪中）ai[2]=样式。
    /// 幽灵扇面直接预演齐射：逐槽画出将要飞行的弹体虚影，GapSlot 那条巷永远空着——
    /// 看得见的缺口就是安全巷（缺口可见性=缺口承诺）。锁定后扇面冻结不再跟踪
    /// </summary>
    internal class EMScatterTelegraphProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Age => ref Projectile.localAI[0];
        private ref float LockPopped => ref Projectile.localAI[1];
        private int HostIndex => (int)Projectile.ai[0];
        private bool Locked => Projectile.ai[1] < 900f;
        private int Style => (int)Projectile.ai[2];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EliteMoveNPC.ScatterTrackFrames + EliteMoveNPC.ScatterLockFrames + 8;
            Projectile.netImportant = true;
        }

        private bool TryHost(out NPC npc, out EliteMoveNPC global) {
            global = null;
            if (!HostIndex.TryGetNPC(out npc) || !npc.TryGetGlobalNPC(out global)
                || EliteMoveSets.FamilyOf(npc.type) != EliteFamily.Scatter) {
                return false;
            }
            return true;
        }

        /// <summary>当前扇面中轴：未锁定时逐帧读目标方向（各端同源收敛），锁定后读同步角</summary>
        private float AimAngle(NPC npc) {
            if (Locked) {
                return Projectile.ai[1];
            }
            Player target = Main.player[npc.target];
            return target.Alives() ? (target.Center - npc.Center).ToRotation() : npc.direction > 0 ? 0f : MathHelper.Pi;
        }

        public override void AI() {
            if (!TryHost(out NPC npc, out EliteMoveNPC global)) {
                Projectile.Kill();
                return;
            }
            Age++;
            Projectile.Center = npc.Center;

            //拉弓定身：地面射手只压横向，浮游/爬墙压全向
            global.StampHold(0.25f, Style != 0);

            if (Locked && LockPopped == 0f) {
                LockPopped = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.75f, Pitch = 0.2f }, Projectile.Center);
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.Torch, Vector2.Zero, 140, GetTint(npc), 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GetTint(npc).ToVector3() * 0.2f);
        }

        private static Color GetTint(NPC npc)
            => EliteMoveSets.Profiles.TryGetValue(npc.type, out EliteProfile p) ? p.Tint : Color.Wheat;

        public override bool PreDraw(ref Color lightColor) {
            if (!TryHost(out NPC npc, out _)) {
                return false;
            }
            int styleProj = EMScatterBoltProj.StyleProjId(Style);
            Main.instance.LoadProjectile(styleProj);
            Texture2D boltTex = TextureAssets.Projectile[styleProj].Value;
            Texture2D ray = CWRAsset.Extra_98.Value;
            Color tint = GetTint(npc);
            float center = AimAngle(npc);
            float appear = MathHelper.Clamp(Age / 12f, 0f, 1f);
            float pulse = Locked ? 1f : 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f);

            for (int i = 0; i < EliteMoveNPC.FanSlots; i++) {
                if (i == EliteMoveNPC.GapSlot) {
                    continue;    //缺口槽位不画=空巷可见，与发射循环同一常量
                }
                float angle = center + EliteMoveNPC.SpreadHalfAngle * (-1f + 2f * i / (EliteMoveNPC.FanSlots - 1));
                Vector2 dir = angle.ToRotationVector2();
                Vector2 ghostPos = npc.Center + dir * (40f + 5f * MathF.Sin(Age * 0.2f + i)) - Main.screenPosition;
                //弹体虚影：原版贴图实拍预演（真alpha着色），锁定后停止呼吸
                Color ghost = Color.Lerp(lightColor, tint, 0.4f) * (0.5f * appear * pulse);
                Main.EntitySpriteDraw(boltTex, ghostPos, null, ghost, angle + MathHelper.PiOver2,
                    boltTex.Size() / 2f, 1f, SpriteEffects.None, 0);
                //弹道细芒：加色薄条指出飞行线
                Color lane = tint with { A = 0 } * (0.3f * appear * pulse);
                Main.EntitySpriteDraw(ray, npc.Center + dir * 70f - Main.screenPosition, null, lane,
                    angle, ray.Size() / 2f, new Vector2(1.1f, 0.06f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
