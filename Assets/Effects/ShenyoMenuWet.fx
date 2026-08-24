// ============================================================================
//ShenyoMenuWet.fx 鬼湖夜雨主菜单湿屏合成（雨水打在镜头上）
//TechWet：把整幅场景RT当玻璃后的世界重采样——
//  两层静态水珠（慢生慢干、内部倒像折射、月白窄缘）
//  + 两层瞬时溅击（雨点砸镜头：瞬现→溅环扩散→残珠微坠渐干，频率随uGust）
//  + 两路下滑水痕（珠头坠行、身后湿迹渐干、沿途轻折射）
//  + 屏缘湿雾（十字4邻域软化+惨白提亮，向屏心让路保可读性）
//  + 旧水道竖纹微明暗；雷闪时珠缘溅环齐亮
//屏心水珠密度刻意压低：标题/按钮后的场景只允许轻微扰动
//s0=场景RT（LinearClamp） s1=PerlinNoise
//绑定噪声实测值域 0.227~0.776，高阈值一律先过 nrm 归一
//直线算术无动态分支；Opaque 整幅输出
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float2 uScreenSize; //像素
float uWet;         //0-1 湿度总闸
float uFlash;       //0-1 雷闪包络（珠缘齐亮）
float uGust;        //0-1 风暴脉动：溅击频率/水珠密度/水痕活跃度同源呼吸
float uIntensity;   //0-1 入场渐显（缘光随场景一起浮现）

static const float3 RIM_PALE = float3(0.560, 0.630, 0.650); //珠缘惨白
static const float3 MIST_PALE = float3(0.200, 0.235, 0.245);//湿雾

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//绑定噪声实测值域 0.227~0.776
float nrm(float v) {
    return saturate((v - 0.227) / 0.549);
}

float h11(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
}

//静态水珠层：返回 (折射偏移px.xy, 缘光, 珠体覆盖)
float4 dropLayer(float2 pix, float cell, float seed, float gateTh, float aspect) {
    float2 g = pix / cell;
    float2 id = floor(g);
    float2 f = frac(g);
    float n = id.x * 7.13 + id.y * 113.7 + seed;

    //稀疏门（软门：gust推密时渐显而非整珠闪现）+ 屏心让路（按格心位置，保证同珠一致）
    float gate = saturate((h11(n + 17.0) - gateTh) * 10.0);
    float2 cUv = (id + 0.5) * cell / uScreenSize;
    float2 vdc = (cUv - 0.5) * float2(aspect, 1.0);
    float centerBias = lerp(0.12, 1.0, smoothstep(0.20, 0.58, length(vdc)));

    //慢生慢干的生命周期
    float ph = frac(uTime * (0.015 + h11(n + 3.3) * 0.030) + h11(n + 7.9));
    float presence = smoothstep(0.02, 0.10, ph) * (1.0 - smoothstep(0.60, 0.95, ph));
    presence *= gate * centerBias;

    float2 jit = float2(h11(n + 1.7), h11(n + 5.2));
    float2 center = 0.5 + (jit - 0.5) * 0.52;
    float size = lerp(0.12, 0.24, h11(n + 9.4));
    float2 d = (f - center) * float2(1.0, 1.18);
    float r = length(d) / max(size, 0.001);
    float inside = step(r, 1.0);
    float dome = saturate(1.0 - r);
    dome = dome * dome * (3.0 - 2.0 * dome);

    //珠内倒像：向珠心反向采样，越靠缘拉得越远
    float2 off = -d / max(size, 0.001) * cell * 0.80 * inside * presence;
    //缘光偏下缘（水珠聚光在底），不画均匀圆圈
    float rimW = 0.40 + 0.60 * saturate(0.5 + d.y / max(size, 0.001) * 0.9);
    float rim = saturate(1.0 - abs(r - 0.86) * 6.5) * inside * presence * rimW;
    return float4(off, rim, dome * inside * presence);
}

//瞬时溅击：雨点砸上镜头——瞬现→溅环扩散→残珠微坠渐干；逐拍换落点
//返回 (折射偏移px.xy, 溅环亮度, 珠体覆盖)
float4 impactLayer(float2 pix, float cell, float seed, float gateTh, float aspect) {
    float2 g = pix / cell;
    float2 id = floor(g);
    float2 f = frac(g);
    float n = id.x * 5.97 + id.y * 91.3 + seed;

    //稀疏门随uGust提密（软门防整珠闪现）+ 屏心弱让路（溅击允许更靠中）
    float gate = saturate((h11(n + 23.0) - gateTh) * 10.0);
    float2 cUv = (id + 0.5) * cell / uScreenSize;
    float2 vdc = (cUv - 0.5) * float2(aspect, 1.0);
    float centerBias = lerp(0.35, 1.0, smoothstep(0.16, 0.52, length(vdc)));

    //快周期：瞬现→渐干；每拍重播种落点与珠径
    float cyc = 1.5 + h11(n + 3.7) * 2.5;
    float k = floor(uTime / cyc + h11(n + 8.1));
    float t = frac(uTime / cyc + h11(n + 8.1));
    float hit = smoothstep(0.0, 0.02, t);
    float dry = 1.0 - smoothstep(0.45, 0.92, t);
    float presence = hit * dry * gate * centerBias;

    float2 jit = float2(h11(n + 1.3 + k * 13.7), h11(n + 6.6 + k * 7.3));
    float2 center = 0.5 + (jit - 0.5) * 0.30;
    center.y += t * 0.05;   //残珠微坠
    float size = lerp(0.10, 0.16, h11(n + 12.9 + k * 3.1));
    float2 d = (f - center) * float2(1.0, 1.15);
    float r = length(d) / max(size, 0.001);
    float inside = step(r, 1.0);
    float dome = saturate(1.0 - r);
    dome = dome * dome * (3.0 - 2.0 * dome);

    //溅环：命中头一拍自珠心扩张的亮环 + 命中瞬间中心亮点——"砸上来"的冲击读感
    float ringT = saturate(t / 0.14);
    float ringR = lerp(0.30, 2.20, ringT) * size;
    float ring = saturate(1.0 - abs(length(d) - ringR) / (size * 0.75));
    ring *= (1.0 - ringT) * (1.0 - ringT) * gate * centerBias;
    float hitFlash = exp(-r * r * 4.0) * (1.0 - smoothstep(0.0, 0.06, t)) * gate * centerBias;
    ring += hitFlash * 1.3;

    float2 off = -d / max(size, 0.001) * cell * 0.85 * inside * presence;
    float rimW = 0.40 + 0.60 * saturate(0.5 + d.y / max(size, 0.001) * 0.9);
    float rim = saturate(1.0 - abs(r - 0.86) * 6.0) * inside * presence * rimW;
    return float4(off, ring + rim * 0.45, dome * inside * presence);
}

//下滑水痕：珠头坠行 + 身后湿迹渐干；返回 (折射偏移px.xy, 珠头缘光)
float3 trickle(float2 pix, float colW, float seed, float speedMul) {
    float cx = floor(pix.x / colW);
    float n = cx * 7.31 + seed;
    //激活列随uGust渐增（软门：靠近门限的列淡入淡出）
    float active = saturate((h11(n + 8.0) - lerp(0.64, 0.40, uGust)) * 8.0);
    float spd = (0.050 + h11(n + 2.2) * 0.075) * speedMul;
    float headY = frac(uTime * spd + h11(n + 4.4));

    //蜿蜒路径
    float xj = (noiseTex(float2(pix.y / uScreenSize.y * 2.4 + n * 0.17, n * 0.31)) - 0.5) * colW * 0.42;
    float xc = (cx + 0.5) * colW + xj;
    float dx = pix.x - xc;
    float dHead = pix.y / uScreenSize.y - headY;

    //珠头
    float2 hd = float2(dx, dHead * uScreenSize.y * 0.85) / 6.5;
    float head = exp(-dot(hd, hd));
    //湿迹：头上方 240px 内渐干渐窄
    float trailT = saturate(1.0 + dHead * uScreenSize.y / 240.0);
    float above = step(dHead, 0.0);
    float trailW = lerp(1.6, 3.6, trailT);
    float trail = saturate(1.0 - abs(dx) / trailW) * trailT * trailT * above;

    float m = (head + trail * 0.50) * active;
    float2 off = float2(-dx * 0.55, 5.5) * m;
    return float3(off, head * active);
}

float4 PSWet(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float2 pix = uv * uScreenSize;
    float2 texel = 1.0 / uScreenSize;
    float aspect = uScreenSize.x / uScreenSize.y;

    //====== 折射源汇总：静珠密度与溅击频率都随uGust呼吸 ======
    float4 dropA = dropLayer(pix, 34.0, 3.1, lerp(0.88, 0.68, uGust), aspect);
    float4 dropB = dropLayer(pix, 78.0, 41.7, lerp(0.86, 0.62, uGust), aspect);
    float4 impA = impactLayer(pix, 46.0, 7.7, lerp(0.90, 0.58, uGust), aspect);
    float4 impB = impactLayer(pix, 120.0, 29.3, lerp(0.94, 0.70, uGust), aspect);
    float3 tr1 = trickle(pix, 112.0, 11.3, 1.0);
    float3 tr2 = trickle(pix, 176.0, 57.9, 0.55);
    float2 offset = (dropA.xy + dropB.xy * 1.25 + impA.xy + impB.xy * 1.35 + tr1.xy + tr2.xy) * uWet;

    //====== 屏缘湿雾：越靠角越糊越亮，缓慢呼吸 ======
    float2 vd = (uv - 0.5) * float2(aspect, 1.0);
    float fogMask = smoothstep(0.52, 1.05, length(vd)) * (0.90 + 0.10 * sin(uTime * 0.20));
    fogMask = (fogMask * 0.80 + 0.05) * uWet;

    //====== 场景重采样：折射位移 + 十字4邻域软化 ======
    float2 suv = uv + offset * texel;
    float blurR = 2.8 * fogMask;
    float3 c0 = tex2D(uImage0, suv).rgb;
    float3 c1 = tex2D(uImage0, suv + float2(texel.x * blurR, 0.0)).rgb;
    float3 c2 = tex2D(uImage0, suv - float2(texel.x * blurR, 0.0)).rgb;
    float3 c3 = tex2D(uImage0, suv + float2(0.0, texel.y * blurR)).rgb;
    float3 c4 = tex2D(uImage0, suv - float2(0.0, texel.y * blurR)).rgb;
    float3 col = lerp(c0, (c0 + c1 + c2 + c3 + c4) * 0.2, saturate(fogMask * 1.3));

    //====== 旧水道竖纹：极轻的明暗调制 ======
    float track = noiseTex(float2(uv.x * 5.0, uv.y * 0.55 + 7.7));
    col *= 1.0 + (track - 0.5) * 0.055 * uWet;

    //====== 珠体内部微压暗（水珠有厚度，不是亮圈贴纸）======
    float domeSum = saturate(dropA.w + dropB.w + impA.w + impB.w);
    col *= 1.0 - domeSum * 0.16 * uWet;

    //====== 湿雾提亮 + 珠缘惨白（雷闪齐亮）======
    col += MIST_PALE * fogMask * 0.10 * uIntensity;
    float rimSum = dropA.z * 0.36 + dropB.z * 0.55 + (tr1.z + tr2.z) * 0.55;
    col += RIM_PALE * rimSum * (0.16 + uFlash * uFlash * 0.30) * uWet * uIntensity;
    //溅击亮环：砸屏的冲击拍单独提亮，雷闪时齐闪
    float splash = impA.z + impB.z * 1.25;
    col += RIM_PALE * splash * (0.55 + uFlash * uFlash * 0.40) * uWet * uIntensity;

    return float4(col, 1.0);
}

technique TechWet {
    pass P0 {
        PixelShader = compile ps_3_0 PSWet();
    }
}
