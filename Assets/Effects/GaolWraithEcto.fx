// ============================================================================
//GaolWraithEcto.fx 深牢怨灵鬼躯灵质材质
//以原版 Wraith 贴图 alpha 为轮廓，体内填青灰灵质：缓慢上升的双频灵流 +
//下摆常态撕散成雾 + 轮廓冷雾缘光 + 胸腔狱火透光。
//uGroundV=出场地线（帧内 v，噪声毛边渗出，传 2 关闭）；uDissolve=死亡自下
//而上蚀散（苍白魂缘）；uVeil=隐袭雾化（噪声碎解成雾块）。
//帧区域由 uUvRect 归一，邻域采样全部钳在帧内防串帧（KikasaHound 同款纪律）。
//预乘输出进 AlphaBlend；门控全走 step/lerp/smoothstep，无动态分支，无极角。
//s0=Wraith 贴图 s1=PerlinNoise（实测值域 0.227~0.776，阈值过 nrm 归一）
// ============================================================================

sampler uImage0 : register(s0);
// 噪声固定 s1：sampler_state 自动分配在 SpriteBatch 下必被 s0 覆写；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

float uTime;        //秒
float uSeed;        //实例相位
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸（纹理 uv 空间，C# 已上下内缩 1px）
float2 uTexel;      //一像素的 uv 尺寸
float uAspect;      //帧宽/帧高，噪声采样防拉伸
float uFlipH;       //1=水平翻转（画面朝右）；在采样里做，不靠 SpriteEffects
float uGroundV;     //出场地线帧内 v；>1.5 视为关闭
float uDissolve;    //0=完好 1=自下而上散尽
float uVeil;        //0=实体 1=雾化散解（隐袭）
float uFireLevel;   //胸腔狱火透光强度 0~1.2
float2 uHeartUv;    //狱火心口帧内 uv（已按翻转折算）
float3 uFireColor;  //狱火色（P2 偏白热）

//====== 灵质调色（青灰怨魂：深椎→灵体→苍白高光）======
static const float3 ECTO_DEEP = float3(0.212, 0.306, 0.361);
static const float3 ECTO_BODY = float3(0.675, 0.784, 0.816);
static const float3 ECTO_PALE = float3(0.874, 0.933, 0.945);
static const float3 MIST_COLD = float3(0.376, 0.455, 0.502);

//PerlinNoise.r 实测值域 0.227~0.776，归一后再进阈值
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

float noiseTex(float2 uv) {
    return nrm(tex2D(noiseSamp, uv).r);
}

//采样贴图 alpha，钳在帧区域内防串帧
float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

float4 PSBody(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    //帧内归一坐标；qx 为画面朝向坐标（翻转在采样里做）
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float qx = lerp(luv.x, 1.0 - luv.x, uFlipH);
    float2 nuv = float2(luv.x * uAspect, luv.y);

    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    float2 srcUv = clamp(uUvRect.xy + float2(qx, luv.y) * uUvRect.zw, lo, hi);
    float4 src = tex2D(uImage0, srcUv);
    float srcA = src.a;
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));

    //====== 灵质体：明度伽马拉开后映入青灰三色谱，形体细节全保 ======
    float l2 = pow(saturate(lum * 1.12), 1.30);
    float3 body = lerp(ECTO_DEEP, ECTO_BODY, smoothstep(0.10, 0.80, l2));
    body += ECTO_PALE * smoothstep(0.66, 0.98, l2) * 0.38;

    //====== 体内灵流：双频反速上升（灵质不坠反升的内脏运动）======
    float n0 = noiseTex(nuv * 1.05 + float2(uSeed, -uTime * 0.14 + uSeed * 3.0));
    float n1 = noiseTex(nuv * 2.35 + float2(-uSeed * 1.7, -uTime * 0.31));
    float flow = n0 * 0.62 + n1 * 0.38;
    body *= 0.80 + flow * 0.42;
    //体内偶发亮缕（灵流的窄高光带）
    body += ECTO_PALE * smoothstep(0.78, 0.96, flow) * 0.30;

    //====== 轮廓冷雾缘光：四邻域边检，一线苍白水膜 ======
    float aL = frameAlpha(srcUv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(srcUv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(srcUv - float2(0.0, uTexel.y));
    float aD = frameAlpha(srcUv + float2(0.0, uTexel.y));
    float minN = min(min(aL, aR), min(aU, aD));
    float rimShape = saturate((srcA - minN) * 2.4);
    float3 rim = ECTO_PALE * rimShape * (0.30 + 0.25 * n1);

    //====== 下摆常态撕散：底段被上滚噪声咬散（宽软带半透过渡，读作雾不读作破洞）======
    float hemGate = smoothstep(0.62, 0.88, luv.y);
    float nF = noiseTex(nuv * 1.75 + float2(uSeed * 1.9, -uTime * 0.21));
    float hemThr = saturate((luv.y - 0.62) / 0.38) * 0.72;
    float frayKeep = lerp(1.0, smoothstep(hemThr - 0.12, hemThr + 0.18, nF) * 0.85 + 0.15, hemGate);
    //撕口缘挂一线苍白（雾正从下摆剥离）
    rim += MIST_COLD * exp(-abs(nF - hemThr) * 14.0) * hemGate * 0.55;

    //====== 出场地线渗出：噪声毛边裁显 + 凝散边界狱火线 ======
    float clipGate = 1.0 - step(1.5, uGroundV);
    float gWob = (nF - 0.5) * 0.06 * clipGate;
    float groundKeep = lerp(1.0, 1.0 - smoothstep(uGroundV - 0.020, uGroundV + 0.028, luv.y + gWob), clipGate);
    float emergeLine = exp(-pow((luv.y - uGroundV) * 22.0, 2.0)) * clipGate * srcA;

    //====== 死亡自下而上蚀散：噪声撕裂前沿 + 苍白魂缘 ======
    float dGate = step(0.001, uDissolve);
    float dFront = 1.0 - uDissolve * 1.12;
    float dd = luv.y + (nF - 0.5) * 0.24 - dFront;
    float dissolveKeep = lerp(1.0, 1.0 - smoothstep(0.0, 0.10, dd), dGate);
    float soulEdge = exp(-dd * dd / 0.0028) * dGate;
    rim += ECTO_PALE * soulEdge * 0.85;

    //====== 隐袭雾化：整体被噪声阈值碎解成雾块，边缘先散 ======
    float vGate = step(0.001, uVeil);
    float nV = noiseTex(nuv * 1.45 + float2(uTime * 0.07 + uSeed * 5.0, -uTime * 0.16));
    float veilKeep = lerp(1.0, smoothstep(uVeil - 0.13, uVeil + 0.13, nV), vGate);
    rim += MIST_COLD * exp(-abs(nV - uVeil) * 10.0) * vGate * saturate(uVeil * 3.0) * 0.5;
    //雾化中的躯体褪向冷雾色
    body = lerp(body, MIST_COLD, uVeil * 0.65);

    //====== 胸腔狱火透光：灵质里烧一簇冷粉，允许少量溢出轮廓 ======
    float2 hd = (luv - uHeartUv) * float2(uAspect, 1.0);
    float heartCore = exp(-dot(hd, hd) * 340.0);
    float heartHalo = exp(-dot(hd, hd) * 60.0);
    float3 fire = uFireColor * (heartCore * 1.15 + heartHalo * 0.34) * uFireLevel;

    //====== 合成（预乘输出）======
    float keep = frayKeep * groundKeep * dissolveKeep * veilKeep;
    float aOut = srcA * keep * vc.a;
    //灵质半透不压死；vc.rgb 携带外界光照（C# 侧已按半自发光折算）
    float3 col = body * vc.rgb * aOut * 0.92
        + rim * aOut
        + fire * (0.35 + 0.65 * srcA) * keep * vc.a
        + uFireColor * emergeLine * keep * vc.a * 0.9;
    return float4(col, aOut * 0.94);
}

technique TechBody {
    pass P0 {
        PixelShader = compile ps_3_0 PSBody();
    }
}
