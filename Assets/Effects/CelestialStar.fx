// ============================================================================
//CelestialStar.fx 启示录天体
//CelestialBody/ImpactFlare 两 Technique；s0+s1 ps_3_0
// ============================================================================

sampler baseSamp : register(s0);

texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture   = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

float uTime;         //全局时间
float fadeAlpha;      //整体透明度
float fallSpeed;      //下落速度标量，用于拖尾强度

//色彩参数
float3 coreColor;    //核心色（接近白热）
float3 surfaceColor; //表面色（金色）
float3 coronaColor;  //日冕色（橘红）
float3 trailColor;   //尾焰色

//天体参数
float sphereRadius;  //球体在UV空间的半径（约0.15~0.25）
float coronaWidth;   //日冕宽度
float intensity;     //整体亮度乘数

//冲击波参数
float impactProgress; //冲击进度0~1
float impactRadius;   //最终冲击半径

#define PI  3.14159265
#define TAU 6.28318530

struct PSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

//==============================
//工具函数
//==============================

//柔和圆环SDF
float softRing(float d, float r, float sharpness)
{
    float delta = d - r;
    return exp(-sharpness * delta * delta);
}

//简单3x3值噪声叠加
float fbmNoise(float2 uv, float time)
{
    float v = 0.0;
    v += tex2D(noiseSamp, uv * 2.0 + float2(time * 0.07, time * 0.03)).r * 0.5;
    v += tex2D(noiseSamp, uv * 4.5 + float2(-time * 0.05, time * 0.08)).g * 0.3;
    v += tex2D(noiseSamp, uv * 9.0 + float2(time * 0.11, -time * 0.06)).b * 0.2;
    return v;
}

//==============================
//CelestialBody 天体球体主渲染
//==============================
float4 CelestialBodyPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 center = float2(0.5, 0.5);
    float2 toCenter = uv - center;
    float dist = length(toCenter);
    float angle = atan2(toCenter.y, toCenter.x);

    float sR = sphereRadius;
    float3 col = 0;
    float alpha = 0;

    //=========================================
    //第一层：尾焰拖尾（在球体之下绘制）
    //=========================================
    {
        //拖尾区域在球体上方，向上延伸
        float2 trailUV = toCenter;
        //垂直偏移使尾焰从球体顶部开始
        float trailY = -trailUV.y - sR * 0.5;
        float trailX = abs(trailUV.x);

        if (trailY > 0)
        {
            //尾焰宽度随距离衰减
            float trailWidth = sR * (0.5 + 0.3 * exp(-trailY * 4.0));
            float trailMask = exp(-trailX * trailX / (trailWidth * trailWidth * 0.5));
            //纵向衰减
            float heightFade = exp(-trailY * trailY * 8.0);
            //速度影响拖尾长度
            float speedMul = saturate(fallSpeed * 0.05);
            heightFade = exp(-trailY * trailY * (12.0 - speedMul * 8.0));

            //噪声扰动使尾焰不均匀
            float tn = fbmNoise(float2(trailUV.x * 3.0 + uTime * 0.5, trailY * 2.0 - uTime * 1.5), uTime);
            tn = tn * 0.6 + 0.4;

            float trailAlpha = trailMask * heightFade * tn * speedMul;

            //颜色从接近表面的金色过渡到远离的淡橙
            float3 tc = lerp(surfaceColor, trailColor, saturate(trailY * 3.0));
            //加入微弱的热白内核
            tc = lerp(coreColor, tc, saturate(trailX / (trailWidth * 0.3) + trailY * 2.0));

            col += tc * trailAlpha * intensity * 0.6;
            alpha += trailAlpha * 0.4;
        }
    }

    //=========================================
    //第二层：日冕大气层（球体边界外的发光）
    //=========================================
    {
        float coronaStart = sR;
        float coronaEnd = sR + coronaWidth;

        if (dist > sR * 0.7 && dist < coronaEnd * 1.5)
        {
            //日冕径向衰减
            float coronaDist = max(dist - sR * 0.85, 0.0);
            float coronaFade = exp(-coronaDist * coronaDist / (coronaWidth * coronaWidth * 0.25));

            //角度调制：模拟不均匀的日冕结构
            float angMod1 = sin(angle * 5.0 + uTime * 0.8) * 0.3 + 0.7;
            float angMod2 = sin(angle * 13.0 - uTime * 1.2 + dist * 20.0) * 0.15 + 0.85;
            //噪声驱动的日冕射线
            float2 coronaNUV = float2(angle / TAU + uTime * 0.05, dist * 3.0 - uTime * 0.2);
            float coronaNoise = tex2D(noiseSamp, frac(coronaNUV) * 3.0).r;
            coronaNoise = coronaNoise * 0.5 + 0.5;

            float coronaAlpha = coronaFade * angMod1 * angMod2 * coronaNoise;

            //日冕色：近球面偏金色，远端偏红
            float3 cc = lerp(surfaceColor, coronaColor, saturate(coronaDist / coronaWidth));
            //加入一点热白（靠近球面的部分）
            float hotFactor = exp(-coronaDist * 30.0);
            cc = lerp(cc, coreColor, hotFactor * 0.3);

            col += cc * coronaAlpha * intensity * 0.7;
            alpha += coronaAlpha * 0.35;
        }
    }

    //=========================================
    //第三层：球体主体（伪3D发光天体）
    //=========================================
    if (dist < sR * 1.05)
    {
        //归一化距离：0=中心，1=球面边缘
        float normD = saturate(dist / sR);

        //球面Z深度（模拟3D）
        float sphereZ = sqrt(max(1.0 - normD * normD, 0.0));

        //球体遮罩（边缘柔化）
        float sphereMask = smoothstep(1.05, 0.92, normD);

        //=== 伪3D光照 ===
        //光源在左上方偏移，制造明暗面
        float2 lightDir = normalize(float2(-0.4, -0.5));
        float lightDot = dot(normalize(toCenter), lightDir);
        //半球面光照：暗面不会完全黑，保持发光体质感
        float surfLight = 0.65 + lightDot * 0.35;

        //临边增亮：恒星大气效应，视线切球面时路径最长
        float limbBright = 1.0 + (1.0 - sphereZ) * 0.8;

        //=== 差分旋转等离子体表面 ===
        //三层以不同速度旋转的噪声
        float rA1 = angle + uTime * 0.6;
        float rA2 = angle + uTime * 1.1 + 2.094;
        float rA3 = angle + uTime * 0.3 + 4.189;

        //球面映射UV（经纬度投影）
        float2 sphUV1 = float2(rA1 / TAU, normD);
        float2 sphUV2 = float2(rA2 / TAU, normD * 0.8);
        float2 sphUV3 = float2(rA3 / TAU, normD * 1.3);

        float p1 = tex2D(noiseSamp, frac(sphUV1 * 3.5 + float2(uTime * 0.04, 0))).r;
        float p2 = tex2D(noiseSamp, frac(sphUV2 * 5.0 + float2(0, uTime * 0.05))).g;
        float p3 = tex2D(noiseSamp, frac(sphUV3 * 2.5 + float2(uTime * 0.03, uTime * 0.02))).b;

        //等离子体混合（保持高基底亮度）
        float plasma = p1 * 0.4 + p2 * 0.35 + p3 * 0.25;
        plasma = plasma * 0.35 + 0.65;

        //=== 表面对流胞纹理（米粒结构） ===
        float2 detailUV = float2(angle / TAU + uTime * 0.15, normD);
        float detail = tex2D(noiseSamp, frac(detailUV * 7.0 + float2(uTime * 0.06, -uTime * 0.03))).r;
        float cellPattern = detail * 0.3 + 0.7;

        //=== 温度色彩梯度 ===
        float3 bodyCol;
        if (normD < 0.3)
        {
            //核心：白热
            bodyCol = lerp(coreColor, surfaceColor * 1.1, normD / 0.3);
        }
        else if (normD < 0.7)
        {
            //中层：金色
            bodyCol = lerp(surfaceColor * 1.1, surfaceColor * 0.85, (normD - 0.3) / 0.4);
        }
        else
        {
            //边缘：偏向日冕色
            bodyCol = lerp(surfaceColor * 0.85, coronaColor * 0.9, (normD - 0.7) / 0.3);
        }

        //=== 能量脉动 ===
        float pulse = sin(uTime * 2.5 + normD * 8.0) * 0.08 + 0.92;

        //=== 组合球体最终颜色 ===
        float lit = surfLight * limbBright;
        float baseBright = sphereMask * plasma * cellPattern * pulse;

        float3 bodyFinal = bodyCol * baseBright * lit * intensity;

        //核心白热辉光（中心区域特别亮）
        float coreGlow = pow(max(1.0 - normD * 2.0, 0.0), 2.5);
        bodyFinal += coreColor * coreGlow * intensity * 0.6;

        //表面热斑闪烁
        float hotspot = p1 * p2;
        hotspot = hotspot * hotspot;
        bodyFinal += surfaceColor * hotspot * sphereMask * intensity * 0.2;

        col += bodyFinal;
        alpha += saturate(baseBright * lit * 1.5 + coreGlow * 0.5);
    }

    //=========================================
    //第四层：外围散射微光（远距离大气辉光）
    //=========================================
    {
        float farGlow = exp(-dist * dist * 6.0) * 0.08;
        col += surfaceColor * farGlow * intensity;
        alpha += farGlow * 0.2;
    }

    alpha = saturate(alpha) * fadeAlpha;
    return float4(col * fadeAlpha, alpha) * input.Color;
}

//==============================
//ImpactFlare 着地冲击闪光
//==============================
float4 ImpactFlarePS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 center = float2(0.5, 0.5);
    float2 toCenter = uv - center;
    float dist = length(toCenter);
    float angle = atan2(toCenter.y, toCenter.x);

    float prog = impactProgress;
    float3 col = 0;
    float alpha = 0;

    //冲击环：向外扩展的光环
    float ringR = prog * 0.45;
    float ringW = 0.02 + prog * 0.03;
    float ring = softRing(dist, ringR, 1.0 / (ringW * ringW));

    //环的衰减
    float ringFade = 1.0 - prog;
    ringFade = ringFade * ringFade;

    //噪声扰动环的形状
    float2 rnUV = float2(angle / TAU + uTime * 0.3, dist * 4.0);
    float rn = tex2D(noiseSamp, frac(rnUV)).r * 0.3 + 0.7;

    float ringAlpha = ring * ringFade * rn;
    float3 ringCol = lerp(coreColor, surfaceColor, prog);
    col += ringCol * ringAlpha * intensity;
    alpha += ringAlpha * 0.8;

    //中心残留辉光
    float centerGlow = exp(-dist * dist * 60.0) * (1.0 - prog * 0.8);
    col += coreColor * centerGlow * intensity * 0.5;
    alpha += centerGlow * 0.5;

    //径向射线爆发
    float rays = sin(angle * 8.0 + prog * 5.0) + sin(angle * 13.0 - prog * 3.0);
    rays = max(rays, 0.0) * 0.5;
    float rayFade = exp(-dist * 5.0) * (1.0 - prog);
    col += surfaceColor * rays * rayFade * intensity * 0.3;
    alpha += rays * rayFade * 0.15;

    //地面水平扩散光（下半部分强化）
    float groundSpread = max(toCenter.y, 0.0) * 2.0;
    float groundGlow = exp(-dist * 4.0) * groundSpread * (1.0 - prog * 0.7);
    col += coronaColor * groundGlow * intensity * 0.2;
    alpha += groundGlow * 0.1;

    alpha = saturate(alpha) * fadeAlpha;
    return float4(col * fadeAlpha, alpha) * input.Color;
}

//==============================
//Techniques
//==============================
technique CelestialBody
{
    pass P0
    {
        PixelShader = compile ps_3_0 CelestialBodyPS();
    }
}

technique ImpactFlare
{
    pass P0
    {
        PixelShader = compile ps_3_0 ImpactFlarePS();
    }
}
