# 星见雅受击音：原始解码

这 4 个 WAV 由本机《绝区零》游戏包中的 Wwise WEM 直接解码，没有额外滤波、音量修正或叠层。已核对 `Impact_Unagi_Attack_Normal` 事件、SoundBank 的 `sourceID` 与 PCK 中的 RIFF/WEM；它们是可确认来源的原始声音层，不代表游戏内最终混音。

| WAV | Wwise 事件后缀 | sourceID | 媒体所在包 |
| --- | --- | ---: | --- |
| `Miyabi_Impact_Normal_03_1.wav` | `Normal_03_1` | 945659017 | `SoundBank_SFX_15.pck` |
| `Miyabi_Impact_Normal_03_2.wav` | `Normal_03_2` | 448183310 | `SoundBank_SFX_5.pck` |
| `Miyabi_Impact_Normal_06_1.wav` | `Normal_06_1` | 266303133 | `SoundBank_SFX_8.pck` |
| `Miyabi_Impact_Normal_06_2.wav` | `Normal_06_2` | 635095994 | `SoundBank_SFX_6.pck` |

敌人 SO 引用的是相邻 `ZZZ_Miyabi_WwisePreview/` 中应用了已知固定参数的同名试听版，以便与原先的 01/02 受击音保持同一种处理方式。
