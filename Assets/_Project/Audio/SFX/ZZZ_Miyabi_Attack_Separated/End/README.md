# 星见雅普攻 Foley 原始解码层

这 5 个 WAV 由本机《绝区零》游戏包中的 Wwise WEM 直接解码，没有加滤波、音量修正或叠层。核对路径是 `Play_SFX_Char_Foley_Unagi_Attack_Normal_XX_End` 事件 → SoundBank 的 `sourceID` → PCK 内对应 RIFF/WEM。它们是可核实来源的原始声音层，**不是游戏内一次完整攻击的最终音效或混音**。

| WAV | Wwise 事件 | sourceID | 媒体所在包 |
| --- | --- | ---: | --- |
| `Miyabi_Foley_Normal_01_End.wav` | `Play_SFX_Char_Foley_Unagi_Attack_Normal_01_End` | 480994093 | `SoundBank_SFX_12.pck` |
| `Miyabi_Foley_Normal_02_End.wav` | `Play_SFX_Char_Foley_Unagi_Attack_Normal_02_End` | 433649114 | `SoundBank_SFX_4.pck` |
| `Miyabi_Foley_Normal_03_End.wav` | `Play_SFX_Char_Foley_Unagi_Attack_Normal_03_End` | 252619236 | `SoundBank_SFX_13.pck` |
| `Miyabi_Foley_Normal_05_End.wav` | `Play_SFX_Char_Foley_Unagi_Attack_Normal_05_End` | 714443549 | `SoundBank_SFX_4.pck` |
| `Miyabi_Foley_Normal_06_End.wav` | `Play_SFX_Char_Foley_Unagi_Attack_Normal_06_End` | 95253296 | `SoundBank_SFX_15.pck` |

这些 End 文件与相邻 `NormalAttack/` 中的普通攻击文件分别保存，没有合成。`NormalAttack/` 属于另一个 `Skill` 分支的近似处理版；游戏内的触发时刻、随机分支、辅助总线和最终混音尚未还原。
