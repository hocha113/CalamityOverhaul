// ============================================================================
//OniFinalePost.fx 鬼切终之太刀屏幕后处理：压暗聚焦 + 负片闪 + 沿刀线裂屏滑移 + 过刃切片
//采样 uImage0 屏幕；uDim/uNegative/uSplitOffset/uSliceAmp 均为 0 时透传
//处理顺序：裂屏/切片采样 → 压暗/去饱和/暗角 → 负片 → 裂缝辉光（保持缝隙最亮）
//ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

float uDim;          //0..1 场景压暗强度
float3 uDimTint;     //压暗色调（暗酒红）
float uDesat;        //压暗附带去饱和量 0..1
float2 uCenter;      //聚焦点 uv（压暗在此处保亮）
float uAspect;       //宽高比，校正距离场
float uNegative;     //0..1 负片反相强度
float uSplitOffset;  //裂屏滑移量（屏幕高度归一单位，两半各滑一半）
float uSplitAngle;   //刀线角度（屏幕空间弧度）
float2 uSplitCenter; //刀线中心 uv
float uSeamGlow;     //0..1 裂缝辉光强度
float3 uSeamColor;   //裂缝辉光色（白热绯红）
float4 uSliceGeo[4]; //过刃切片槽 (cx*Aspect, cy, perpX, perpY)
float4 uSliceAmp;    //四槽切片滑移量（屏幕高度归一），0=空槽

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //---- 裂屏滑移：以宽高比校正空间求点到刀线的有符号侧距，两半沿法线反向滑开 ----
    float2 ac = float2(coords.x * uAspect, coords.y);
    float2 c0 = float2(uSplitCenter.x * uAspect, uSplitCenter.y);
    float2 dir = float2(cos(uSplitAngle), sin(uSplitAngle));
    float2 perp = float2(-dir.y, dir.x);
    float side = dot(ac - c0, perp);

    float2 sampleUV = coords;
    float gapMask = 0.0;
    float gapEdge = 0.0;
    if (uSplitOffset > 1e-5)
    {
        float sideSign = side >= 0.0 ? 1.0 : -1.0;
        float slide = uSplitOffset * sideSign;
        sampleUV = coords - float2(perp.x / max(uAspect, 1e-3), perp.y) * slide;
        //两半各滑开 uSplitOffset 后，|side|<offset 的带不再有任何一半覆盖
        //这就是伤口内部：显示虚空而非被拉扯的屏幕像素（采样跳变也被虚空盖住，无需羽化）
        float inGap = uSplitOffset - abs(side);
        gapMask = smoothstep(-0.0015, 0.0015, inGap);
        gapEdge = exp(-pow(max(inGap, 0.0) / 0.010, 2.0));
    }

    //---- 过刃切片：新生刀线把画面本身切开一瞬，两侧沿法线错开、缝里烧一条白热线 ----
    float sliceGlow = 0.0;
    for (int i = 0; i < 4; i++)
    {
        float4 geo = uSliceGeo[i];
        float amp = uSliceAmp[i];
        float gate = step(1e-5, amp);
        float sliceSide = dot(ac - geo.xy, geo.zw);
        float sgn = sliceSide >= 0.0 ? 1.0 : -1.0;
        sampleUV -= float2(geo.z / max(uAspect, 1e-3), geo.w) * (sgn * amp * gate);
        float band = max(amp * 1.25 - abs(sliceSide), 0.0) / max(amp * 1.25, 1e-5);
        sliceGlow += band * gate * min(amp * 700.0, 1.0);
    }

    float4 src = tex2D(uImage0, sampleUV);
    float3 col = src.rgb;

    //---- 虚空带：世界的两半之间没有东西；断面（带边）贴一条白热镶边 ----
    if (gapMask > 0.001)
    {
        float3 voidCol = uSeamColor * 0.05 + float3(0.045, 0.008, 0.012);
        col = lerp(col, voidCol, gapMask * 0.92);
        col += uSeamColor * gapEdge * gapMask * 0.85;
    }

    //---- 压暗聚焦：聚焦点附近保亮，四周压向暗酒红；边缘再叠暗角 ----
    float2 toC = coords - uCenter;
    toC.x *= uAspect;
    float dist = length(toC);
    float focus = smoothstep(0.10, 0.72, dist);
    float dimW = uDim * (0.45 + 0.55 * focus);

    float lum = dot(col, float3(0.299, 0.587, 0.114));
    col = lerp(col, float3(lum, lum, lum), uDesat * dimW);
    col *= lerp(float3(1.0, 1.0, 1.0), uDimTint, dimW);

    float2 c = coords * 2.0 - 1.0;
    col *= 1.0 - dot(c, c) * 0.18 * uDim;

    //---- 负片闪：死寂末帧的整屏反相脉冲 ----
    col = lerp(col, saturate(float3(1.0, 1.0, 1.0) - col), uNegative);

    //---- 裂缝辉光：贴刀线一条白热光，随 uSeamGlow 呼吸 ----
    if (uSeamGlow > 1e-4)
    {
        float seamCore = exp(-pow(abs(side) / 0.0045, 2.0));
        float seamHalo = exp(-pow(abs(side) / 0.030, 2.0));
        col += uSeamColor * (seamCore * 1.6 + seamHalo * 0.45) * uSeamGlow;
    }

    //---- 过刃切片缝光：割开一瞬的白热细线，随滑移量衰减自灭 ----
    col += uSeamColor * sliceGlow * 1.15;

    return float4(col, src.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
