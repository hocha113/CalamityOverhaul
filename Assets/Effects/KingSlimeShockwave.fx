// ============================================================================
// KingSlimeShockwave.fx 史莱姆王落地冲击波
// Additive 预乘 alpha
// ============================================================================

float uTime;
float ringProgress;    //0~1 扩散进度
float fadeAlpha;       //整体透明度
float pulseIntensity;  //脉冲强度
float3 coreColor;      //内核颜色（皇冠金）
float3 midColor;       //中层颜色（皇室紫）
float3 edgeColor;      //边缘颜色（深邃蓝紫）

sampler uNoiseTex : register(s1);

struct VSOutput {
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

float hash(float2 p) {
    float h = dot(p, float2(127.1, 311.7));
    return frac(sin(h) * 43758.5453);
}

float noise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + float2(1.0, 0.0));
    float c = hash(i + float2(0.0, 1.0));
    float d = hash(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p, int octaves) {
    float val = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for (int i = 0; i < octaves; i++) {
        val += amp * noise(p * freq);
        amp *= 0.5;
        freq *= 2.0;
    }
    return val;
}

float4 PSShockwave(VSOutput input) : COLOR0 {
    float2 uv = input.UV;
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);

    //极坐标噪声：模拟皇室凝胶被压扁的弯曲圈纹
    float2 polarUV = float2(angle / 6.2832 + 0.5, dist * 2.0);
    float noiseSample = tex2D(uNoiseTex, polarUV * 4.0 + float2(uTime * 0.25, -uTime * 0.6)).r;

    float progress = ringProgress;
    float invProgress = 1.0 - progress;

    //========== (A) 主冲击环 ==========
    //凝胶环更平、更宽，体现"果冻波纹"
    float ringRadius = progress * 0.42;
    float ringWidth  = 0.06 + progress * 0.05;

    //噪声让环呈现波动起伏，更像果冻
    float disp = (noiseSample - 0.5) * 0.045 * (1.0 + progress * 1.6);
    float ringDist = abs(dist - ringRadius + disp);

    float ringIntensity = smoothstep(ringWidth, 0.0, ringDist);
    ringIntensity *= pow(invProgress, 0.85);

    //环的颜色从中心金到外缘蓝紫
    float ringColorMix = smoothstep(0.0, ringWidth, ringDist);
    float3 ringColor = lerp(coreColor, midColor, ringColorMix);

    //========== (B) 内部凝胶残波 ==========
    //扩散后内圈留下淡淡的紫蓝凝胶余波
    float innerZone = smoothstep(ringRadius + 0.02, ringRadius * 0.25, dist);

    //极向流动的果冻噪声
    float2 gelUV = float2(
        angle / 6.2832 + 0.5 + uTime * 0.30,
        dist * 5.0 - uTime * 1.2
    );
    float gelFbm = fbm(gelUV * 2.6, 4);

    float gelIntensity = innerZone * gelFbm * invProgress * 0.55;

    float radialGrad = 1.0 - dist / max(ringRadius + 0.01, 0.01);
    radialGrad = saturate(radialGrad);
    float3 gelColor = lerp(edgeColor, midColor, pow(abs(radialGrad), 1.4));

    //========== (C) 金色皇冠裂纹 ==========
    //从中心向外辐射几道金色光柱，体现"皇室之力"
    float crackCount = 6.0;
    float crackAngle = frac(angle / 6.2832 * crackCount + hash(floor(angle / 6.2832 * crackCount)) * 0.5);
    float crackSharp = smoothstep(0.06, 0.0, abs(crackAngle - 0.5));

    float crackMask = smoothstep(ringRadius + 0.04, ringRadius * 0.15, dist);
    float crackIntensity = crackSharp * crackMask * invProgress * 0.7;

    float3 crackColor = coreColor * 1.6;

    //========== (D) 外晕余波 ==========
    float outerWave = smoothstep(ringRadius, ringRadius + 0.12, dist)
                    * smoothstep(ringRadius + 0.26, ringRadius + 0.10, dist);

    float outerNoise = fbm(float2(angle * 2.0, dist * 5.0 - uTime) * 1.8, 3);
    float outerIntensity = outerWave * outerNoise * invProgress * 0.35;

    float3 outerCol = edgeColor * 0.6;

    //========== (E) 中心闪光 ==========
    float coreBurst = exp(-dist * 22.0) * invProgress * invProgress;
    float3 burstColor = coreColor * 2.2;

    //========== (F) 脉冲明暗 ==========
    float pulse = 1.0 + sin(uTime * 22.0 - dist * 32.0) * pulseIntensity * 0.18 * invProgress;

    //合成
    float3 finalColor = float3(0, 0, 0);
    finalColor += ringColor * ringIntensity * 1.55;
    finalColor += gelColor * gelIntensity;
    finalColor += crackColor * crackIntensity;
    finalColor += outerCol * outerIntensity;
    finalColor += burstColor * coreBurst;

    finalColor *= pulse;

    float totalAlpha = saturate(ringIntensity + gelIntensity + crackIntensity * 0.5 + outerIntensity + coreBurst);
    totalAlpha *= fadeAlpha;

    //圆形边界柔化
    float edgeFade = smoothstep(0.5, 0.42, dist);
    totalAlpha *= edgeFade;

    return float4(finalColor * totalAlpha, totalAlpha);
}

technique KingSlimeShockwavePass {
    pass P0 {
        PixelShader = compile ps_3_0 PSShockwave();
    }
}
