# 《绝区零》敌人攻击预警提示声

`ZZZ_EnemyAttackWarning_CommonHint.wav` 由本机游戏包中的 WEM 直接解码。没有额外滤波、音量修正或与其他声音合成。格式为 44.1 kHz、单声道、16 位 PCM，长度约 1.13 秒。

来源核对：

- Wwise 事件：`Play_SFX_Monster_Common_Hint`，位于 `SoundBank_SFX_2.pck`，事件 Bank ID `562845624`。
- 事件的声音对象指向 `sourceID 411682766`。原始 WEM 位于 `SoundBank_SFX_15.pck`，媒体 Bank ID `4245690530`。
- 原始 WEM SHA-256：`bc82656c541103f4a0aa1d05f790cde169025c90ec8c7301eebe9d973f7535fb`。
- [针对攻击预警提示声的公开替换记录](https://gamebanana.com/sounds/86439)使用同一个 WEM ID，作为用途的交叉核对；此目录中的 WAV 来自本机游戏包，并非替换模组。

这个 Wwise 事件只有一个音源，没有发现按黄色／红色闪光分别切换的音频。颜色与播放时刻由游戏逻辑决定；本项目尚未配置到敌人攻击动作，也未在游戏内核对触发时机。
