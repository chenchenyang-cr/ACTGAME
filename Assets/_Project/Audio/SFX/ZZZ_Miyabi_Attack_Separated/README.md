# 星见雅攻击素材：普通攻击与 End 分开

- `NormalAttack/`：普攻 01、02、03、05、06 的 7 个 `Skill` 分支试听 WAV。文件名中的 A/B 是同一攻击的不同候选，不是 End。这些 WAV 做过部分 Wwise 参数的近似处理，并非游戏内最终混音。
- `End/`：对应普攻的 5 个 `Foley ... End` 原始解码 WAV。没有与 `NormalAttack/` 的文件合成，也没有额外滤波或音量处理。具体事件、媒体 ID 和来源见 `End/README.md`。

两组声音保持独立，供动作编辑器分别选择和安排时间；此目录没有制作普攻加 End 的合成文件。敌人受击和弹反素材仍在相邻的 `ZZZ_Miyabi_WwisePreview/`。
