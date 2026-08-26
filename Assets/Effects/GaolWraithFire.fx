// ============================================================================
//GaolWraithFire.fx 冷粉狱火材质（怨灵狱火弹 + 拖尾/挥击风带共用）
//材质分野：冷粉 / 幽浮 / 无烟 / 缘滴上升（Arbiter 橙红带烟、KikasaWisp 金慢
//贴水线，本件是牢狱怨火——冷而不暖，白只走芯部呼吸不常驻）。
//TechBolt：弹体泪滴焰（placeholder 白像素 quad，+x=飞行向）——头圆尾撕，
//深粉缘全 alpha 遮挡 + 冷粉体 + 白热窄芯；噪声撕裂焰尾。
//TechTrail：vs+ps 条带（uv.x 0=头 1=尾）——三层焰谱同材质拖尾，尾段噪声
//撕成焰缕；uDecay=余韵蚀散（弹亡后条带交给余辉缓冲，自尾向头先蚀）。
//颜色走 uniform（狱火冷粉 / 铁风青灰两套色板复用同一份几何）。
//预乘输出进 AlphaBlend；无动态分支，无极角。s1=PerlinNoise（值域过 nrm）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;
float uFade;        //整体透明度 0~1
float uHot;         //白热芯增幅（P2 / 蓄力）
float uDecay;       //余韵蚀散 0~1（活体传 0）
float3 uColDeep;    //深缘（全 alpha，负责遮挡底色）
float3 uColBody;    //主体
float3 uColCore;    //白热芯

sampler noiseSamp : register(s1);

//PerlinNoise.r 实测值域 0.227~0.776
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

float noiseTex(float2 uv) {
    return nrm(tex2D(noiseSamp, uv).r);
}

//==================== TechBolt：泪滴焰体 ====================

float4 PSBolt(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    //+x=飞行向（头），-x=尾
    float xc = (uv.x - 0.5) * 2.0;
    float yc = (uv.y - 0.5) * 2.0;

    //泪滴 SDF：头短圆、尾长拖；尾区被噪声咬出焰舌
    float den = lerp(0.52, 1.30, smoothstep(-0.15, 0.15, xc));
    float nT = noiseTex(float2(xc * 0.85 - uTime * 1.6 + uSeed * 7.0, yc * 1.1 + uSeed * 3.0));
    float tailZone = saturate(-xc * 1.25);
    float rd = length(float2(xc * den, yc * (1.42 + tailZone * 0.5)));
    rd += (0.5 - nT) * 0.62 * tailZone;

    float bodyMask = 1.0 - smoothstep(0.46, 0.98, rd);
    float edge = smoothstep(0.30, 0.92, rd) * bodyMask;

    //白热芯：偏头部的窄核，呼吸明灭（白不常驻，芯只占体的小半）
    float flick = 0.78 + 0.22 * sin(uTime * 8.5 + uSeed * 11.0);
    float coreD = length(float2((xc - 0.22) * 1.7, yc * 2.4));
    float core = (1.0 - smoothstep(0.0, 0.42, coreD)) * flick;

    //焰面冷粉体：噪声给体内明暗，无烟
    float3 col = uColDeep * edge * 1.05
        + uColBody * bodyMask * (0.55 + nT * 0.45)
        + uColCore * core * (0.55 + uHot * 0.45);

    float alpha = bodyMask * uFade * vc.a;
    return float4(col * uFade * vc.a, alpha);
}

//==================== TechTrail：条带拖尾 ====================

struct VSInput {
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VSTrail(VSInput v) {
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PSTrail(PSInput input) : COLOR0 {
    float2 uv = input.TexCoords;
    float along = uv.x;                 //0 头 → 1 尾
    float cross_ = (uv.y - 0.5) * 2.0;  //-1 ~ 1

    //宽度包络：头满宽，向尾收窄；尾段被噪声咬得更碎
    float nW = noiseTex(float2(along * 2.5 - uTime * 1.05 + uSeed * 7.0, uv.y * 0.8 + uSeed));
    float halfW = (0.94 - along * 0.34) + (nW - 0.5) * 0.52 * along;
    float body = saturate(1.0 - abs(cross_) / max(halfW, 0.05));

    //尾段撕成焰缕：顺流窄条纹把整带咬开
    float streak = smoothstep(0.30, 0.72, noiseTex(float2(along * 4.6 - uTime * 1.9 + uSeed * 3.0, cross_ * 1.1 + uSeed * 5.0)));
    body *= lerp(1.0, streak, smoothstep(0.30, 0.92, along));
    //尾端渐灭（撕散收口，不平切）
    body *= 1.0 - smoothstep(0.72, 1.02, along + (nW - 0.5) * 0.18);

    //余韵蚀散：自尾向头吃掉，缘上挂一线苍芒
    float dFront = 1.0 - uDecay * 1.18;
    float dd = along + (nW - 0.5) * 0.16 - dFront;
    float decayKeep = 1.0 - smoothstep(0.0, 0.09, dd);
    float decayEdge = exp(-dd * dd / 0.0022) * step(0.001, uDecay);
    body *= decayKeep;

    //三层焰谱：深缘 / 冷粉体 / 白热芯线（芯只活在前 45%）
    float rim = smoothstep(0.42, 0.96, abs(cross_) / max(halfW, 0.05)) * body;
    float core = saturate(1.0 - abs(cross_) / 0.17) * (1.0 - smoothstep(0.05, 0.48, along)) * (0.6 + uHot * 0.4);

    float3 col = uColDeep * rim * 1.0
        + uColBody * body * (0.50 + nW * 0.42)
        + uColCore * core
        + uColCore * decayEdge * 0.6;

    float alpha = saturate(body + decayEdge * 0.4) * uFade * input.Color.a * (1.0 - uDecay * 0.45);
    return float4(col * uFade * input.Color.a * (1.0 - uDecay * 0.45), alpha);
}

technique TechBolt {
    pass P0 {
        PixelShader = compile ps_3_0 PSBolt();
    }
}

technique TechTrail {
    pass P0 {
        VertexShader = compile vs_3_0 VSTrail();
        PixelShader = compile ps_3_0 PSTrail();
    }
}
