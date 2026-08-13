// ============================================================================
//KikasaUmbrella.fx 悬伞伞面处理(KikasaItemForm 血统:物品贴图 alpha 承载)
//TechCanopy:湿墨光泽随自旋横扫(旋转要被看见)+伞骨水膜下流+轮廓湿水线
//          +内建鬼眼(uEye 开阖/uEyeLook 瞳向/uEyeGlow 释放红芒,雨随目光)
//TechFill: 倒撑蓄墨的碗内液面(独立椭圆 quad,与贴图布局解耦)——
//          液面随 uSlosh 晃荡、缘高中低的张力弯月、满蓄溢缘
//坐标全笛卡尔;直线算术+普通 tex2D,FNA3D 安全;预乘输出进 AlphaBlend 批
//s0=物品贴图(TechFill 为画布,不采样) s1=PerlinNoise
//消费入口 KikasaRains/KikasaRainUmbrella.cs
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;
float uSeed;
float uSpinPhase;   //自旋相位(绕伞柄轴)
float uSpinSpeed;   //自旋速度归一 0~1,驱动扫光强度
float uWet;         //湿度 0~1,收伞时退场
float uEye;         //鬼眼开阖 0=闭 1=全睁
float2 uEyeLook;    //瞳向(单位向量)
float uEyeGlow;     //释放红芒 0~1
float2 uEyeCenter;  //眼心(帧内归一 uv)
float uEyeR;        //眼半径(帧高比例)
float uFill;        //蓄墨水位 0~1
float uSlosh;       //晃荡强度 0~1
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸
float2 uTexel;      //一像素 uv,轮廓检测用
float uAspect;      //帧宽/帧高
float3 uColInk;     //墨体近黑
float3 uColDeep;    //暗血缘
float3 uColCore;    //血芯(虹膜/红芒)
float3 uColSheen;   //湿反光

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

//==================== 伞面 ====================

float4 PSCanopy(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);
    float srcA = src.a;
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float2 nuv = float2(luv.x * uAspect, luv.y);

    float3 body = src.rgb;

    //伞盖区权重:上半为盖,扫光与水膜只认盖
    float canopyW = 1.0 - smoothstep(0.42, 0.64, luv.y);

    //湿光扫掠:高光带位置=sin(自旋相位),背面(cos<0)减弱——伪偏航自旋的光学证据
    float lx = luv.x * 2.0 - 1.0;
    float sweepPos = sin(uSpinPhase) * 0.7;
    float dSweep = lx - sweepPos;
    float band = exp2(-dSweep * dSweep * 26.0);
    float facing = cos(uSpinPhase) * 0.5 + 0.5;
    float sweep = band * canopyW * (0.18 + 0.82 * uSpinSpeed) * (0.35 + 0.65 * facing);

    //伞骨水膜:细竖流缓慢下淌
    float film = noiseTex(float2(nuv.x * 3.1 + uSeed, nuv.y * 0.8 - uTime * 0.35));
    float streak = smoothstep(0.62, 0.80, film) * canopyW * 0.30;

    //轮廓湿水线:剪影边缘一线湿亮,泡透的伞
    float aL = frameAlpha(uv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(uv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(uv - float2(0.0, uTexel.y));
    float aD = frameAlpha(uv + float2(0.0, uTexel.y));
    float rimShape = saturate((srcA - min(min(aL, aR), min(aU, aD))) * 2.4);

    //====== 鬼眼:盖在伞盖剪影上的开阖眼 ======
    float lid = max(uEye, 0.001);
    float exf = (luv.x - uEyeCenter.x) * uAspect / max(uEyeR, 0.001);
    float eyf = (luv.y - uEyeCenter.y) / max(uEyeR, 0.001);
    //杏仁孔径:横幅固定,纵幅吃开阖
    float a2 = exf * exf * 0.42 + eyf * eyf / (lid * lid);
    float eyeMask = (1.0 - smoothstep(0.80, 1.05, a2)) * step(0.03, uEye) * srcA;

    //瞳向偏移的虹膜与瞳孔
    float2 pq = float2(exf, eyf) - uEyeLook * 0.35;
    float r2 = dot(pq, pq);
    float iris = 1.0 - smoothstep(0.09, 0.17, r2);
    float pupil = 1.0 - smoothstep(0.012, 0.032, r2);
    float2 hq = pq - float2(0.12, -0.12);
    float glint = 1.0 - smoothstep(0.003, 0.010, dot(hq, hq));

    float3 sclera = float3(0.74, 0.68, 0.66);
    body = lerp(body, sclera, eyeMask * 0.92);
    body = lerp(body, uColCore * 0.95, eyeMask * iris);
    body = lerp(body, uColInk * 0.65, eyeMask * pupil);

    //====== 预乘合成:本体乘光,湿光走加色项 ======
    float aOut = srcA * vc.a;
    float3 outCol = body * vc.rgb * aOut;
    float3 glow = uColSheen * (sweep * 0.85 + streak * uWet + rimShape * 0.36 * uWet)
                + uColSheen * glint * eyeMask * 0.8
                + uColCore * exp2(-abs(a2 - 1.0) * 14.0) * uEyeGlow * step(0.03, uEye);
    outCol += glow * srcA * vc.a;
    return float4(outCol, aOut);
}

//==================== 倒撑蓄墨液面 ====================

float4 PSFill(float4 vc : COLOR0, float2 coords : TEXCOORD0) : COLOR0 {
    float2 raw = coords * 2.0 - 1.0;

    //碗口椭圆
    float2 q = float2(raw.x, raw.y * 1.9);
    float bowl = 1.0 - smoothstep(0.88, 1.0, length(q));

    //液面线:水位自下而上,张力弯月(缘高中低)+晃荡行波
    float level = lerp(0.95, -0.42, uFill);
    float slosh = sin(raw.x * 4.2 + uTime * 5.2 + uSeed * 5.0) * 0.07 * uSlosh
                + sin(raw.x * 2.1 - uTime * 3.4 + uSeed) * 0.05 * uSlosh;
    float meniscus = raw.x * raw.x * 0.10;
    float surfY = level + slosh - meniscus;
    float ink = smoothstep(surfY - 0.03, surfY + 0.05, raw.y) * bowl;

    //液面高光线与浅层
    float surfLine = exp2(-(raw.y - surfY) * (raw.y - surfY) * 220.0) * bowl;
    float depthT = saturate((raw.y - surfY) * 1.4);

    //墨面微涌
    float n = noiseTex(float2(raw.x * 0.7 + uSeed, raw.y * 0.5 - uTime * 0.12));

    float3 col = lerp(uColDeep, uColInk, depthT * 0.8 + n * 0.15);
    col = lerp(col, uColCore, (1.0 - depthT) * 0.22);

    float aInk = ink * 0.94;
    float3 outCol = col * aInk + uColSheen * surfLine * (0.35 + 0.4 * uSlosh) * step(0.02, uFill);
    float a = saturate(aInk + surfLine * 0.25 * step(0.02, uFill));

    float guard = smoothstep(1.0, 0.86, max(abs(raw.x), abs(raw.y)));
    float k = guard * vc.a;
    return float4(outCol * k, a * k);
}

technique TechCanopy {
    pass CanopyPass {
        PixelShader = compile ps_3_0 PSCanopy();
    }
}

technique TechFill {
    pass FillPass {
        PixelShader = compile ps_3_0 PSFill();
    }
}
