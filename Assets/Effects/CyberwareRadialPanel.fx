// ============================================================================
// CyberwareRadialPanel.fx 义体技能雷达专属背板（v2 — 舒缓 HUD 美学）
//
// 设计转向（与 v1 的对比）：
//   v1：六边形栅格 + 旋转辐条 + 周期拨码刻度 —— 强"数据网格"感，过于接近
//      HackRamArc 的语言，玩家会误以为这是 RAM 的某种延伸 HUD
//   v2：参考 SHPCCyberPanel 的"舒缓 HUD"思路 —— 用 FBM 软噪铺底、平滑的同心
//      光晕、轻柔的脉冲环；中心采用"亮度梯度 iris"代替机械化的旋转辐条；
//      外圈用平滑光晕环取代刻度。整体追求"接口面板"的克制感，而不是
//      "数据芯片"的密集信息感
//
// 渲染范围：本 shader 只画"框架与底纹"——
//   - 整个雷达圆盘的 FBM 软底（极低 alpha，作大气感铺垫）
//   - 内/外缘的双层柔和发光环
//   - 中心 iris：径向亮度梯度 + 三层向外扩散的脉冲环（无旋转辐条！）
//   - 外圈大气光晕：缓慢周向波浪，提示"装置正在通电"，但不出现任何刻度
//   - 入场扩散动画：从圆心向外揭开
// 扇区填充、悬停高亮、图标、状态文字、悬停信息面板等动态内容继续由 CPU
// 在本 shader 绘制完成之后叠加。
//
// 参数说明：
//   uResolution     绘制 quad 的像素尺寸
//   uCenter         雷达圆心在 quad 内的像素坐标
//   uInnerR         扇区内弧半径
//   uOuterR         扇区外弧半径
//   uDeadZoneR      中心死区半径（iris 直径基准）
//   uDecoOuterR     外圈大气光晕的最大半径
//   uTime           动画驱动时间（秒）
//   uAlpha          全局 alpha
//   uOpenProgress   展开进度（0~1），驱动入场动画
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uOpenProgress;
float2 uResolution;
float2 uCenter;
float uInnerR;
float uOuterR;
float uDeadZoneR;
float uDecoOuterR;

//------------------ 工具函数 ------------------

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//4 octave FBM，平滑铺底用
float fbm(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * vnoise(p);
        p *= 2.07;
        a *= 0.5;
    }
    return v;
}

//角度归一到 [-pi, pi]
float wrapPi(float a) {
    a = fmod(a + 3.14159265, 6.28318530);
    if (a < 0) a += 6.28318530;
    return a - 3.14159265;
}

//------------------ 主像素着色 ------------------

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vcol : COLOR0) : COLOR0
{
    float2 p = uv * uResolution;
    float2 d = p - uCenter;
    float r = length(d);
    float ang = atan2(d.y, d.x);

    //入场进度：三次 ease-out 让圆盘"从中心绽放"
    float ease = 1.0 - pow(1.0 - saturate(uOpenProgress), 3.0);
    //归一化半径，外圈装饰半径作为分母
    float rNorm = r / max(uDecoOuterR, 1.0);

    float3 outCol = float3(0.0, 0.0, 0.0);
    float outA = 0.0;

    //==================================================
    // 1) FBM 大气底纹：填充整个雷达活动圈
    //    选用低饱和度的青蓝色，叠加缓慢漂移的两层噪声，形成"接口板内部
    //    电流余晖"的氛围感。alpha 故意压低，让上层 CPU 扇区清晰可读
    //==================================================
    if (r < uDecoOuterR + 6.0) {
        //把局部坐标转换到 FBM 的输入空间
        float2 nuv = d * 0.012;
        float n1 = fbm(nuv + float2(uTime * 0.06, 0.0));
        float n2 = fbm(nuv * 2.1 + float2(-uTime * 0.04, uTime * 0.05));
        //两层 FBM 用乘性方式组合，比相加更能拉出"层次感"
        float atmo = saturate(n1 * 0.65 + n2 * 0.55 - 0.18);

        //大气只在外圈范围出现，内圈交给 iris 处理
        float atmoMask = smoothstep(uInnerR - 4.0, uOuterR - 2.0, r)
                      * (1.0 - smoothstep(uOuterR + 1.0, uDecoOuterR + 4.0, r));

        //深色青底 -> 略亮的青色高光，渐变受 FBM 调制
        float3 base = float3(0.022, 0.064, 0.090);
        float3 accent = float3(0.034, 0.140, 0.180);
        float3 atmoCol = lerp(base, accent, atmo);
        //朝中心方向轻微变亮，强化"接口被点亮"的方向感
        atmoCol *= 0.85 + 0.25 * (1.0 - smoothstep(uInnerR, uOuterR, r));

        outCol += atmoCol * atmoMask;
        outA = max(outA, atmoMask * (0.36 + atmo * 0.10));
    }

    //==================================================
    // 2) 内弧柔和光带：环本体的内边缘羽化光环
    //    不画硬线，改用高斯衰减让"内圈口径"自然过渡到中心 iris
    //==================================================
    {
        float innerD = abs(r - uInnerR);
        float innerHalo = exp(-pow(innerD / 2.4, 2.0));
        float3 innerCol = float3(0.18, 0.66, 0.84);
        outCol += innerCol * innerHalo * 0.62;
        outA = max(outA, innerHalo * 0.62);

        //内侧延伸的二级柔光，让光由内弧渐进消失，避免一刀切
        float innerSpread = (1.0 - smoothstep(uInnerR - 14.0, uInnerR, r))
                         * smoothstep(uInnerR - 16.0, uInnerR - 6.0, r);
        outCol += float3(0.08, 0.32, 0.42) * innerSpread * 0.40;
        outA = max(outA, innerSpread * 0.30);
    }

    //==================================================
    // 3) 外弧柔和光带：环本体的外边缘羽化光环
    //==================================================
    {
        float outerD = abs(r - uOuterR);
        float outerHalo = exp(-pow(outerD / 2.0, 2.0));
        float3 outerCol = float3(0.16, 0.60, 0.78);
        outCol += outerCol * outerHalo * 0.68;
        outA = max(outA, outerHalo * 0.68);

        //外侧延伸的二级柔光，向 uDecoOuterR 方向平滑收尾
        float outerSpread = smoothstep(uOuterR, uOuterR + 14.0, r)
                         * (1.0 - smoothstep(uOuterR + 4.0, uDecoOuterR, r));
        outCol += float3(0.08, 0.30, 0.40) * outerSpread * 0.35;
        outA = max(outA, outerSpread * 0.25);
    }

    //==================================================
    // 4) 中心 iris：径向亮度梯度 + 三层向外扩散的脉冲环
    //    严格避开 v1 的旋转辐条 —— 那种放射状结构非常"机械数据感"，
    //    本版改为"光圈呼吸 + 脉冲扩散"，更接近接口张开/闭合的有机质感
    //==================================================
    if (r < uDeadZoneR * 1.18) {
        float irisT = saturate(r / uDeadZoneR);
        //核心柔光：在死区中心 1/3 范围内最亮，往边缘平滑衰减
        float core = 1.0 - smoothstep(0.0, 1.0, irisT);
        //FBM 调制让 iris 内部有微弱"涡动"的灵动感
        float irisN = fbm(d * 0.05 + float2(uTime * 0.10, -uTime * 0.07));
        float3 irisCol = float3(0.06, 0.32, 0.46) * core * (0.65 + irisN * 0.55);
        outCol += irisCol;
        outA = max(outA, core * 0.62);

        //中心明亮高光点，呼吸节奏由 sin(uTime * 2.8) 驱动
        float breath = 0.85 + 0.15 * sin(uTime * 2.8);
        float dotMask = exp(-r / (5.5 * breath));
        outCol += float3(0.45, 0.95, 1.00) * dotMask * 0.85;
        outA = max(outA, dotMask * 0.85);

        //三层脉冲环：以稳定周期从中心向 deadZone 边缘扩散
        //各层错开 1/3 相位，形成连续的"接口在采集 / 释放数据"的感观
        [unroll]
        for (int i = 0; i < 3; i++) {
            float iF = (float)i;
            //每个环的"年龄"，0 = 刚生成（在中心），1 = 即将消失（到达 deadZone 边缘）
            float ringT = frac(uTime * 0.32 + iF * 0.333);
            //环的当前半径
            float ringR = ringT * uDeadZoneR;
            //环的厚度随年龄缩薄，强化"扩散后消散"的物理感
            float thickness = lerp(2.4, 4.5, ringT);
            float ringFall = exp(-pow((r - ringR) / thickness, 2.0));
            //年龄越大越透明，避免最外圈"硬切"
            float ringFade = (1.0 - ringT) * (1.0 - smoothstep(0.85, 1.0, ringT));
            outCol += float3(0.22, 0.78, 0.92) * ringFall * ringFade * 0.45;
            outA = max(outA, ringFall * ringFade * 0.45);
        }

        //死区边缘的细环：和"内弧光带"无缝衔接，让 iris 与扇区在视觉上属于同一接口
        float edgeD = abs(r - uDeadZoneR);
        float edgeRing = exp(-pow(edgeD / 1.5, 2.0));
        outCol += float3(0.16, 0.58, 0.72) * edgeRing * 0.50;
        outA = max(outA, edgeRing * 0.50);
    }

    //==================================================
    // 5) 外圈大气光晕：取代 v1 的旋转拨码刻度
    //    用平滑的周向波形扰动一层薄薄的环带 alpha，
    //    让外圈像"装置外壳上轻轻流动的电场"，而非机械刻度
    //==================================================
    if (r > uOuterR && r < uDecoOuterR + 4.0) {
        float haloMid = (uOuterR + uDecoOuterR) * 0.5;
        float haloHalf = (uDecoOuterR - uOuterR) * 0.5;
        float haloD = abs(r - haloMid);
        //柔和带状高斯衰减
        float haloMask = exp(-pow(haloD / max(haloHalf, 1.0), 2.0));

        //周向缓慢起伏：用低频正弦组合让强度沿角度自然变化
        float swirl = (sin(ang * 3.0 - uTime * 0.32) * 0.5 + 0.5)
                    * (sin(ang * 5.0 + uTime * 0.21 + 1.7) * 0.4 + 0.6);
        //叠加一层 FBM 微扰，避免周向变化看起来"过于规则"
        float swirlNoise = vnoise(float2(ang * 6.0 + uTime * 0.5, r * 0.03));
        swirl = saturate(swirl * (0.7 + swirlNoise * 0.5));

        float3 haloCol = float3(0.10, 0.40, 0.52);
        outCol += haloCol * haloMask * (0.40 + swirl * 0.45);
        outA = max(outA, haloMask * (0.32 + swirl * 0.30));

        //外缘细环：标定"雷达活动范围最外缘"，不画刻度，仅一层平滑亮线
        float outerLineD = abs(r - uDecoOuterR);
        float outerLine = exp(-pow(outerLineD / 1.2, 2.0));
        outCol += float3(0.20, 0.66, 0.80) * outerLine * 0.55;
        outA = max(outA, outerLine * 0.50);
    }

    //==================================================
    // 6) 入场扩散动画：以圆心向外揭开
    //    通过 ease 控制可见半径阈值，未到阈值的像素被抹掉 alpha，
    //    让整圈背板呈现"自圆心向外绽放"的入场感
    //==================================================
    {
        float reveal = smoothstep(ease - 0.18, ease + 0.05, rNorm);
        outA *= lerp(1.0, 0.0, reveal);
    }

    //==================================================
    // 输出（预乘 alpha 以匹配 AlphaBlend，与 HackRamArc / CyberDomainPanel 一致）
    //==================================================
    float finalA = outA * uAlpha;
    return float4(outCol * finalA, finalA);
}

technique Technique1
{
    pass CyberwareRadialPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
