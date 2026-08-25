// ============================================================================
//HimayoInkStroke.fx 夜樱主菜单：悬停行下的朱墨笔痕
//TechInk：一道自左向右落笔的朱墨横画——起笔顿压/行笔噪变粗细/收笔与前锋提尖，
//        边缘噪蚀+飞白露底；四周洇染晕开（悬停越久越湿越宽），洇边偏暖；
//        墨体内朱砂颗粒沉淀肌理；脊线挂一线亮绯湿光，前锋后新墨更亮、
//        行进头一点暖白；湿透后自笔画淌下1-2道垂痕，尖端挂珠将坠未坠
//与 ShenyoMenuInk 同宗笔画语法，材质故事换朱墨：绯墨沉底可压暗底色、
//洇边暖橙、亮绯湿光、暖白笔头，对齐夜樱色板（PetalCrimson/AccentBloom）
//quad 契约：整幅四边形 uv 0..1，横向为行进向；uAspect=宽/高 供等比换算
//s0=占位白图（批次主纹理，不采样） s1=PerlinNoise
//绑定噪声实测值域 0.227~0.776，高阈值一律先过 nrm 归一
//预乘输出进 AlphaBlend（暗墨要能压暗底色）；直线算术无动态分支
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;    //秒
float uReveal;  //0-1 悬停进度=笔画铺开
float uWet;     //0-1 悬停持续湿润度：洇染增宽、垂痕生长
float uSeed;    //逐行错相
float uAspect;  //quad 宽/高
float uFade;    //菜单整体渐显

static const float3 INK_MURK = float3(0.118, 0.016, 0.030);  //朱墨墨体（压暗底色的深绯）
static const float3 INK_BLEED = float3(0.392, 0.078, 0.086); //洇边暖绯（朱砂在纸上洇开偏橙）
static const float3 INK_SHEEN = float3(0.929, 0.408, 0.494); //湿朱墨亮绯光泽
static const float3 INK_PALE = float3(1.000, 0.769, 0.741);  //笔头新墨暖白

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//绑定噪声实测值域 0.227~0.776
float nrm(float v) {
    return saturate((v - 0.227) / 0.549);
}

float hash1(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
}

float4 PSInk(float2 uv : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float reveal = saturate(uReveal);
    float headX = reveal * 1.06 - 0.03;

    //====== 笔迹脊线：静态蜿蜒 + 极低幅的活墨蠕动 ======
    float spineY = 0.26
        + (noiseTex(float2(uv.x * 1.6 + uSeed * 3.7, uSeed * 0.913)) - 0.5) * 0.16
        + sin(uv.x * 9.0 + uTime * 1.1 + uSeed * 8.0) * 0.010;

    //====== 粗细：起笔顿压、行笔噪变、收笔与前锋提尖 ======
    float press = nrm(noiseTex(float2(uv.x * 2.8 + uSeed * 7.1, uSeed * 0.7 + 3.3)));
    float halfW = 0.085 * (0.70 + 0.60 * press);
    halfW *= 1.0 + 0.35 * exp(-uv.x * 22.0);
    halfW *= smoothstep(1.03, 0.86, uv.x);
    float tipTaper = smoothstep(headX, headX - 0.10, uv.x);
    halfW *= 0.25 + 0.75 * tipTaper;

    //前锋外未落笔、起端收齐
    float drawn = smoothstep(headX + 0.012, headX - 0.012, uv.x) * smoothstep(-0.012, 0.02, uv.x);

    //====== 本体：噪蚀边 + 飞白露底 ======
    float dy = uv.y - spineY;
    float edgeN = noiseTex(float2(uv.x * 6.5 + uSeed * 11.0, uv.y * 1.7 + uSeed * 5.0)) - 0.5;
    float dist = abs(dy + edgeN * 0.05) / max(halfW, 0.001);
    float body = smoothstep(1.0, 0.62, dist) * drawn;
    float gaps = smoothstep(0.60, 0.80, nrm(noiseTex(
        float2(uv.x * 7.7 + uSeed * 13.0, uv.y * 2.3 - uSeed))));
    body *= 1.0 - gaps * 0.52;

    //====== 朱砂颗粒：墨体内高频双阈值沉淀肌理（暗粒沉、亮粒浮） ======
    float granule = nrm(noiseTex(float2(uv.x * 12.0 + uSeed * 17.0, uv.y * 4.2 + uSeed * 3.1)));
    float grainDark = smoothstep(0.68, 0.86, granule);
    float grainLite = smoothstep(0.16, 0.04, granule);

    //====== 洇染：更宽更软的晕圈，悬停越久（越湿）洇得越开 ======
    float bleedW = halfW * (2.4 + uWet * 1.6) + 0.02;
    float bleedD = abs(dy) / max(bleedW, 0.001);
    float bleedN = 0.62 + 0.38 * noiseTex(float2(uv.x * 3.4 + uSeed, uv.y * 1.2 + uTime * 0.03));
    float bleed = exp(-bleedD * bleedD * 2.2) * bleedN * drawn * (0.34 + uWet * 0.36);
    bleed *= 1.0 - body * 0.55;

    //====== 湿光：贴脊线的窄反光带，活墨蠕动明暗；前锋刚画过的新墨更亮 ======
    float sheen = exp(-pow(abs(dy) / max(halfW * 0.45, 0.001), 2.0)) * drawn;
    float crawl = 0.70 + 0.30 * nrm(noiseTex(
        float2(uv.x * 5.0 - uTime * 0.10 + uSeed, uv.y * 2.0 + uSeed * 2.0)));
    float fresh = smoothstep(0.30, 0.02, abs(uv.x - headX)) * drawn;

    //行进笔头：一点暖白，铺满后自然熄灭（headX 越界后距离拉大）
    float headDx = (uv.x - headX) * uAspect;
    float headGlow = exp(-(headDx * headDx / 0.006 + dy * dy / max(halfW * halfW * 2.6, 0.0005)))
        * smoothstep(0.02, 0.10, reveal) * (1.0 - smoothstep(0.92, 1.0, reveal));

    //====== 垂痕：至多两道自脊线下淌，随湿润度生长，尖端挂珠 ======
    float drip = 0.0;
    float dripGlint = 0.0;
    [unroll]
    for (int k = 0; k < 2; k++) {
        float hk = hash1(uSeed * 1.71 + (float)k * 2.618);
        //两道分区取位防撞：前段/后段各一
        float dripX = k == 0 ? (0.14 + hk * 0.30) : (0.52 + hk * 0.30);
        //第二道只有约六成的行会有
        float gate = k == 0 ? 1.0 : step(0.4, hash1(uSeed * 3.913 + 7.7));
        float grow = smoothstep(0.12, 1.0, uWet);
        float len = (0.22 + hash1(hk * 9.1) * 0.30) * grow;
        float on = step(dripX, headX) * gate * step(0.001, len);

        float ddx = (uv.x - dripX) * uAspect;
        float below = dy;
        float t = saturate(below / max(len, 0.001));
        float w = 0.12 * (1.0 - t * 0.55);
        //挂珠：尾端微鼓
        w += 0.10 * smoothstep(0.72, 0.95, t) * (1.0 - smoothstep(0.96, 1.0, t));
        //轻缓的一次弯，不做高频蛇摆
        float wob = sin(below * 9.0 + hk * 12.0) * 0.012;
        float dmask = saturate(1.0 - abs(ddx + wob) / max(w, 0.001))
            * step(0.0, below) * (1.0 - smoothstep(0.92, 1.0, t)) * on;
        dmask *= 0.50 + 0.50 * (1.0 - t);
        drip = max(drip, dmask);
        dripGlint = max(dripGlint, dmask * smoothstep(0.70, 0.94, t));
    }

    //====== 预乘合成：绯墨沉底 + 洇边回暖 + 湿光提亮 ======
    float mul = uFade * vertexColor.a;
    float inkMass = body * 0.90 + bleed * 0.80 + drip * 0.85;
    float3 rgb = INK_MURK * inkMass * (1.0 - grainDark * 0.38 * body);
    rgb += INK_BLEED * bleed * 0.55;
    rgb += INK_SHEEN * sheen * crawl * (0.22 + fresh * 0.46);
    rgb += INK_SHEEN * grainLite * body * 0.10;
    rgb += INK_SHEEN * dripGlint * 0.36;
    rgb += INK_PALE * headGlow * 0.85;
    float alpha = saturate(body * 0.88 + bleed * 0.60 + drip * 0.80 + sheen * 0.18 + headGlow * 0.5);

    return float4(rgb * mul * vertexColor.rgb, alpha * mul);
}

technique TechInk {
    pass P0 {
        PixelShader = compile ps_3_0 PSInk();
    }
}
