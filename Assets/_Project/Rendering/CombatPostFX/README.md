# Combat Post FX

面向战斗命中与演出镜头的单 Pass URP 全屏效果。支持：

- 径向模糊
- 色差
- 暗角
- 闪白/彩色闪光
- 去饱和
- 横向故障撕裂
- 程序化速度线
- 胶片颗粒

## 推荐工作流：独立 Post FX 轨道

在能力编辑器的事件菜单中打开：

`Post FX`

其中每种屏幕效果都是一条独立轨道：

- Radial Blur
- Chromatic Aberration
- Vignette
- Flash
- Color
- Glitch
- Speed Lines
- Film Grain

每条轨道单独拥有：

- 能力时间轴上的独立起止位置。
- `Intensity`：最大强度。
- `Intensity Curve`：轨道范围内的强度曲线。
- 效果专属参数，例如径向模糊采样距离、故障行密度和速度线半径。

轨道支持编辑器预览，多条后处理轨道重叠时会在运行时自动混合。

## 从脚本、Animation Event 或 Timeline Signal 使用

时间轴之外，可以创建 `Combat/Post FX Playback Profile` 资源并使用
`CombatPostFxTrigger` 触发一次性效果。

- `Play()`：播放配置的 Profile。
- `PlayImpact()`：快速播放内置普通命中效果。
- `PlayFinisher()`：播放更强的终结技效果。

需要快速触发低层参数时仍可直接调用：

```csharp
CombatPostFX.CombatPostFxRuntime.Pulse(
    CombatPostFX.CombatPostFxSettings.Impact,
    0.16f);
```

如果冲击中心来自世界坐标：

```csharp
var settings = CombatPostFX.CombatPostFxSettings.Impact;
settings.center = CombatPostFX.CombatPostFxRuntime.WorldToViewport(hitPoint);
CombatPostFX.CombatPostFxRuntime.Pulse(settings, 0.16f);
```

## 渲染配置

编辑器首次导入时会把 `Combat Post FX` Renderer Feature 安装到项目各档 URP Renderer。
如更换或新增 Renderer，可执行 `Tools > Combat Post FX > Install Renderer Feature`。

效果默认在 URP 内置后处理之前执行，因此闪光和速度线可以继续参与现有 Bloom 与 Tonemapping。
