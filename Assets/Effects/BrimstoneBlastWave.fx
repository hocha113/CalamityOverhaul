// ============================================================================
// BrimstoneBlastWave.fx 硫磺火爆炸冲击波
// UV(0.5,0.5)为爆心；正方形画布
// ============================================================================
float uTime;
float ringProgress;    //0~1 扩散进度
float fadeAlpha;       //整体透明度
float pulseIntensity;  //脉冲强度
float3 coreColor;      //内核颜色
float3 midColor;       //中层颜色
float3 edgeColor;      //边缘颜色

sampler uNoiseTex : register(s1);

struct VSOutput {
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

//简化噪声哈希
float hash(float2 p) {
    float h = dot(p, float2(127.1, 311.7));
    return frac(sin(h) * 43758.5453);
}

//值噪声
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

//分形噪声
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

float4 PSBlastWave(VSOutput input) : COLOR0 {
    float2 uv = input.UV;
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);

    //极坐标噪声采样
    float2 polarUV = float2(angle / 6.2832 + 0.5, dist * 2.0);
    float noiseSample = tex2D(uNoiseTex, polarUV * 3.0 + float2(uTime * 0.3, -uTime * 0.8)).r;

    float progress = ringProgress;
    float invProgress = 1.0 - progress;

    //========== (A) 冲击波环 ==========
    //环的当前半径随progress扩大
    float ringRadius = progress * 0.45;
    float ringWidth = 0.04 + progress * 0.06;

    //环的距离场
    float ringDist = abs(dist - ringRadius);
    //噪声扭曲环形
    float noiseDisplace = (noiseSample - 0.5) * 0.03 * (1.0 + progress * 2.0);
    ringDist = abs(dist - ringRadius + noiseDisplace);

    float ringIntensity = smoothstep(ringWidth, 0.0, ringDist);
    ringIntensity *= invProgress; //随扩散衰减

    //环的颜色从内核到边缘渐变
    float ringColorMix = smoothstep(0.0, ringWidth, ringDist);
    float3 ringColor = lerp(coreColor, midColor, ringColorMix);

    //========== (B) 内部火焰残像 ==========
    //扩散后内部留下残余火焰
    float innerZone = smoothstep(ringRadius + 0.02, ringRadius * 0.3, dist);

    //旋转火焰噪声
    float2 fireUV = float2(
        angle / 6.2832 + 0.5 + uTime * 0.5,
        dist * 4.0 - uTime * 2.0
    );
    float fireFbm = fbm(fireUV * 3.0, 4);

    float fireIntensity = innerZone * fireFbm * invProgress * 0.8;

    //火焰颜色：中心亮，外层暗
    float radialGrad = 1.0 - dist / max(ringRadius + 0.01, 0.01);
    radialGrad = saturate(radialGrad);
    float3 fireColor = lerp(edgeColor, coreColor, pow(abs(radialGrad), 1.5));

    //========== (C) 等离子裂隙 ==========
    //从中心向外辐射的裂纹线条
    float crackCount = 8.0;
    float crackAngle = frac(angle / 6.2832 * crackCount + hash(floor(angle / 6.2832 * crackCount)) * 0.5);
    float crackSharp = smoothstep(0.08, 0.0, abs(crackAngle - 0.5));

    //裂隙只在环内部出现
    float crackMask = smoothstep(ringRadius + 0.05, ringRadius * 0.1, dist);
    float crackIntensity = crackSharp * crackMask * invProgress * 0.6;

    //裂隙颜色：明亮的核心色
    float3 crackColor = coreColor * 1.5;

    //========== (D) 暗能量余波 ==========
    //环外侧的暗色余波
    float outerWave = smoothstep(ringRadius, ringRadius + 0.15, dist)
                    * smoothstep(ringRadius + 0.3, ringRadius + 0.1, dist);

    //暗波噪声纹理
    float darkNoise = fbm(float2(angle * 2.0, dist * 6.0 - uTime) * 2.0, 3);
    float darkIntensity = outerWave * darkNoise * invProgress * 0.4;

    float3 darkColor = edgeColor * 0.5;

    //========== (E) 中心闪光 ==========
    float coreBurst = exp(-dist * 20.0) * invProgress * invProgress;
    float3 burstColor = coreColor * 2.0;

    //========== (F) 脉冲明暗变化 ==========
    float pulse = 1.0 + sin(uTime * 20.0 - dist * 30.0) * pulseIntensity * 0.2 * invProgress;

    //合成
    float3 finalColor = float3(0, 0, 0);
    finalColor += ringColor * ringIntensity * 1.5;
    finalColor += fireColor * fireIntensity;
    finalColor += crackColor * crackIntensity;
    finalColor += darkColor * darkIntensity;
    finalColor += burstColor * coreBurst;

    finalColor *= pulse;

    float totalAlpha = saturate(ringIntensity + fireIntensity + crackIntensity * 0.5 + darkIntensity + coreBurst);
    totalAlpha *= fadeAlpha;

    //边缘柔化
    float edgeFade = smoothstep(0.5, 0.42, dist);
    totalAlpha *= edgeFade;

    return float4(finalColor * totalAlpha, totalAlpha);
}

technique BrimstoneBlastWavePass {
    pass P0 {
        PixelShader = compile ps_3_0 PSBlastWave();
    }
}
