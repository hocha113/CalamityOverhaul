using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 深渊厉鬼环境效果系统
    /// 为厉鬼附近的玩家添加恐怖氛围效果
    /// </summary>
    public class AbyssSpiritAtmosphere : ModPlayer
    {
        /// <summary>
        /// 最近的厉鬼
        /// </summary>
        private NPC nearestSpirit;

        /// <summary>
        /// 距离最近厉鬼的距离
        /// </summary>
        private float distanceToSpirit = float.MaxValue;

        /// <summary>
        /// 恐惧强度（0-1）
        /// </summary>
        private float fearIntensity = 0f;

        /// <summary>
        /// 心跳效果计时器
        /// </summary>
        private float heartbeatTimer = 0f;

        /// <summary>
        /// 是否播放心跳音效
        /// </summary>
        private int heartbeatSoundCooldown = 0;

        /// <summary>
        /// 视野暗化强度
        /// </summary>
        private float vignetteDarkness = 0f;

        public override void PreUpdate() {
            // 查找最近的厉鬼
            UpdateNearestSpirit();

            // 更新恐惧强度
            UpdateFearIntensity();

            // 更新心跳效果
            UpdateHeartbeat();

            // 更新视野效果
            UpdateVignette();
        }

        /// <summary>
        /// 查找最近的厉鬼
        /// </summary>
        private void UpdateNearestSpirit() {
            nearestSpirit = null;
            distanceToSpirit = float.MaxValue;

            int spiritType = ModContent.NPCType<TheSpiritofTheAbyss>();

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == spiritType) {
                    float dist = Vector2.Distance(Player.Center, npc.Center);
                    if (dist < distanceToSpirit) {
                        distanceToSpirit = dist;
                        nearestSpirit = npc;
                    }
                }
            }
        }

        /// <summary>
        /// 更新恐惧强度
        /// </summary>
        private void UpdateFearIntensity() {
            float targetIntensity = 0f;

            if (nearestSpirit != null) {
                // 根据距离计算恐惧强度
                const float MaxRange = 600f;
                const float MinRange = 200f;

                if (distanceToSpirit < MaxRange) {
                    if (distanceToSpirit < MinRange) {
                        targetIntensity = 1f;
                    } else {
                        targetIntensity = 1f - ((distanceToSpirit - MinRange) / (MaxRange - MinRange));
                    }

                    // 如果厉鬼在狩猎状态，恐惧强度翻倍
                    if (nearestSpirit.ai[0] == 3f) { // Hunting state
                        targetIntensity = Math.Min(targetIntensity * 2f, 1f);
                    }
                }
            }

            // 平滑过渡
            fearIntensity = MathHelper.Lerp(fearIntensity, targetIntensity, 0.05f);
        }

        /// <summary>
        /// 更新心跳效果
        /// </summary>
        private void UpdateHeartbeat() {
            if (fearIntensity > 0.3f) {
                heartbeatTimer += fearIntensity * 0.15f;

                // 心跳音效
                if (heartbeatSoundCooldown > 0) {
                    heartbeatSoundCooldown--;
                } else {
                    float heartbeatPhase = (float)Math.Sin(heartbeatTimer);
                    if (heartbeatPhase > 0.9f) {
                        PlayHeartbeatSound();
                        heartbeatSoundCooldown = (int)(60f / fearIntensity); // 根据恐惧强度调整频率
                    }
                }

                // 屏幕轻微脉动
                if (!VaultUtils.isServer) {
                    float pulse = (float)Math.Sin(heartbeatTimer) * 0.5f + 0.5f;
                    Main.screenPosition += new Vector2(
                        Main.rand.NextFloat(-0.5f, 0.5f),
                        Main.rand.NextFloat(-0.5f, 0.5f)
                    ) * pulse * fearIntensity;
                }
            } else {
                heartbeatTimer = 0f;
            }
        }

        /// <summary>
        /// 播放心跳音效
        /// </summary>
        private void PlayHeartbeatSound() {
            if (VaultUtils.isServer) return;

            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item8 with {
                Volume = 0.3f * fearIntensity,
                Pitch = -0.8f,
                MaxInstances = 1
            }, Player.Center);
        }

        /// <summary>
        /// 更新视野暗化
        /// </summary>
        private void UpdateVignette() {
            vignetteDarkness = MathHelper.Lerp(vignetteDarkness, fearIntensity * 0.4f, 0.05f);
        }

        /// <summary>
        /// 绘制屏幕效果
        /// </summary>
        public override void PostUpdate() {
            if (vignetteDarkness > 0.01f && !VaultUtils.isServer) {
                // 这个效果会在HUD绘制时应用
            }
        }

        /// <summary>
        /// 修改屏幕位置（用于恐惧抖动）
        /// </summary>
        public override void ModifyScreenPosition() {
            if (fearIntensity > 0.5f && nearestSpirit != null) {
                // 当厉鬼非常接近时，屏幕更剧烈抖动
                float shake = fearIntensity * 2f;
                Main.screenPosition += new Vector2(
                    Main.rand.NextFloat(-shake, shake),
                    Main.rand.NextFloat(-shake, shake)
                );
            }
        }
    }

    /// <summary>
    /// 深渊厉鬼屏幕效果绘制
    /// </summary>
    public class AbyssSpiritScreenEffects : ModSystem
    {
        public override void PostUpdateEverything() {
            // 这里可以添加全局的环境效果
        }
    }
}
