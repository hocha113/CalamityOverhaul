// ============================================================================
//OniFinalePost.fx 鬼切终之太刀屏幕后处理：压暗聚焦 + 负片闪 + 沿刀线裂屏滑移
//采样 uImage0 屏幕；uDim/uNegative/uSplitOffset 均为 0 时透传
//处理顺序：裂屏采样 → 压暗/去饱和/暗角 → 负片 → 裂缝辉光（保持缝隙最亮）
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

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //---- 裂屏滑移：以宽高比校正空间求点到刀线的有符号侧距，两半沿法线反向滑开 ----
    float2 ac = float2(coords.x * uAspect, coords.y);
    float2 c0 = float2(uSplitCenter.x * uAspect, uSplitCenter.y);
    float2 dir = float2(cos(uSplitAngle), sin(uSplitAngle));
    float2 perp = float2(-dir.y, dir.x);
    float side = dot(ac - c0, perp);

    float2 sampleUV = coords;
    if (uSplitOffset > 1e-5)
    {
        float sideSign = side >= 0.0 ? 1.0 : -1.0;
        //贴缝处滑移羽化，避免缝上像素采样瞬间跳变产生锯齿
        float slide = uSplitOffset * sideSign * smoothstep(0.0, 0.012, abs(side));
        sampleUV = coords - float2(perp.x / max(uAspect, 1e-3), perp.y) * slide;
    }

    float4 src = tex2D(uImage0, sampleUV);
    float3 col = src.rgb;

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

    return float4(col, src.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
