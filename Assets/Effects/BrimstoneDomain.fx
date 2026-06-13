// ============================================================================
// BrimstoneDomain.fx 硫磺火领域法阵
// 采样 s0 + s1 噪声；Immediate AlphaBlend 全屏 quad
// ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;
float fadeAlpha;        //整体透明度 0~1
float tierLevel;        //当前层级 0~3（连续值，用于平滑过渡）
float expandProgress;   //法阵展开进度 0~1
float pulseIntensity;   //脉冲强度控制

float3 coreColor;       //核心橙红色
float3 midColor;        //中层深红色
float3 edgeColor;       //边缘暗红色
float3 voidColor;       //虚空底色

#define PI 3.14159265
#define TAU 6.28318530

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.11369, 0.13787));
    p3 += dot(p3, p3.yzx + 19.19);
    return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
}

//简易值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f); //Hermite插值

    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//分形噪声fbm
float fbm(float2 p, int octaves)
{
    float value = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for (int i = 0; i < octaves; i++)
    {
        value += amp * valueNoise(p * freq);
        freq *= 2.17;
        amp *= 0.48;
        p += float2(1.7, 9.2);
    }
    return value;
}

//域扭曲fbm（更有机的火焰形态）
float warpedFbm(float2 p, float time)
{
    float2 q = float2(fbm(p + float2(0.0, 0.0), 4),
                      fbm(p + float2(5.2, 1.3), 4));
    float2 r = float2(fbm(p + 4.0 * q + float2(1.7, 9.2) + time * 0.15, 4),
                      fbm(p + 4.0 * q + float2(8.3, 2.8) + time * 0.12, 4));
    return fbm(p + 4.0 * r, 4);
}

// A. 硫火等离子漩涡
// 多层旋转火焰，带域扭曲的湍流效果
float3 brimstonePlasma(float2 centered, float dist, float angle, float time, float tier)
{
    float normAngle = (angle + PI) / TAU;

    //主火焰层：径向+切向旋转的湍流
    float2 fireUV1 = float2(normAngle * 3.0 + time * 0.35, dist * 2.5 - time * 0.2);
    float2 warp1 = tex2D(noiseSamp, frac(fireUV1)).rg * 0.4;
    float fire1 = tex2D(noiseSamp, frac(float2(
        normAngle * 5.0 + warp1.x + time * 0.25,
        dist * 3.0 + warp1.y - time * 0.18
    ))).r;

    //第二火焰层：反向旋转，不同频率
    float2 fireUV2 = float2(normAngle * 7.0 - time * 0.28, dist * 4.0 + time * 0.15);
    float2 warp2 = tex2D(noiseSamp, frac(fireUV2 + 0.37)).gb * 0.35;
    float fire2 = tex2D(noiseSamp, frac(float2(
        normAngle * 4.0 - warp2.x - time * 0.2,
        dist * 2.0 + warp2.y + time * 0.22
    ))).g;

    //深层岩浆流：缓慢，大尺度
    float2 magmaUV = float2(centered.x * 0.8 + time * 0.06, centered.y * 0.8 - time * 0.04);
    float magma = tex2D(noiseSamp, frac(magmaUV)).r;
    magma = smoothstep(0.3, 0.7, magma);

    //混合火焰 - 取最大值模拟火焰亮纹
    float fireMix = max(fire1 * 0.7, fire2 * 0.6);
    fireMix = pow(abs(fireMix), 1.5) * 1.8;

    //中心区域更强烈
    float centerIntensity = smoothstep(0.9, 0.0, dist);
    fireMix *= 0.3 + centerIntensity * 0.7;

    //火焰颜色渐变：核心亮→边缘暗
    float3 fireColor = lerp(edgeColor, coreColor, fireMix);
    fireColor = lerp(fireColor, midColor, magma * 0.4);

    //层级增强
    fireColor *= 0.4 + tier * 0.2;

    return fireColor * fireMix;
}

// B. 魔法阵环
// 多层同心圆环，带符文刻蚀纹理和脉冲辉光
float magicCircleRing(float dist, float angle, float ringRadius, float time,
    float rotSpeed, float runeCount, float runeSize)
{
    float normAngle = (angle + PI) / TAU;

    //主环线
    float ringDist = abs(dist - ringRadius);
    float ring = 1.0 - smoothstep(0.0, 0.008, ringDist);

    //柔和辉光
    float glow = exp(-ringDist * ringDist * 3000.0);

    //符文刻蚀：在环上等距排列的符文图案
    float runeAngle = frac(normAngle * runeCount + time * rotSpeed);
    float runeSlot = frac(runeAngle);

    //每个符文槽位的图案
    float runePattern = 0.0;

    //菱形符文
    float2 runeUV = float2((runeSlot - 0.5) * 2.0, (dist - ringRadius) / runeSize);
    float diamond = abs(runeUV.x) + abs(runeUV.y);
    runePattern += (1.0 - smoothstep(0.5, 0.55, diamond)) * step(diamond, 0.8);

    //符文内部纹路
    float innerLine = abs(runeUV.x) < 0.02 || abs(runeUV.y) < 0.02 ? 0.5 : 0.0;
    runePattern += innerLine * step(diamond, 0.5);

    //符文脉冲
    float runePulse = 0.7 + 0.3 * sin(time * 3.0 + floor(normAngle * runeCount) * 1.7);
    runePattern *= runePulse;

    //限制符文只在环附近显示
    float runeZone = 1.0 - smoothstep(0.0, runeSize, abs(dist - ringRadius));
    runePattern *= runeZone;

    return ring + glow * 0.6 + runePattern * 0.5;
}

// C. 五芒星/六芒星几何
// 程序化绘制旋转的魔法几何图形
float starGeometry(float2 centered, float dist, float angle, float radius,
    int points, float rotation, float thickness)
{
    float result = 0.0;

    //顶点连线
    for (int i = 0; i < points; i++)
    {
        //星形连线：每个顶点连接到跨越的顶点
        float a1 = rotation + TAU * (float)i / (float)points;
        int skip = points == 5 ? 2 : (points == 6 ? 2 : 1); //五芒星跨2，六芒星跨2
        float a2 = rotation + TAU * (float)((i + skip) % points) / (float)points;

        float2 p1 = float2(cos(a1), sin(a1)) * radius;
        float2 p2 = float2(cos(a2), sin(a2)) * radius;

        //点到线段距离
        float2 pa = centered - p1;
        float2 ba = p2 - p1;
        float h = saturate(dot(pa, ba) / dot(ba, ba));
        float lineDist = length(pa - ba * h);

        float lineGlow = exp(-lineDist * lineDist / (thickness * thickness * 0.5));
        float lineSharp = 1.0 - smoothstep(0.0, thickness * 0.5, lineDist);

        result += lineSharp * 0.6 + lineGlow * 0.3;
    }

    //顶点辉光
    for (int j = 0; j < points; j++)
    {
        float a = rotation + TAU * (float)j / (float)points;
        float2 vertex = float2(cos(a), sin(a)) * radius;
        float vDist = length(centered - vertex);
        result += exp(-vDist * vDist * 800.0) * 0.5;
    }

    return saturate(result);
}

// D. 符文光环带
// 在特定半径上显示旋转的古代符文序列
float runeArcBand(float dist, float angle, float bandRadius, float bandWidth,
    float time, float rotSpeed, float segCount)
{
    float normAngle = (angle + PI) / TAU;
    float rotAngle = frac(normAngle + time * rotSpeed);

    //带状区域遮罩
    float bandMask = 1.0 - smoothstep(0.0, bandWidth, abs(dist - bandRadius));
    if (bandMask < 0.001)
        return 0.0;

    //将圆弧分段，每段是一个"符文"
    float segAngle = frac(rotAngle * segCount);
    float segID = floor(rotAngle * segCount);

    //每个符文的独特图案（用hash生成伪随机图案）
    float h = hash11(segID * 7.13 + bandRadius * 3.7);

    //符文图案：组合水平/垂直线 + 圆弧
    float2 runeUV = float2((segAngle - 0.5) * 2.0, (dist - bandRadius) / bandWidth);

    float pattern = 0.0;

    //根据hash选择不同符文类型
    float type = frac(h * 7.0);
    if (type < 0.2)
    {
        //十字形
        pattern = (abs(runeUV.x) < 0.06 ? 1.0 : 0.0) + (abs(runeUV.y) < 0.06 ? 1.0 : 0.0);
        pattern = saturate(pattern);
    }
    else if (type < 0.4)
    {
        //圆形
        float r = length(runeUV) * 1.5;
        pattern = 1.0 - smoothstep(0.3, 0.35, abs(r - 0.5));
    }
    else if (type < 0.6)
    {
        //三角
        float tri = abs(runeUV.x) + runeUV.y * 0.5 + 0.25;
        pattern = 1.0 - smoothstep(0.3, 0.35, tri);
        pattern *= step(-0.4, runeUV.y);
    }
    else if (type < 0.8)
    {
        //竖线组
        float lines = sin(runeUV.x * 25.0);
        pattern = step(0.7, lines) * step(abs(runeUV.y), 0.35);
    }
    else
    {
        //菱形叠加
        float d1 = abs(runeUV.x) + abs(runeUV.y);
        float d2 = abs(runeUV.x * 0.7) + abs(runeUV.y * 1.3);
        pattern = (1.0 - smoothstep(0.3, 0.35, d1)) + (1.0 - smoothstep(0.2, 0.25, d2)) * 0.5;
        pattern = saturate(pattern);
    }

    //间隔：符文之间有空隙
    float gap = smoothstep(0.0, 0.08, segAngle) * smoothstep(1.0, 0.92, segAngle);
    pattern *= gap;

    //闪烁
    float flicker = 0.6 + 0.4 * sin(time * 2.5 + segID * 2.3 + bandRadius * 5.0);

    return pattern * bandMask * flicker;
}

// E. 硫火余烬上升效果
// 程序化上升的火星粒子
float risingEmbers(float2 centered, float time, float tier)
{
    float embers = 0.0;
    float count = 15.0 + tier * 10.0;

    for (int i = 0; i < 30; i++)
    {
        if ((float)i >= count)
            break;

        float id = (float)i;
        float h1 = hash11(id * 1.731);
        float h2 = hash11(id * 3.147);
        float h3 = hash11(id * 5.891);

        //径向位置
        float emAngle = h1 * TAU;
        float emDist = 0.15 + h2 * 0.65;

        //上升动画
        float speed = 0.3 + h3 * 0.5;
        float life = frac(time * speed + h1);

        //位置
        float2 emPos = float2(cos(emAngle), sin(emAngle)) * emDist;
        emPos.y -= life * 0.3; //上升
        emPos.x += sin(life * PI * 2.0 + h2 * TAU) * 0.05; //左右飘动

        float emR = length(centered - emPos);

        //大小随生命周期变化：出现→放大→缩小消失
        float emScale = sin(life * PI) * (0.008 + h3 * 0.005);
        float em = exp(-emR * emR / (emScale * emScale));

        //亮度随距中心距离衰减
        em *= smoothstep(0.85, 0.2, length(emPos));

        embers += em;
    }

    return saturate(embers);
}

// F. 暗焰脉冲波
// 从中心向外扩展的暗能量波纹
float darkPulseWave(float dist, float time, float tier)
{
    float waves = 0.0;
    float waveCount = 2.0 + tier;

    for (int i = 0; i < 5; i++)
    {
        if ((float)i >= waveCount)
            break;
        float phase = time * 0.8 + (float)i * 1.5;
        float wavePos = frac(phase) * 0.9;
        float waveFade = sin(frac(phase) * PI); //出现到消失的渐变
        float waveDist = abs(dist - wavePos);
        float wave = exp(-waveDist * waveDist * 2000.0) * waveFade;
        waves += wave;
    }

    return saturate(waves);
}

// 主像素着色器
float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + PI) / TAU;

    float tier = tierLevel;
    float time = uTime;
    float expand = expandProgress;

    // ======== 域边界裁剪 ========
    float edgeFade = 1.0 - smoothstep(0.82, 1.0, dist);
    if (edgeFade <= 0.001)
        return float4(0, 0, 0, 0);

    // ======== A. 硫火等离子底色 ========
    float3 plasma = brimstonePlasma(centered, dist, angle, time, tier);

    //底色暗红渐变（中心更暗更深邃，营造深渊感）
    float depthGrad = smoothstep(0.0, 0.85, dist);
    float3 baseColor = lerp(voidColor * 0.5, edgeColor * 0.3, depthGrad);

    // ======== B. 核心魔法阵环 ========
    float ringTotal = 0.0;

    //最内环 - 始终存在
    ringTotal += magicCircleRing(dist, angle, 0.18 * expand, time, 0.12, 8.0, 0.04);

    //第二环
    ringTotal += magicCircleRing(dist, angle, 0.32 * expand, time, -0.08, 12.0, 0.035) * 0.8;

    //第三环（tier >= 1）
    float t1 = smoothstep(0.5, 1.5, tier);
    ringTotal += magicCircleRing(dist, angle, 0.48 * expand, time, 0.06, 16.0, 0.03) * t1 * 0.7;

    //第四环（tier >= 2）
    float t2 = smoothstep(1.5, 2.5, tier);
    ringTotal += magicCircleRing(dist, angle, 0.62 * expand, time, -0.1, 20.0, 0.028) * t2 * 0.6;

    //最外环（tier >= 3）
    float t3 = smoothstep(2.5, 3.0, tier);
    ringTotal += magicCircleRing(dist, angle, 0.78 * expand, time, 0.04, 24.0, 0.025) * t3 * 0.5;

    ringTotal = saturate(ringTotal);

    // ======== C. 几何图形 ========
    float geomTotal = 0.0;

    //内层五芒星 - 始终旋转
    geomTotal += starGeometry(centered, dist, angle, 0.22 * expand, 5, time * 0.5, 0.006);

    //中层六芒星（tier >= 1）
    geomTotal += starGeometry(centered, dist, angle, 0.4 * expand, 6, -time * 0.35, 0.005) * t1;

    //外层五芒星（tier >= 2，反向旋转）
    geomTotal += starGeometry(centered, dist, angle, 0.55 * expand, 5, time * 0.25 + PI * 0.2, 0.005) * t2 * 0.8;

    //最外层八角形（tier >= 3）
    geomTotal += starGeometry(centered, dist, angle, 0.72 * expand, 8, -time * 0.15, 0.004) * t3 * 0.6;

    geomTotal = saturate(geomTotal);

    // ======== D. 符文光环带 ========
    float runeTotal = 0.0;

    //内圈符文
    runeTotal += runeArcBand(dist, angle, 0.25 * expand, 0.035, time, 0.15, 10.0) * 0.6;

    //中圈符文（tier >= 1）
    runeTotal += runeArcBand(dist, angle, 0.44 * expand, 0.03, time, -0.1, 14.0) * t1 * 0.5;

    //外圈符文（tier >= 2）
    runeTotal += runeArcBand(dist, angle, 0.58 * expand, 0.028, time, 0.07, 18.0) * t2 * 0.4;

    runeTotal = saturate(runeTotal);

    // ======== E. 暗焰脉冲波 ========
    float pulseWave = darkPulseWave(dist, time, tier) * pulseIntensity;

    // ======== F. 硫火余烬 ========
    float embers = risingEmbers(centered, time, tier) * (0.5 + tier * 0.16);

    // ======== G. 核心漩涡 ========
    //中心的黑暗漩涡（深渊之眼）
    float vortexDist = smoothstep(0.18, 0.0, dist);
    float vortexSwirl = sin(angle * 3.0 + time * 4.0 + dist * 20.0) * 0.5 + 0.5;
    float2 vortexUV = float2(normAngle * 2.0 + time * 0.5, dist * 5.0);
    float vortexNoise = tex2D(noiseSamp, frac(vortexUV)).r;
    float vortex = vortexDist * (0.5 + vortexSwirl * 0.3 + vortexNoise * 0.2);

    // ======== H. 电弧效果 ========
    //在环之间产生的闪电电弧
    float arcEffect = 0.0;
    if (tier >= 1.0)
    {
        float2 arcUV = float2(normAngle * 20.0 + time * 3.0, dist * 8.0);
        float arcNoise = tex2D(noiseSamp, frac(arcUV)).r;
        float arcLine = pow(arcNoise, 8.0) * 3.0;

        //限制在环之间的区域
        float arcZone = 0.0;
        arcZone += smoothstep(0.0, 0.03, abs(dist - 0.25 * expand))
                 * (1.0 - smoothstep(0.0, 0.03, abs(dist - 0.35 * expand)));
        arcZone += smoothstep(0.0, 0.03, abs(dist - 0.35 * expand))
                 * (1.0 - smoothstep(0.0, 0.03, abs(dist - 0.48 * expand))) * t1;

        arcEffect = arcLine * saturate(arcZone) * 0.4 * min(tier, 2.0);

        //随机闪烁
        float arcFlicker = step(0.85, hash21(float2(floor(normAngle * 30.0), floor(time * 12.0))));
        arcEffect *= 0.3 + arcFlicker * 0.7;
    }

    // ======== I. 外层暗能量涌动 ========
    float2 darkFlowUV = float2(
        normAngle * 3.0 + time * 0.18,
        dist * 2.0 - time * 0.12
    );
    float darkFlow = tex2D(noiseSamp, frac(darkFlowUV)).r;
    float2 darkFlowUV2 = float2(
        normAngle * 5.0 - time * 0.14,
        dist * 3.0 + time * 0.1
    );
    float darkFlow2 = tex2D(noiseSamp, frac(darkFlowUV2)).g;
    float outerDark = (darkFlow + darkFlow2) * 0.5;
    outerDark = smoothstep(0.3, 0.7, outerDark) * 0.2;
    outerDark *= smoothstep(0.4, 0.85, dist); //仅外层显示

    // =
    // 颜色合成
    // =
    float3 finalColor = baseColor;

    //底层等离子火焰
    finalColor += plasma * 0.6;

    //外层暗能量
    finalColor += voidColor * outerDark;

    //魔法阵环（硫磺火色调）
    float3 ringColor = lerp(midColor, coreColor, ringTotal * 0.6);
    finalColor += ringColor * ringTotal * (0.7 + pulseIntensity * 0.3);

    //几何图形（更亮的核心色）
    float3 geomColor = lerp(coreColor, float3(1.0, 0.85, 0.6), geomTotal * 0.3);
    finalColor += geomColor * geomTotal * 0.6;

    //符文带（中间色调）
    float3 runeColor = lerp(midColor, coreColor, 0.5);
    finalColor += runeColor * runeTotal * 0.5;

    //暗焰脉冲
    float3 pulseColor = lerp(edgeColor, coreColor, 0.7);
    finalColor += pulseColor * pulseWave * 0.8;

    //余烬粒子（最亮的橙红）
    float3 emberColor = lerp(coreColor, float3(1.0, 0.9, 0.5), 0.4);
    finalColor += emberColor * embers;

    //核心漩涡（深红到亮橙渐变）
    float3 vortexColor = lerp(voidColor, coreColor, vortex);
    finalColor += vortexColor * vortex * 1.5;

    //电弧（最亮）
    float3 arcColor = lerp(coreColor, float3(1.0, 0.95, 0.8), 0.5);
    finalColor += arcColor * arcEffect;

    // ======== 透明度合成 ========
    float alpha = 0.0;

    //基础填充
    float fillAlpha = lerp(0.08, 0.25, smoothstep(0.8, 0.0, dist));
    fillAlpha += length(plasma) * 0.3;
    alpha += fillAlpha;

    //结构元素
    alpha += ringTotal * 0.6;
    alpha += geomTotal * 0.5;
    alpha += runeTotal * 0.4;
    alpha += pulseWave * 0.5;
    alpha += embers * 0.7;
    alpha += vortex * 0.8;
    alpha += arcEffect * 0.6;
    alpha += outerDark * 0.3;

    alpha = saturate(alpha);
    alpha *= edgeFade * fadeAlpha * expand;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass BrimstoneDomainPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
